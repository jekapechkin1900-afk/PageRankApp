using System.Net;
using System.Net.Sockets;
using PageRankApp.Scheduler;

var listener = new TcpListener(IPAddress.Any, 8888);
listener.Start();
Console.WriteLine("Scheduler is running on port 8888...");
Console.WriteLine("Waiting for connections from MAUI client and Solvers...");

while (true)
{
	try
	{
		var tcpClient = await listener.AcceptTcpClientAsync();
		var connection = new ClientConnection(tcpClient);
		_ = Scheduler.HandleConnectionAsync(connection);
	}
	catch (Exception ex)
	{
		Console.WriteLine($"Error accepting connection: {ex.Message}");
	}
}
