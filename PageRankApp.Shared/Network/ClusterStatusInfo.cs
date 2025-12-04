namespace PageRankApp.Shared.Network;

public class ClusterStatusInfo
{
	public int TotalSolvers { get; set; }
	public int AvailableSolvers { get; set; }
	public int BusySolvers { get; set; }
	public int CrashedSolvers { get; set; }
}
