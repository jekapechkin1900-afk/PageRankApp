using System.Collections.Concurrent;
using System.Text.Json;
using System.Net.Sockets;
using PageRankApp.Scheduler.Models;
using PageRankApp.Shared.Models;
using PageRankApp.Shared.Network;

namespace PageRankApp.Scheduler;

public static class Scheduler
{
	private static readonly ConcurrentDictionary<Guid, ClientConnection> _allSolvers = new();
	private static readonly ConcurrentQueue<ClientConnection> _availableSolvers = new();
	private static readonly ConcurrentDictionary<Guid, bool> _solverBusyState = new();
	private static readonly ConcurrentQueue<CalculationTask> _largeTaskQueue = new();
	private static readonly ConcurrentQueue<CalculationTask> _smallTaskQueue = new();

	private const int MaxIterations = 30;
	private const double Epsilon = 1e-6;

	private static volatile bool _isLargeTaskRunning = false;
	private static int _crashedSolversCount = 0;

	public const int LargeGraphNodeThreshold = 1000;
	private const double LargeTaskSolverQuota = 0.7;
	private const int SolversPerSmallTask = 2;

	public static async Task Main(string[] args)
	{
		_ = Task.Run(TaskDispatcherLoop);
		_ = Task.Run(WatchdogLoop);

		var listener = new TcpListener(System.Net.IPAddress.Any, 8888);
		listener.Start();
		Console.WriteLine("Scheduler started on port 8888.");

		while (true)
		{
			try
			{
				var tcpClient = await listener.AcceptTcpClientAsync();
				_ = HandleNewConnectionAsync(new ClientConnection(tcpClient));
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Accept error: {ex.Message}");
			}
		}
	}

	public static async Task HandleNewConnectionAsync(ClientConnection connection)
	{
		try
		{
			var initialMessage = await connection.ReadMessageAsync();
			if (initialMessage == null) return;

			if (initialMessage.Type == MessageType.RegisterSolver)
			{
				_allSolvers.TryAdd(connection.Id, connection);
				_availableSolvers.Enqueue(connection);
				_solverBusyState[connection.Id] = false;

				Console.WriteLine($"Solver registered. Total: {_allSolvers.Count}. Available: {_availableSolvers.Count}");
				_ = MonitorSolverConnectionAsync(connection);
			}
			else if (initialMessage.Type == MessageType.SubmitGraph)
			{
				var graph = JsonSerializer.Deserialize<Graph>(initialMessage.JsonPayload);
				if (graph == null) { connection.Disconnect(); return; }

				var task = new CalculationTask(connection, graph);

				if (task.IsLargeTask)
				{
					_largeTaskQueue.Enqueue(task);
					Console.WriteLine($"Task {task.TaskId} queued (LARGE). Queue size: {_largeTaskQueue.Count}");
				}
				else
				{
					_smallTaskQueue.Enqueue(task);
					Console.WriteLine($"Task {task.TaskId} queued (SMALL). Queue size: {_smallTaskQueue.Count}");
				}
			}
			else if (initialMessage.Type == MessageType.GetClusterStatus)
			{
				int total = _allSolvers.Count;
				int busy = _solverBusyState.Count(kvp => kvp.Value == true && _allSolvers.ContainsKey(kvp.Key));
				int free = total - busy;
				if (free < 0) free = 0;

				var status = new ClusterStatusInfo
				{
					TotalSolvers = total,
					AvailableSolvers = free,
					BusySolvers = busy,
					CrashedSolvers = _crashedSolversCount
				};

				await connection.WriteMessageAsync(new NetworkMessage
				{
					Type = MessageType.ClusterStatusResponse,
					JsonPayload = JsonSerializer.Serialize(status)
				});

				connection.Disconnect();
			}
		}
		catch
		{
			connection.Disconnect();
		}
	}

	private static async Task MonitorSolverConnectionAsync(ClientConnection connection)
	{
		try
		{
			var socket = connection.Client;
			while (connection.IsConnected)
			{
				if (_solverBusyState.TryGetValue(connection.Id, out var isBusy) && isBusy)
				{
					await Task.Delay(1000);
					continue;
				}

				if (socket.Poll(1000, SelectMode.SelectRead))
				{
					if (socket.Available == 0)
					{
						throw new Exception("Disconnected");
					}

					connection.UpdateHeartbeat();
				}

				await Task.Delay(200);
			}
		}
		catch
		{
			CleanupSolver(connection, "Connection dropped");
		}
	}

	private static void CleanupSolver(ClientConnection solver, string reason)
	{
		if (!solver.IsConnected && !_allSolvers.ContainsKey(solver.Id)) return; 

		Console.WriteLine($"Solver {solver.Id} removing: {reason}");
		solver.Disconnect();

		if (_allSolvers.TryRemove(solver.Id, out _))
		{
			Interlocked.Increment(ref _crashedSolversCount);
			_solverBusyState.TryRemove(solver.Id, out _);
		}
	}

	public static async Task WatchdogLoop()
	{
		while (true)
		{
			var now = DateTime.UtcNow;
			var timeout = TimeSpan.FromSeconds(15.0);

			foreach (var kvp in _allSolvers)
			{
				var solver = kvp.Value;
				if (now - solver.LastHeartbeat > timeout)
				{
					CleanupSolver(solver, "Watchdog timeout");
				}
			}
			await Task.Delay(1000);
		}
	}

