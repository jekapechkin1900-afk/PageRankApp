namespace PageRankApp.Tests.UnitTests;

public class AlgorithmTests
{
	[Fact]
	public void PageRank_Calculation_ManualVerification()
	{
		// (1-d)/N + d * Sum(PR(in)/C(in))

		// Arrange
		double damping = 0.85;
		int nodeCount = 3;
		double currentRankNodeA = 0.5;
		int outgoingLinksNodeA = 2; 

		// Act
		double contribution = currentRankNodeA / outgoingLinksNodeA;
		double resultRankPart = damping * contribution;

		// Assert
		double expectedContribution = 0.25; 
		double expectedPart = 0.85 * 0.25; 

		Assert.Equal(expectedContribution, contribution);
		Assert.Equal(expectedPart, resultRankPart);
	}

	private List<List<T>> SimulatePartition<T>(IEnumerable<T> source, int size)
	{
		var partitions = new List<List<T>>();
		for (int i = 0; i < size; i++) partitions.Add(new List<T>());
		int index = 0;
		foreach (var item in source) partitions[index++ % size].Add(item);
		return partitions;
	}

	[Fact]
	public void Partitioning_EqualDivison_ShouldBeBalanced()
	{
		var items = Enumerable.Range(0, 10).ToList();
		var result = SimulatePartition(items, 2);

		Assert.Equal(2, result.Count);
		Assert.Equal(5, result[0].Count); 
		Assert.Equal(5, result[1].Count); 
	}

	[Fact]
	public void Partitioning_UnevenDivision_ShouldDistributeRemainder()
	{
		var items = Enumerable.Range(0, 10).ToList();
		var result = SimulatePartition(items, 3);

		Assert.Equal(3, result.Count);
		Assert.Equal(4, result[0].Count);
		Assert.Equal(3, result[1].Count);
		Assert.Equal(3, result[2].Count);
	}

	[Fact]
	public void Partitioning_MoreBucketsThanItems_ShouldHaveEmptyBuckets()
	{
		var items = Enumerable.Range(0, 2).ToList(); 
		var result = SimulatePartition(items, 5);  

		Assert.Equal(5, result.Count);
		Assert.Single(result[0]);
		Assert.Single(result[1]); 
		Assert.Empty(result[2]);  
		Assert.Empty(result[3]); 
		Assert.Empty(result[4]); 
	}

	[Fact]
	public void Partitioning_ZeroItems_ShouldReturnEmptyBuckets()
	{
		var items = new List<int>();
		var result = SimulatePartition(items, 3);

		Assert.Equal(3, result.Count);
		Assert.All(result, list => Assert.Empty(list));
	}
}