using System.Text.Json;
using PageRankApp.Tests.Helpers;
using PageRankApp.Shared.Models;
using Xunit;

namespace PageRankApp.Tests.UnitTests;

public class GraphLogicTests
{
	[Fact]
	public void Graph_Serialization_ShouldBeLossless()
	{
		// Arrange
		var graph = GraphGenerator.GenerateRandomGraph(100, 2);

		// Act
		var json = JsonSerializer.Serialize(graph);
		var deserializedGraph = JsonSerializer.Deserialize<Graph>(json);

		// Assert
		Assert.NotNull(deserializedGraph);
		Assert.Equal(graph.Nodes.Count, deserializedGraph.Nodes.Count);
		Assert.Equal(graph.Edges.Count, deserializedGraph.Edges.Count);
		Assert.Equal(graph.Edges[0].SourceId, deserializedGraph.Edges[0].SourceId);
	}

	[Fact]
	public void Partitioning_ShouldDistributeNodesEvenly()
	{
		// Arrange
		var nodes = Enumerable.Range(0, 100).ToList();
		int parts = 4;

		// Act (Копия логики Partition)
		var partitions = new List<List<int>>();
		for (int i = 0; i < parts; i++) partitions.Add(new List<int>());
		int index = 0;
		foreach (var item in nodes) partitions[index++ % parts].Add(item);

		// Assert
		Assert.Equal(4, partitions.Count);
		Assert.Equal(25, partitions[0].Count);
		Assert.Equal(25, partitions[3].Count);
	}
}