	public static async Task TaskDispatcherLoop()
	{
		while (true)
		{
			if (!_isLargeTaskRunning && _largeTaskQueue.TryPeek(out _))
			{
				int required = (int)Math.Ceiling(_allSolvers.Count * LargeTaskSolverQuota);
				if (_availableSolvers.Count >= required && required > 0)
				{
					if (_largeTaskQueue.TryDequeue(out var largeTask))
					{
						_isLargeTaskRunning = true;
						_ = ProcessTaskAsync(largeTask, required);
					}
				}
			}

			if (_smallTaskQueue.TryPeek(out _))
			{
				while (_availableSolvers.Count >= SolversPerSmallTask && _smallTaskQueue.TryDequeue(out var smallTask))
				{
					_ = ProcessTaskAsync(smallTask, SolversPerSmallTask);
				}
			}

			await Task.Delay(500);
		}
	}

	private static async Task ProcessTaskAsync(CalculationTask task, int count)
	{
		List<ClientConnection> assigned = new();
		try
		{
			for (int i = 0; i < count; i++)
			{
				if (_availableSolvers.TryDequeue(out var solver))
				{
					_solverBusyState[solver.Id] = true;
					assigned.Add(solver);
				}
			}

			Console.WriteLine($"Task {task.TaskId}: Allocated {assigned.Count} solvers.");
			var result = await CalculatePageRankDistributedAsync(task.Graph, assigned);

			await task.MauiClient.WriteMessageAsync(new NetworkMessage
			{
				Type = MessageType.CalculationComplete,
				JsonPayload = JsonSerializer.Serialize(result)
			});
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Task {task.TaskId} failed: {ex.Message}");
		}
		finally
		{
			Console.WriteLine($"Task {task.TaskId} cleaning up resources...");
			foreach (var solver in assigned)
			{
				if (solver.IsConnected)
				{
					_solverBusyState[solver.Id] = false; 
					solver.UpdateHeartbeat();
					_availableSolvers.Enqueue(solver);
				}
				else
				{
					CleanupSolver(solver, "Finished task but disconnected");
				}
			}

			if (task.IsLargeTask) _isLargeTaskRunning = false;
			task.MauiClient.Disconnect();
		}
	}

	private static async Task<Dictionary<int, double>> CalculatePageRankDistributedAsync(Graph graph, List<ClientConnection> solvers)
	{
		int nodeCount = graph.Nodes.Count;
		var ranks = graph.Nodes.ToDictionary(n => n.Id, n => 1.0 / nodeCount);
		var links = graph.Nodes.ToDictionary(n => n.Id, n => graph.Edges.Count(e => e.SourceId == n.Id));
		int iteration = 0;

		while (iteration < MaxIterations)
		{
			if (solvers.Count == 0) throw new Exception("All solvers lost.");

			Console.WriteLine($"Iteration {iteration + 1}/{MaxIterations}. Active: {solvers.Count}");

			var prevRanks = new Dictionary<int, double>(ranks);
			var partitions = Partition(graph.Nodes, solvers.Count);
			var tasks = new List<Task<PartialResult>>();
			var solverMap = new Dictionary<Task<PartialResult>, ClientConnection>();

			for (int i = 0; i < solvers.Count; i++)
			{
				var s = solvers[i];
				var part = partitions[i];
				if (!part.Any()) continue;

				var t = new SolverTask
				{
					FullGraph = graph,
					NodeIdsToCalculate = part.Select(n => n.Id).ToList(),
					CurrentRanks = ranks,
					OutgoingLinks = links
				};

				var task = ExecuteTaskOnSolverAsync(s, t);
				tasks.Add(task);
				solverMap[task] = s;
			}

			try
			{
				await Task.WhenAll(tasks);

				var newRanks = new Dictionary<int, double>();
				foreach (var t in tasks)
				{
					foreach (var kvp in t.Result.CalculatedRanks) newRanks[kvp.Key] = kvp.Value;
				}

				foreach (var id in ranks.Keys)
					if (!newRanks.ContainsKey(id)) newRanks[id] = ranks[id];

				double sum = newRanks.Values.Sum();
				if (sum > 0) foreach (var k in newRanks.Keys.ToList()) newRanks[k] /= sum;

				ranks = newRanks;

				double diff = prevRanks.Sum(kvp => Math.Abs(kvp.Value - ranks.GetValueOrDefault(kvp.Key, 0)));
				GC.Collect();

				if (diff < Epsilon) break;

				iteration++;
			}
			catch
			{
				Console.WriteLine("Worker crash detected. Rebalancing...");
				var failed = new List<ClientConnection>();

				foreach (var entry in solverMap)
				{
					if (entry.Key.IsFaulted || !entry.Value.IsConnected)
					{
						failed.Add(entry.Value);
					}
				}

				foreach (var f in failed)
				{
					solvers.Remove(f);
					CleanupSolver(f, "Crash during calculation");
				}

				if (solvers.Count == 0) throw;
			}
		}
		return ranks;
	}

	private static async Task<PartialResult> ExecuteTaskOnSolverAsync(ClientConnection solver, SolverTask task)
	{
		try
		{
			await solver.WriteMessageAsync(new NetworkMessage
			{
				Type = MessageType.AssignTask,
				JsonPayload = JsonSerializer.Serialize(task)
			});

			while (true)
			{
				var msg = await solver.ReadMessageAsync() 
					?? throw new IOException("Disconnected");
				if (msg.Type == MessageType.Heartbeat || msg.Type == MessageType.Ping)
				{
					solver.UpdateHeartbeat();
					continue;
				}

				if (msg.Type == MessageType.PartialResult)
				{
					solver.UpdateHeartbeat();
					return JsonSerializer.Deserialize<PartialResult>(msg.JsonPayload) ?? new PartialResult();
				}
			}
		}
		catch
		{
			throw;
		}
	}

	private static List<List<T>> Partition<T>(IEnumerable<T> source, int size)
	{
		var partitions = new List<List<T>>();
		for (int i = 0; i < size; i++) partitions.Add(new List<T>());
		int index = 0;
		foreach (var item in source) partitions[index++ % size].Add(item);
		return partitions;
	}
}