namespace PageRankApp.Shared.Network;

public enum MessageType
{
	SubmitGraph,         
	CalculationComplete,  
	RegisterSolver,      
	AssignTask,         
	PartialResult,
	GetClusterStatus,
	ClusterStatusResponse,
	Heartbeat,
	Ping
}

