using PageRankApp.Shared.Network;

namespace PageRankApp.Solver;

public static class Solver
{
	public static PartialResult CalculatePartialPageRank(SolverTask task)
	{
		const double dampingFactor = 0.85;
		var nodeCount = task.FullGraph.Nodes.Count;
		var newRanks = new Dictionary<int, double>();

		var incomingLinks = task.FullGraph.Edges
			.GroupBy(e => e.TargetId)
			.ToDictionary(g => g.Key, g => g.Select(e => e.SourceId).ToList());

		foreach (var nodeId in task.NodeIdsToCalculate)
		{
			double rankSum = 0;

			if (incomingLinks.TryGetValue(nodeId, out var incomingNodeIds))
			{
				foreach (var incomingNodeId in incomingNodeIds)
				{
					int outgoingCount = task.OutgoingLinks.GetValueOrDefault(incomingNodeId, 0);
					if (outgoingCount > 0)
					{
						rankSum += task.CurrentRanks[incomingNodeId] / outgoingCount;
					}
				}
			}

			var newRank = (1 - dampingFactor) / nodeCount + dampingFactor * rankSum;
			newRanks[nodeId] = newRank;
		}

		return new PartialResult { CalculatedRanks = newRanks };
	}
}
