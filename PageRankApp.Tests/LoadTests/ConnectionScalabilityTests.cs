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
public class ConnectionScalabilityTests(ITestOutputHelper output)
{
	private readonly ITestOutputHelper _output = output;

	[Theory]
	[InlineData(5)]
	[InlineData(10)]
	[InlineData(20)]
	[InlineData(30)]
	[InlineData(50)]
	[InlineData(100)]
	public async Task Scheduler_ShouldAccept_N_Workers_WithoutCrash(int workerCount)
	{
		// 1. Arrange
		using var runner = new ProcessRunner();
		runner.StartScheduler();

		var clients = new List<SimulatedWorker>();

		_output.WriteLine($"Starting stress test with {workerCount} workers...");
		var stopwatch = Stopwatch.StartNew();

		try
		{
			// 2. Act
			for (int i = 0; i < workerCount; i++)
			{
				var worker = new SimulatedWorker();
				clients.Add(worker);

				await Task.Delay(Random.Shared.Next(20, 50));

				await worker.ConnectAsync("127.0.0.1", 8888);
			}

			stopwatch.Stop();
			_output.WriteLine($"All connections initiated in {stopwatch.ElapsedMilliseconds} ms.");

			await Task.Delay(1000);

			// 3. Assert
			using var statusClient = new TcpClient();
			await statusClient.ConnectAsync("127.0.0.1", 8888);
			var stream = statusClient.GetStream();
			await NetworkHelper.WriteMessageAsync(stream, new NetworkMessage { Type = MessageType.GetClusterStatus });

			var response = await NetworkHelper.ReadMessageAsync(stream);
			var status = JsonSerializer.Deserialize<ClusterStatusInfo>(response.JsonPayload);

			_output.WriteLine($"Server reports: {status.TotalSolvers} total, {status.AvailableSolvers} free.");

			Assert.NotNull(status);
			Assert.True(status.TotalSolvers >= workerCount, $"Expected at least {workerCount} solvers, but found {status.TotalSolvers}");

			Assert.All(clients, c => Assert.True(c.IsConnected));
		}
		finally
		{
			foreach (var c in clients) c.Dispose();
		}
	}
}