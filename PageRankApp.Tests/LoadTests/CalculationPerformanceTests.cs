using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using PageRankApp.Shared.Models;
using PageRankApp.Shared.Network;
using PageRankApp.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace PageRankApp.Tests.LoadTests;

[Collection("SequentialPort8888")]
public class CalculationPerformanceTests
{
	private readonly ITestOutputHelper _output;

	public CalculationPerformanceTests(ITestOutputHelper output)
	{
		_output = output;
	}

	public static IEnumerable<object[]> GetLoadScenarios()
	{
		yield return new object[] { 5, 1000 };
		yield return new object[] { 5, 2000 };
		yield return new object[] { 5, 3000 };
		yield return new object[] { 5, 4000 };
		yield return new object[] { 5, 5000 };

		yield return new object[] { 10, 2000 };
		yield return new object[] { 10, 5000 };
		yield return new object[] { 20, 7000 };
		yield return new object[] { 20, 8000 };
		yield return new object[] { 20, 9000 };
		yield return new object[] { 20, 10000 };

		yield return new object[] { 30, 5000 };
		yield return new object[] { 30, 10000 };
		yield return new object[] { 30, 12000 };
		yield return new object[] { 30, 15000 };
		yield return new object[] { 30, 20000 };

		yield return new object[] { 50, 20000 };
	}

	[Theory]
	[MemberData(nameof(GetLoadScenarios))]
	public async Task Cluster_ShouldCalculateGraph_Correctly(int workerCount, int nodeCount)
	{
		// 1. Arrange
		_output.WriteLine($"Scenario: {workerCount} Workers, {nodeCount} Nodes.");

		using var runner = new ProcessRunner();
		runner.StartScheduler();

		var workers = new List<SimulatedWorker>();
		var cts = new CancellationTokenSource();

		try
		{

			for (int i = 0; i < workerCount; i++)
			{
				var w = new SimulatedWorker();
				await w.ConnectAsync("127.0.0.1", 8888);
				_ = w.RunCycleAsync(cts.Token);
				workers.Add(w);
			}

			await Task.Delay(1000);


			var graph = GraphGenerator.GenerateRandomGraph(nodeCount, 3); 
			_output.WriteLine("Graph generated.");

			// 2. Act: Отправка задачи
			using var client = new TcpClient();
			await client.ConnectAsync("127.0.0.1", 8888);
			var stream = client.GetStream();

			var submitMsg = new NetworkMessage
			{
				Type = MessageType.SubmitGraph,
				JsonPayload = JsonSerializer.Serialize(graph)
			};

			var sw = Stopwatch.StartNew();
			await NetworkHelper.WriteMessageAsync(stream, submitMsg);

			// 3. Assert: Ожидание результата
			int timeoutMs = 5000 + (nodeCount * 5);

			var readTask = NetworkHelper.ReadMessageAsync(stream);
			var timeoutTask = Task.Delay(timeoutMs);

			var completedTask = await Task.WhenAny(readTask, timeoutTask);

			if (completedTask == timeoutTask)
			{
				throw new TimeoutException($"Calculation timed out after {timeoutMs}ms. System overloaded.");
			}

			var resultMsg = await readTask;
			sw.Stop();

			_output.WriteLine($"Calculation finished in {sw.ElapsedMilliseconds}ms.");

			Assert.NotNull(resultMsg);
			Assert.Equal(MessageType.CalculationComplete, resultMsg.Type);

			var results = JsonSerializer.Deserialize<Dictionary<int, double>>(resultMsg.JsonPayload);

			Assert.NotNull(results);
			Assert.Equal(nodeCount, results.Count);

			double sum = results.Values.Sum();
			Assert.True(Math.Abs(sum - 1.0) < 0.1, $"Rank sum should be ~1.0, but was {sum}");
		}
		finally
		{
			cts.Cancel();
			foreach (var w in workers) w.Dispose();
		}
	}
}