using System.Net.Sockets;
using System.Text.Json;
using PageRankApp.Shared.Network;
using PageRankApp.Solver;

Console.WriteLine("PageRank Solver started.");
const string SchedulerIp = "127.0.0.1";
const int SchedulerPort = 8888;

while (true)
{
	try
	{
		using var client = new TcpClient();
		Console.WriteLine($"Attempting to connect to scheduler at {SchedulerIp}:{SchedulerPort}...");
		await client.ConnectAsync(SchedulerIp, SchedulerPort);

		var stream = client.GetStream();
		Console.WriteLine("Successfully connected to scheduler. Registering...");

		var registerMessage = new NetworkMessage { Type = MessageType.RegisterSolver, JsonPayload = "" };
		await NetworkHelper.WriteMessageAsync(stream, registerMessage);
		Console.WriteLine("Registered as a solver. Waiting for tasks...");

		while (client.Connected)
		{
			var taskMessage = await NetworkHelper.ReadMessageAsync(stream);
			if (taskMessage == null)
			{
				break;
			}

			if (taskMessage.Type == MessageType.AssignTask)
			{
				Console.WriteLine("Received a new task. Processing...");
				var solverTask = JsonSerializer.Deserialize<SolverTask>(taskMessage.JsonPayload);

				if (solverTask != null)
				{
					var partialResult = Solver.CalculatePartialPageRank(solverTask);

					var resultMessage = new NetworkMessage
					{
						Type = MessageType.PartialResult,
						JsonPayload = JsonSerializer.Serialize(partialResult)
					};

					await NetworkHelper.WriteMessageAsync(stream, resultMessage);
					Console.WriteLine($"Task processed. Sent partial result for {partialResult.CalculatedRanks.Count} nodes.");
				}
			}
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"An error occurred: {ex.Message}");
	}

	Console.WriteLine("Connection lost. Will try to reconnect in 5 seconds...");
	await Task.Delay(TimeSpan.FromSeconds(5));
}
