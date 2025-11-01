using System.Collections.Concurrent;
using System.Text.Json;
using PageRankApp.Scheduler.Models;
using PageRankApp.Shared.Models;
using PageRankApp.Shared.Network;

namespace PageRankApp.Scheduler;

public static class Scheduler
{
	private static readonly ConcurrentDictionary<Guid, ClientConnection> _allSolvers = new();
	private static readonly ConcurrentQueue<ClientConnection> _availableSolvers = new();
	private static readonly ConcurrentQueue<CalculationTask> _largeTaskQueue = new();
	private static readonly ConcurrentQueue<CalculationTask> _smallTaskQueue = new();

	private const double DampingFactor = 0.85;
	private const int MaxIterations = 30;
	private const double Epsilon = 1e-6;

	private static volatile bool _isLargeTaskRunning = false;

	public const int LargeGraphNodeThreshold = 1000;
	private const double LargeTaskSolverQuota = 0.7;
	private const int SolversPerSmallTask = 2;

	public static async Task HandleNewConnectionAsync(ClientConnection connection)
	{
		Console.WriteLine($"Client connected from {connection.RemoteEndPoint}. Awaiting identification...");

		try
		{
			var initialMessage = await connection.ReadMessageAsync();
			if (initialMessage == null) return;

			if (initialMessage.Type == MessageType.RegisterSolver)
			{
				_allSolvers.TryAdd(connection.Id, connection);
				_availableSolvers.Enqueue(connection);
				Console.WriteLine($"Solver registered. Total solvers: {_allSolvers.Count}. Available: {_availableSolvers.Count}");
			}
			else if (initialMessage.Type == MessageType.SubmitGraph)
			{
				var graph = JsonSerializer.Deserialize<Graph>(initialMessage.JsonPayload);
				if (graph == null) { connection.Disconnect(); return; }

				var task = new CalculationTask(connection, graph);

				if (task.IsLargeTask)
				{
					int requiredForLargeTask = (int)Math.Ceiling(_allSolvers.Count * LargeTaskSolverQuota);
					if (_allSolvers.Count < requiredForLargeTask || requiredForLargeTask == 0)
					{
						Console.WriteLine($"[Task {task.TaskId}] REJECTED. Not enough total solvers ({_allSolvers.Count}) to run a large task (needs at least {requiredForLargeTask}).");
						connection.Disconnect();
						return;
					}
					_largeTaskQueue.Enqueue(task);
					Console.WriteLine($"[Task {task.TaskId}] Queued as LARGE. Pending large tasks: {_largeTaskQueue.Count}");
				}
				else
				{
					_smallTaskQueue.Enqueue(task);
					Console.WriteLine($"[Task {task.TaskId}] Queued as SMALL. Pending small tasks: {_smallTaskQueue.Count}");
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error during connection handling for {connection.Id}: {ex.Message}");
			connection.Disconnect();
		}
	}

	public static async Task TaskDispatcherLoop()
	{
		Console.WriteLine("Advanced Task Dispatcher is running.");
		while (true)
		{
			if (!_isLargeTaskRunning && _largeTaskQueue.TryPeek(out _))
			{
				int requiredForLargeTask = (int)Math.Ceiling(_allSolvers.Count * LargeTaskSolverQuota);
				if (_availableSolvers.Count >= requiredForLargeTask)
				{
					if (_largeTaskQueue.TryDequeue(out var largeTask))
					{
						_isLargeTaskRunning = true; 
						Console.WriteLine($"Starting LARGE task {largeTask.TaskId}. Locking large task slot.");
						_ = ProcessTaskAsync(largeTask, requiredForLargeTask);
					}
				}
			}

			if (_smallTaskQueue.TryPeek(out _))
			{
				int totalSmallQuota = _allSolvers.Count - (int)Math.Ceiling(_allSolvers.Count * LargeTaskSolverQuota);
				int largeTaskSolversInUse = _isLargeTaskRunning ? (int)Math.Ceiling(_allSolvers.Count * LargeTaskSolverQuota) : 0;
				int availableForSmallTasks = _availableSolvers.Count;

				if (availableForSmallTasks >= SolversPerSmallTask)
				{
					if (_smallTaskQueue.TryDequeue(out var smallTask))
					{
						Console.WriteLine($"Starting SMALL task {smallTask.TaskId}.");
						_ = ProcessTaskAsync(smallTask, SolversPerSmallTask);
					}
				}
			}

			await Task.Delay(500);
		}
	}

	private static async Task ProcessTaskAsync(CalculationTask task, int solversToAllocate)
	{
		List<ClientConnection> assignedSolvers = [];
		try
		{
			for (int i = 0; i < solversToAllocate; i++)
			{
				if (_availableSolvers.TryDequeue(out var solver)) assignedSolvers.Add(solver);
			}
			Console.WriteLine($"[Task {task.TaskId}] {assignedSolvers.Count} solvers allocated. POOL: {_availableSolvers.Count} left.");

			var finalRanks = await CalculatePageRankDistributedAsync(task.Graph, assignedSolvers);

			var resultMessage = new NetworkMessage { Type = MessageType.CalculationComplete, JsonPayload = JsonSerializer.Serialize(finalRanks) };
			await task.MauiClient.WriteMessageAsync(resultMessage);
		}
		catch (Exception ex)
		{ 

		}
		finally
		{
			Console.WriteLine($"[Task {task.TaskId}] Task finished. Returning {assignedSolvers.Count} solvers.");
			foreach (var solver in assignedSolvers)
			{
				if (solver.IsConnected) _availableSolvers.Enqueue(solver);
				else _allSolvers.TryRemove(solver.Id, out _); 
			}

			if (task.IsLargeTask)
			{
				_isLargeTaskRunning = false;
				Console.WriteLine($"Large task slot is now free.");
			}
			Console.WriteLine($"POOL STATUS: {_availableSolvers.Count} solvers are now available.");
			task.MauiClient.Disconnect();
		}
	}


	private static async Task<Dictionary<int, double>> CalculatePageRankDistributedAsync(Graph graph, List<ClientConnection> solvers)
	{
		int nodeCount = graph.Nodes.Count;
		if (nodeCount == 0) return [];

		var ranks = graph.Nodes.ToDictionary(n => n.Id, n => 1.0 / nodeCount);

		var outgoingLinks = graph.Nodes.ToDictionary(
			n => n.Id,
			n => graph.Edges.Count(e => e.SourceId == n.Id)
		);

		for (int i = 0; i < MaxIterations; i++)
		{
			Console.WriteLine($"--- Iteration {i + 1} for task... ---");

			var previousRanks = new Dictionary<int, double>(ranks);
			var tasks = new List<Task<PartialResult>>();
			var nodePartitions = Partition(graph.Nodes, solvers.Count);

			for (int j = 0; j < solvers.Count; j++)
			{
				var solver = solvers[j];
				var nodePartition = nodePartitions[j];

				if (nodePartition.Count == 0) continue;

				var solverTask = new SolverTask
				{
					FullGraph = graph,
					NodeIdsToCalculate = nodePartition.Select(n => n.Id).ToList(),
					CurrentRanks = ranks,
					OutgoingLinks = outgoingLinks
				};

				tasks.Add(ExecuteTaskOnSolverAsync(solver, solverTask));
			}

			var partialResults = await Task.WhenAll(tasks);

			var newRanks = new Dictionary<int, double>();
			foreach (var result in partialResults)
			{
				if (result?.CalculatedRanks == null) continue;
				foreach (var rankEntry in result.CalculatedRanks)
				{
					newRanks[rankEntry.Key] = rankEntry.Value;
				}
			}

			foreach (var nodeId in ranks.Keys)
			{
				if (!newRanks.ContainsKey(nodeId))
				{
					newRanks[nodeId] = ranks[nodeId];
				}
			}

			double rankSum = newRanks.Values.Sum();
			foreach (var key in newRanks.Keys)
			{
				newRanks[key] /= rankSum;
			}

			ranks = newRanks;

			double diff = previousRanks.Sum(kvp => Math.Abs(kvp.Value - ranks.GetValueOrDefault(kvp.Key, 0)));
			Console.WriteLine($"Iteration {i + 1} finished. Change (L1 Norm): {diff}");
			if (diff < Epsilon)
			{
				Console.WriteLine($"Converged after {i + 1} iterations.");
				break;
			}
		}

		return ranks;
	}

	private static async Task<PartialResult> ExecuteTaskOnSolverAsync(ClientConnection solver, SolverTask task)
	{
		var request = new NetworkMessage
		{
			Type = MessageType.AssignTask,
			JsonPayload = JsonSerializer.Serialize(task)
		};
		await solver.WriteMessageAsync(request);

		var response = await solver.ReadMessageAsync();
		if (response?.Type != MessageType.PartialResult)
		{
			Console.WriteLine($"Warning: Solver {solver.Id} returned an unexpected message type or disconnected.");
			return new PartialResult { CalculatedRanks = new Dictionary<int, double>() };
		}

		return JsonSerializer.Deserialize<PartialResult>(response.JsonPayload) ?? new PartialResult();
	}

	private static List<List<T>> Partition<T>(IEnumerable<T> source, int size)
	{
		var partitions = new List<List<T>>();
		for (int i = 0; i < size; i++)
		{
			partitions.Add([]);
		}

		int index = 0;
		foreach (var item in source)
		{
			partitions[index++ % size].Add(item);
		}
		return partitions;
	}
}
