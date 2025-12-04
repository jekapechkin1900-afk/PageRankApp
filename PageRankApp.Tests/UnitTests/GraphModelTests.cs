using System.Text.Json;
using PageRankApp.Shared.Models;

namespace PageRankApp.Tests.UnitTests;

public class GraphModelTests
{
	[Fact]
	public void Node_Defaults_ShouldBeCorrect()
	{
		var node = new Node();
		Assert.Equal(1.0, node.Rank); 
		Assert.Equal(0, node.Id);
	}

	[Fact]
	public void Graph_Initialization_ShouldCreateEmptyCollections()
	{
		var graph = new Graph();
		Assert.NotNull(graph.Nodes);
		Assert.NotNull(graph.Edges);
		Assert.Empty(graph.Nodes);
		Assert.Empty(graph.Edges);
	}

	[Fact]
	public void Graph_Serialization_RoundTrip_ShouldRetainData()
	{
		// Arrange
		var graph = new Graph();
		graph.Nodes.Add(new Node { Id = 1, X = 10, Y = 20 });
		graph.Nodes.Add(new Node { Id = 2, X = 30, Y = 40 });
		graph.Edges.Add(new Edge { SourceId = 1, TargetId = 2 });

		// Act
		var json = JsonSerializer.Serialize(graph);
		var deserialized = JsonSerializer.Deserialize<Graph>(json);

		// Assert
		Assert.NotNull(deserialized);
		Assert.Equal(2, deserialized.Nodes.Count);
		Assert.Equal(1, deserialized.Edges.Count);
		Assert.Equal(10, deserialized.Nodes[0].X); 
		Assert.Equal(1, deserialized.Edges[0].SourceId);
		Assert.Equal(2, deserialized.Edges[0].TargetId);
	}

	[Fact]
	public void Graph_WithThousandsOfNodes_SerializationPerfCheck()
	{
		var graph = new Graph();
		for (int i = 0; i < 5000; i++) graph.Nodes.Add(new Node { Id = i });

		var json = JsonSerializer.Serialize(graph);
		Assert.True(json.Length > 0);

		var restored = JsonSerializer.Deserialize<Graph>(json);
		Assert.Equal(5000, restored.Nodes.Count);
	}
}