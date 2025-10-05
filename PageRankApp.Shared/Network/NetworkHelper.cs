using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PageRankApp.Shared.Network;

public static class NetworkHelper
{
	public static async Task WriteMessageAsync(NetworkStream stream, NetworkMessage message)
	{
		var jsonString = JsonSerializer.Serialize(message);
		var messageBytes = Encoding.UTF8.GetBytes(jsonString);
		var lengthPrefix = BitConverter.GetBytes(messageBytes.Length);

		await stream.WriteAsync(lengthPrefix.AsMemory(0, 4));
		await stream.WriteAsync(messageBytes);
	}

	public static async Task<NetworkMessage?> ReadMessageAsync(NetworkStream stream)
	{
		var lengthBuffer = new byte[4];
		int bytesRead = await stream.ReadAsync(lengthBuffer.AsMemory(0, 4));
		if (bytesRead < 4) return null;

		var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
		var messageBuffer = new byte[messageLength];

		int totalBytesRead = 0;
		while (totalBytesRead < messageLength)
		{
			bytesRead = await stream.ReadAsync(messageBuffer.AsMemory(totalBytesRead, messageLength - totalBytesRead));
			if (bytesRead == 0) return null; 
			totalBytesRead += bytesRead;
		}

		var jsonString = Encoding.UTF8.GetString(messageBuffer);
		return JsonSerializer.Deserialize<NetworkMessage>(jsonString);
	}
}
