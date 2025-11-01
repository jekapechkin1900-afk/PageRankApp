using System.Net;
using System.Net.Sockets;
using PageRankApp.Scheduler;

var listener = new TcpListener(IPAddress.Any, 8888);
listener.Start();
Console.WriteLine("Scheduler is running on port 8888...");
Console.WriteLine("Waiting for connections from MAUI client and Solvers...");

_ = Task.Run(Scheduler.TaskDispatcherLoop);

while (true)
{
	try
	{
		var tcpClient = await listener.AcceptTcpClientAsync();
		_ = Scheduler.HandleNewConnectionAsync(new ClientConnection(tcpClient));
	}
	catch (Exception ex)
	{
		Console.WriteLine($"Error accepting connection: {ex.Message}");
	}
}
