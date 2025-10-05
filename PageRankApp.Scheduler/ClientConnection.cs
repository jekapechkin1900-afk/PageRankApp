using System.Net.Sockets;
using PageRankApp.Shared.Network;

namespace PageRankApp.Scheduler;

public class ClientConnection
{
	public Guid Id { get; } = Guid.NewGuid();
	private readonly TcpClient _tcpClient;
	private readonly NetworkStream _stream;

	public string RemoteEndPoint => _tcpClient.Client.RemoteEndPoint?.ToString() ?? "N/A";

	public ClientConnection(TcpClient tcpClient)
	{
		_tcpClient = tcpClient;
		_stream = tcpClient.GetStream();
	}

	public async Task WriteMessageAsync(NetworkMessage message)
	{
		await NetworkHelper.WriteMessageAsync(_stream, message);
	}

	public async Task<NetworkMessage?> ReadMessageAsync()
	{
		return await NetworkHelper.ReadMessageAsync(_stream);
	}

	public void Disconnect()
	{
		_stream.Close();
		_tcpClient.Close();
	}
}
