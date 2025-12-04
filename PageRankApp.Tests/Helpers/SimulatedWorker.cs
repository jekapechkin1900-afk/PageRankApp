using System.Net.Sockets;
using System.Text.Json;
using PageRankApp.Shared.Network;

namespace PageRankApp.Tests.Helpers;

public class SimulatedWorker : IDisposable
{
	private TcpClient _client;
	public bool IsConnected => _client != null && _client.Connected;

	public async Task ConnectAsync(string host, int port)
	{
		_client = new TcpClient();
		await _client.ConnectAsync(host, port);
		var stream = _client.GetStream();
		await NetworkHelper.WriteMessageAsync(stream, new NetworkMessage { Type = MessageType.RegisterSolver });
	}

	public async Task RunCycleAsync(CancellationToken token)
	{
		var stream = _client.GetStream();
		while (!token.IsCancellationRequested && _client.Connected)
		{
			try
			{
				var msgTask = NetworkHelper.ReadMessageAsync(stream);
				var completed = await Task.WhenAny(msgTask, Task.Delay(-1, token));
				if (completed != msgTask) break;

				var msg = await msgTask;
				if (msg == null) break;

				if (msg.Type == MessageType.AssignTask)
				{
					var task = JsonSerializer.Deserialize<SolverTask>(msg.JsonPayload);
					var result = new PartialResult { CalculatedRanks = new Dictionary<int, double>() };
					foreach (var id in task.NodeIdsToCalculate) result.CalculatedRanks[id] = 1.0;

					await NetworkHelper.WriteMessageAsync(stream, new NetworkMessage
					{
						Type = MessageType.PartialResult,
						JsonPayload = JsonSerializer.Serialize(result)
					});
				}
			}
			catch { break; }
		}
	}

	public void Dispose() => _client?.Dispose();
}