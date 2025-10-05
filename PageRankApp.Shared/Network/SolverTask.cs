using PageRankApp.Shared.Models;

namespace PageRankApp.Shared.Network;

public class SolverTask
{
	public List<int> NodeIdsToCalculate { get; set; } = [];
	public Graph? FullGraph { get; set; }
	public Dictionary<int, double> CurrentRanks { get; set; } = [];
	public Dictionary<int, int> OutgoingLinks { get; set; } = [];
}
