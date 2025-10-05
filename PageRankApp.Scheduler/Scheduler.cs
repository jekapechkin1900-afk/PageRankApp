using System.Collections.Concurrent;
using System.Text.Json;
using PageRankApp.Shared.Models;
using PageRankApp.Shared.Network;

namespace PageRankApp.Scheduler;

public static class Scheduler
{
	// Потокобезопасные коллекции для хранения подключений
	private static readonly ConcurrentDictionary<Guid, ClientConnection> _solvers = new();
	private static ClientConnection? _mauiClient;

	// Параметры алгоритма PageRank
	private const double DampingFactor = 0.85;
	private const int MaxIterations = 30;
	private const double Epsilon = 1e-6; // Порог для определения сходимости

	public static async Task HandleConnectionAsync(ClientConnection connection)
	{
		Console.WriteLine($"Client connected from {connection.RemoteEndPoint}. Awaiting identification...");

		try
		{
			// Первое сообщение от клиента должно быть для его идентификации
			var initialMessage = await connection.ReadMessageAsync();
			if (initialMessage == null) return; // Клиент отсоединился

			// Определяем, кто подключился: MAUI клиент или решатель
			if (initialMessage.Type == MessageType.RegisterSolver)
			{
				_solvers.TryAdd(connection.Id, connection);
				Console.WriteLine($"Solver registered. Total solvers: {_solvers.Count}. ID: {connection.Id}");
			}
			else if (initialMessage.Type == MessageType.SubmitGraph)
			{
				// Если это MAUI клиент, сохраняем его и начинаем обработку графа
				_mauiClient = connection;
				Console.WriteLine("MAUI client connected and submitted a graph.");
				await ProcessGraphRequest(initialMessage);
			}
			else
			{
				Console.WriteLine("Unknown client type. Disconnecting.");
				connection.Disconnect();
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error during connection handling for {connection.Id}: {ex.Message}");
			connection.Disconnect();
		}
	}

	private static async Task ProcessGraphRequest(NetworkMessage graphMessage)
	{
		if (_mauiClient == null)
		{
			Console.WriteLine("Cannot process graph: MAUI client is not connected.");
			return;
		}

		if (_solvers.IsEmpty)
		{
			Console.WriteLine("No solvers available to perform calculation.");
			// Можно отправить сообщение об ошибке клиенту
			return;
		}

		try
		{
			var graph = JsonSerializer.Deserialize<Graph>(graphMessage.JsonPayload);
			if (graph == null || graph.Nodes.Count == 0)
			{
				Console.WriteLine("Received empty or invalid graph.");
				return;
			}

			Console.WriteLine($"Graph received with {graph.Nodes.Count} nodes and {graph.Edges.Count} edges. Starting calculation...");

			var finalRanks = await CalculatePageRankDistributedAsync(graph);

			Console.WriteLine("Calculation complete. Sending results back to MAUI client.");

			var resultMessage = new NetworkMessage
			{
				Type = MessageType.CalculationComplete,
				JsonPayload = JsonSerializer.Serialize(finalRanks)
			};

			await _mauiClient.WriteMessageAsync(resultMessage);
		}
		catch (JsonException jsonEx)
		{
			Console.WriteLine($"Failed to deserialize graph: {jsonEx.Message}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"An error occurred during PageRank calculation: {ex.Message}");
		}
		finally 
		{
			Console.WriteLine("Disconnecting MAUI client.");
			_mauiClient.Disconnect();
			_mauiClient = null;
		}
	}

	private static async Task<Dictionary<int, double>> CalculatePageRankDistributedAsync(Graph graph)
	{
		int nodeCount = graph.Nodes.Count;
		var ranks = graph.Nodes.ToDictionary(n => n.Id, n => 1.0 / nodeCount);

		var outgoingLinks = graph.Nodes.ToDictionary(
			n => n.Id,
			n => graph.Edges.Count(e => e.SourceId == n.Id)
		);

		for (int i = 0; i < MaxIterations; i++)
		{
			Console.WriteLine($"--- Iteration {i + 1} ---");

			var previousRanks = new Dictionary<int, double>(ranks);
			var tasks = new List<Task<PartialResult>>();
			var availableSolvers = _solvers.Values.ToList();
			var nodePartitions = Partition(graph.Nodes, availableSolvers.Count);

			for (int j = 0; j < availableSolvers.Count; j++)
			{
				var solver = availableSolvers[j];
				var nodePartition = nodePartitions[j];

				var solverTask = new SolverTask
				{
					FullGraph = graph,
					NodeIdsToCalculate = [.. nodePartition.Select(n => n.Id)],
					CurrentRanks = ranks,
					OutgoingLinks = outgoingLinks
				};

				tasks.Add(ExecuteTaskOnSolverAsync(solver, solverTask));
			}

			var partialResults = await Task.WhenAll(tasks);

			var newRanks = new Dictionary<int, double>();
			foreach (var result in partialResults)
			{
				foreach (var rankEntry in result.CalculatedRanks)
				{
					newRanks[rankEntry.Key] = rankEntry.Value;
				}
			}

			double rankSum = newRanks.Values.Sum();
			foreach (var key in newRanks.Keys)
			{
				newRanks[key] /= rankSum;
			}

			ranks = newRanks;

			double diff = previousRanks.Sum(kvp => Math.Abs(kvp.Value - ranks[kvp.Key]));
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
			throw new InvalidOperationException($"Solver {solver.Id} returned an unexpected message type.");
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
