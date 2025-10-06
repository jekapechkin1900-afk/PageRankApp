using System.Collections.ObjectModel;
using PageRankApp.Shared.Models;

namespace PageRankApp.Maui.Drawable;

public class GraphDrawable : IDrawable
{
	private const int LargeGraphThreshold = 500;
	public ObservableCollection<Node> Nodes { get; set; } = [];
	public ObservableCollection<Edge> Edges { get; set; } = [];

	public Node? SelectedNode { get; set; } 

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		if (Nodes == null || Edges == null) return;

		bool isLarge = Nodes.Count > LargeGraphThreshold;

		if (isLarge)
		{
			canvas.StrokeColor = Colors.LightGray.WithAlpha(0.2f);
			canvas.StrokeSize = 1;
			foreach (var edge in Edges)
			{
				var source = Nodes.FirstOrDefault(n => n.Id == edge.SourceId);
				var target = Nodes.FirstOrDefault(n => n.Id == edge.TargetId);
				if (source != null && target != null)
				{
					canvas.DrawLine((float)source.X, (float)source.Y, (float)target.X, (float)target.Y);
				}
			}

			foreach (var node in Nodes)
			{
				canvas.FillColor = GetColorForRank(node.Rank);
				canvas.FillRectangle((float)node.X, (float)node.Y, 2, 2);
			}
		}
		else
		{
			canvas.StrokeColor = Colors.LightGray;
			canvas.StrokeSize = 2;
			foreach (var edge in Edges)
			{
				var source = Nodes.FirstOrDefault(n => n.Id == edge.SourceId);
				var target = Nodes.FirstOrDefault(n => n.Id == edge.TargetId);
				if (source != null && target != null)
				{
					DrawArrow(canvas, (float)source.X, (float)source.Y, (float)target.X, (float)target.Y);
				}
			}

			foreach (var node in Nodes)
			{
				float nodeSize = 35;
				var rect = new RectF((float)node.X - nodeSize / 2, (float)node.Y - nodeSize / 2, nodeSize, nodeSize);

				if (node == SelectedNode)
				{
					canvas.FillColor = Colors.OrangeRed;
				}
				else
				{
					canvas.FillColor = GetColorForRank(node.Rank);
				}

				canvas.FillEllipse(rect);

				canvas.StrokeColor = Colors.DarkSlateGray;
				canvas.StrokeSize = 2;
				canvas.DrawEllipse(rect);

				canvas.FontColor = Colors.Black;
				canvas.FontSize = 12;
				canvas.DrawString(node.Id.ToString(), rect, HorizontalAlignment.Center, VerticalAlignment.Center);

				canvas.FontSize = 10;
				canvas.FontColor = Colors.DarkBlue;
				canvas.DrawString($"{node.Rank:F4}", (float)node.X, (float)node.Y + nodeSize / 2 + 5, HorizontalAlignment.Center);
			}

		}
	}

	private void DrawArrow(ICanvas canvas, float x1, float y1, float x2, float y2)
	{
		canvas.DrawLine(x1, y1, x2, y2);
		float angle = (float)Math.Atan2(y2 - y1, x2 - x1);
		float arrowLength = 10;
		float arrowAngle = 0.4f;

		float x3 = x2 - arrowLength * (float)Math.Cos(angle - arrowAngle);
		float y3 = y2 - arrowLength * (float)Math.Sin(angle - arrowAngle);
		float x4 = x2 - arrowLength * (float)Math.Cos(angle + arrowAngle);
		float y4 = y2 - arrowLength * (float)Math.Sin(angle + arrowAngle);

		canvas.DrawLine(x2, y2, x3, y3);
		canvas.DrawLine(x2, y2, x4, y4);
	}

	private Color GetColorForRank(double rank)
	{
		return Color.FromRgb(0.5 + rank * 5, 0.7 + rank * 3, 0.9 - rank * 5);
	}
}