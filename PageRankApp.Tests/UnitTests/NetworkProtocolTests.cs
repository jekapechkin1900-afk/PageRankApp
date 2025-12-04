using System.Text;
using System.Text.Json;
using PageRankApp.Shared.Network;
using Xunit;

namespace PageRankApp.Tests.UnitTests;

public class NetworkProtocolTests
{
	[Fact]
	public async Task WriteAndReadMessage_ShouldPreserveData()
	{
		// Arrange
		using var memoryStream = new MemoryStream();
		var originalMessage = new NetworkMessage
		{
			Type = MessageType.SubmitGraph,
			JsonPayload = "{\"test\": 123}"
		};

		// Act 
		await NetworkHelper.WriteMessageAsync(memoryStream, originalMessage);

		memoryStream.Position = 0;

		// Act 
		var readMessage = await NetworkHelper.ReadMessageAsync(memoryStream);

		// Assert
		Assert.NotNull(readMessage);
		Assert.Equal(originalMessage.Type, readMessage.Type);
		Assert.Equal(originalMessage.JsonPayload, readMessage.JsonPayload);
	}

	[Fact]
	public async Task ClusterStatusInfo_Serialization_ShouldWork()
	{
		var info = new ClusterStatusInfo
		{
			TotalSolvers = 10,
			BusySolvers = 5,
			AvailableSolvers = 5,
			CrashedSolvers = 0
		};

		var json = JsonSerializer.Serialize(info);
		var msg = new NetworkMessage { Type = MessageType.ClusterStatusResponse, JsonPayload = json };

		var deserializedMsg = JsonSerializer.Deserialize<ClusterStatusInfo>(msg.JsonPayload);

		Assert.Equal(10, deserializedMsg.TotalSolvers);
		Assert.Equal(5, deserializedMsg.BusySolvers);
	}

	[Theory]
	[InlineData(MessageType.Heartbeat)]
	[InlineData(MessageType.Ping)]
	[InlineData(MessageType.GetClusterStatus)]
	public void NetworkMessage_EnumHandling_ShouldSupportNewTypes(MessageType type)
	{
		// Проверка что енум сериализуется корректно
		var msg = new NetworkMessage { Type = type };
		var json = JsonSerializer.Serialize(msg);
		var restored = JsonSerializer.Deserialize<NetworkMessage>(json);

		Assert.Equal(type, restored.Type);
	}
}