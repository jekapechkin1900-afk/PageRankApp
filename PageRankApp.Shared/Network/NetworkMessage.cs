namespace PageRankApp.Shared.Network;

public class NetworkMessage
{
	public MessageType Type { get; set; }
	public string? JsonPayload { get; set; }
}
