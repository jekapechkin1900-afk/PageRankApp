using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PageRankApp.Shared.Network;

public static class NetworkHelper
{
	public static async Task WriteMessageAsync(Stream stream, NetworkMessage message)
	{
		var jsonString = JsonSerializer.Serialize(message);
		var messageBytes = Encoding.UTF8.GetBytes(jsonString);
		var lengthPrefix = BitConverter.GetBytes(messageBytes.Length);

		await stream.WriteAsync(lengthPrefix.AsMemory(0, 4));
		await stream.WriteAsync(messageBytes);
		await stream.FlushAsync();
	}

	public static async Task<NetworkMessage?> ReadMessageAsync(Stream stream)
	{
		var lengthBuffer = new byte[4];

		int totalBytesRead = 0;
		while (totalBytesRead < 4)
		{
			int bytesRead = await stream.ReadAsync(lengthBuffer.AsMemory(totalBytesRead, 4 - totalBytesRead));
			if (bytesRead == 0) return null; 
			totalBytesRead += bytesRead;
		}

		var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
		if (messageLength <= 0) return null; 

		var messageBuffer = new byte[messageLength];

		totalBytesRead = 0;
		while (totalBytesRead < messageLength)
		{
			int bytesRead = await stream.ReadAsync(messageBuffer, totalBytesRead, messageLength - totalBytesRead);
			if (bytesRead == 0) return null;
			totalBytesRead += bytesRead;
		}

		var jsonString = Encoding.UTF8.GetString(messageBuffer);

		try
		{
			return JsonSerializer.Deserialize<NetworkMessage>(jsonString);
		}
		catch
		{
			return null; 
		}
	}
}
