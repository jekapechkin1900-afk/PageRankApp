namespace PageRankApp.Shared.Models;

public class Node
{
	public int Id { get; set; }
	public double Rank { get; set; } = 1.0;
	public double X { get; set; }
	public double Y { get; set; }
}
