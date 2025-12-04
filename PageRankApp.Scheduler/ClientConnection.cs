using System.Net.Sockets;
using PageRankApp.Shared.Network;

namespace PageRankApp.Scheduler;

public class ClientConnection(TcpClient tcpClient)
{
	public Guid Id { get; } = Guid.NewGuid();
	private readonly TcpClient _tcpClient = tcpClient;
	private readonly NetworkStream _stream = tcpClient.GetStream();
	public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
	public bool IsConnected => _tcpClient.Connected;
	public Socket Client => _tcpClient.Client;
	public string RemoteEndPoint => _tcpClient.Client.RemoteEndPoint?.ToString() ?? "N/A";

	public void UpdateHeartbeat() => LastHeartbeat = DateTime.UtcNow;
	
	public async Task WriteMessageAsync(NetworkMessage message) => 
		await NetworkHelper.WriteMessageAsync(_stream, message);

	public async Task<NetworkMessage?> ReadMessageAsync() => 
		await NetworkHelper.ReadMessageAsync(_stream);

	public void Disconnect()
	{
		_stream.Close();
		_tcpClient.Close();
	}
}
