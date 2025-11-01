using PageRankApp.Shared.Models;

namespace PageRankApp.Scheduler.Models;

internal class CalculationTask(ClientConnection mauiClient, Graph graph)
{
	public Guid TaskId { get; } = Guid.NewGuid();
	public ClientConnection MauiClient { get; } = mauiClient;
	public Graph Graph { get; } = graph;
	public bool IsLargeTask => Graph.Nodes.Count > Scheduler.LargeGraphNodeThreshold;
}
