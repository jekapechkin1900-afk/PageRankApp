using PageRankApp.Shared.Models;

namespace PageRankApp.Tests.Helpers;

public static class GraphGenerator
{
    public static Graph GenerateRandomGraph(int nodesCount, int density = 5)
    {
        var graph = new Graph { Nodes = new List<Node>(), Edges = new List<Edge>() };
        var rnd = new Random();

        for (int i = 0; i < nodesCount; i++)
        {
            graph.Nodes.Add(new Node { Id = i });
        }

        for (int i = 0; i < nodesCount - 1; i++)
        {
            graph.Edges.Add(new Edge { SourceId = i, TargetId = i + 1 });
        }
        graph.Edges.Add(new Edge { SourceId = nodesCount - 1, TargetId = 0 }); 

        for (int i = 0; i < nodesCount; i++)
        {
            for (int j = 0; j < density; j++)
            {
                int target = rnd.Next(0, nodesCount);
                if (i != target)
                {
                    graph.Edges.Add(new Edge { SourceId = i, TargetId = target });
                }
            }
        }

        return graph;
    }
}