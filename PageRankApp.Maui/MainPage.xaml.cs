using PageRankApp.Maui.Drawable;
using PageRankApp.Maui.ViewModels;
using PageRankApp.Shared.Models;

namespace PageRankApp.Maui;

public partial class MainPage : ContentPage
{
	private readonly MainViewModel _viewModel;
	private readonly GraphDrawable _graphDrawable;
	private Node _draggedNode = null;

	public MainPage()
	{
		InitializeComponent();

		_viewModel = BindingContext as MainViewModel;

		_graphDrawable = new GraphDrawable
		{
			Nodes = _viewModel.Nodes,
			Edges = _viewModel.Edges
		};

		GraphCanvas.Drawable = _graphDrawable;

		_viewModel.InvalidateRequest = () => GraphCanvas.Invalidate();
	}

	private void OnStartInteraction(object sender, TouchEventArgs e)
	{
		if (_viewModel.IsGraphLarge) return;

		if (e.Touches.Length > 0)
		{
			var touchPoint = e.Touches[0];
			_draggedNode = FindNodeAtPoint(touchPoint);

			if (_draggedNode != null)
			{
				_graphDrawable.SelectedNode = _draggedNode;
				GraphCanvas.Invalidate();
			}
		}
	}

	private void OnDragInteraction(object sender, TouchEventArgs e)
	{
		if (_viewModel.IsGraphLarge) return;

		if (_draggedNode != null && e.Touches.Length > 0)
		{
			var touchPoint = e.Touches[0];

			_draggedNode.X = touchPoint.X;
			_draggedNode.Y = touchPoint.Y;

			GraphCanvas.Invalidate();
		}
	}

	private void OnEndInteraction(object sender, TouchEventArgs e)
	{
		if (_viewModel.IsGraphLarge) return;

		_draggedNode = null;
		_graphDrawable.SelectedNode = null;
		GraphCanvas.Invalidate();
	}

	private Node FindNodeAtPoint(PointF point)
	{
		float nodeHitboxSize = 35; 
		foreach (var node in _viewModel.Nodes.Reverse())
		{
			var distance = Math.Sqrt(Math.Pow(point.X - node.X, 2) + Math.Pow(point.Y - node.Y, 2));
			if (distance <= nodeHitboxSize / 2)
			{
				return node;
			}
		}
		return null;
	}
}
