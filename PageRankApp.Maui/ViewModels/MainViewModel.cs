using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Maui.Storage;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using PageRankApp.Shared.Models;
using PageRankApp.Shared.Network;

namespace PageRankApp.Maui.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
	private string _statusMessage = "Ready. Create or load a graph.";
	private bool _isBusy;
	private bool _areResultsCalculated = false;
	private string _schedulerIpAddress;
	private int _schedulerPort;
	public bool CanSaveResults => !IsBusy && _areResultsCalculated;
	public ObservableCollection<Node> Nodes { get; } = [];
	public ObservableCollection<Edge> Edges { get; } = [];

	public string SchedulerIpAddress
	{
		get => _schedulerIpAddress;
		set
		{
			if (SetProperty(ref _schedulerIpAddress, value))
			{
				Preferences.Set(nameof(SchedulerIpAddress), value);
			}
		}
	}

	public int SchedulerPort
	{
		get => _schedulerPort;
		set
		{
			if (SetProperty(ref _schedulerPort, value))
			{
				Preferences.Set(nameof(SchedulerPort), value);
			}
		}
	}


	public string StatusMessage
	{
		get => _statusMessage;
		set => SetProperty(ref _statusMessage, value);
	}

	public bool IsBusy
	{
		get => _isBusy;
		set
		{
			if (SetProperty(ref _isBusy, value))
			{
				OnPropertyChanged(nameof(CanSaveResults));
			}
		}
	}

	public Action InvalidateRequest;

	public Command AddNodeCommand { get; }
	public Command AddEdgeCommand { get; }
	public Command CalculateCommand { get; }
	public Command LoadFromFileCommand { get; }
	public Command ClearGraphCommand { get; }
	public Command SaveResultsCommand { get; }

	public MainViewModel()
	{
		AddNodeCommand = new Command(OnAddNode, () => !IsBusy);
		AddEdgeCommand = new Command(async () => await OnAddEdge(), () => !IsBusy);
		CalculateCommand = new Command(async () => await OnCalculate(), () => !IsBusy);
		LoadFromFileCommand = new Command(async () => await OnLoadFromFile(), () => !IsBusy);
		ClearGraphCommand = new Command(OnClearGraph, () => !IsBusy);
		SaveResultsCommand = new Command(
		   async () => await OnSaveResults(),
		   () => !IsBusy && _areResultsCalculated);
		LoadSettings();
	}

	private void LoadSettings()
	{
		SchedulerIpAddress = Preferences.Get(nameof(SchedulerIpAddress), "127.0.0.1");
		SchedulerPort = Preferences.Get(nameof(SchedulerPort), 8888);
	}

	private async Task OnSaveResults()
	{
		if (!Nodes.Any())
		{
			StatusMessage = "Nothing to save. The graph is empty.";
			return;
		}

		try
		{
			var csvBuilder = new StringBuilder();
			csvBuilder.AppendLine("NodeId,PageRank"); 

			var sortedNodes = Nodes.OrderByDescending(n => n.Rank);

			foreach (var node in sortedNodes)
			{
				csvBuilder.AppendLine($"{node.Id},{node.Rank:F8}");
			}

			var fileContent = csvBuilder.ToString();
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
			var fileSaverResult = await FileSaver.Default.SaveAsync("pagerank_results.csv", stream, CancellationToken.None);

			if (fileSaverResult.IsSuccessful)
			{
				StatusMessage = $"Results successfully saved to {fileSaverResult.FilePath}";
			}
			else
			{
				StatusMessage = $"Failed to save results: {fileSaverResult.Exception?.Message}";
			}
		}
		catch (Exception ex)
		{
			StatusMessage = $"An error occurred while saving: {ex.Message}";
		}
	}

	private void OnAddNode()
	{
		var newId = Nodes.Any() ? Nodes.Max(n => n.Id) + 1 : 0;
		Nodes.Add(new Node
		{
			Id = newId,
			X = new Random().Next(50, 450),
			Y = new Random().Next(50, 450)
		});
		InvalidateRequest?.Invoke();
		if (_areResultsCalculated)
		{
			_areResultsCalculated = false;
			UpdateCanSave();
			StatusMessage = "Graph modified. Please recalculate.";
		}
	}

	private async Task OnAddEdge()
	{
		var sourceIdStr = await App.Current.MainPage.DisplayPromptAsync("Add Edge", "Enter Source Node ID:");
		if (string.IsNullOrWhiteSpace(sourceIdStr) || !int.TryParse(sourceIdStr, out var sourceId)) return;

		var targetIdStr = await App.Current.MainPage.DisplayPromptAsync("Add Edge", "Enter Target Node ID:");
		if (string.IsNullOrWhiteSpace(targetIdStr) || !int.TryParse(targetIdStr, out var targetId)) return;

		if (Nodes.Any(n => n.Id == sourceId) && Nodes.Any(n => n.Id == targetId))
		{
			Edges.Add(new Edge { SourceId = sourceId, TargetId = targetId });
			InvalidateRequest?.Invoke();

			if (_areResultsCalculated)
			{
				_areResultsCalculated = false;
				UpdateCanSave();
				StatusMessage = "Graph modified. Please recalculate.";
			}
		}
		else
		{
			await App.Current.MainPage.DisplayAlert("Error", "One or both nodes do not exist.", "OK");
		}
	}

	private async Task OnLoadFromFile()
	{
		try
		{
			var result = await FilePicker.PickAsync(new PickOptions { PickerTitle = "Select a graph file (.txt)" });
			if (result == null) return;
			OnClearGraph();
			using var stream = await result.OpenReadAsync();
			using var reader = new StreamReader(stream);
			var tempNodes = new HashSet<int>();
			string line;
			while ((line = await reader.ReadLineAsync()) != null)
			{
				var parts = line.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 2 && int.TryParse(parts[0], out var sourceId) && int.TryParse(parts[1], out var targetId))
				{
					Edges.Add(new Edge { SourceId = sourceId, TargetId = targetId });
					tempNodes.Add(sourceId);
					tempNodes.Add(targetId);
				}
			}
			foreach (var nodeId in tempNodes.OrderBy(id => id))
			{
				Nodes.Add(new Node { Id = nodeId, X = new Random().Next(50, 450), Y = new Random().Next(50, 450) });
			}
			InvalidateRequest?.Invoke();
			StatusMessage = $"Graph loaded with {Nodes.Count} nodes and {Edges.Count} edges.";
			_areResultsCalculated = false;
			UpdateCanSave();
		}
		catch (Exception ex)
		{
			StatusMessage = $"Failed to load file: {ex.Message}";
		}
	}

	private void OnClearGraph()
	{
		Nodes.Clear();
		Edges.Clear();
		InvalidateRequest?.Invoke();
		StatusMessage = "Graph cleared.";
		_areResultsCalculated = false;
		SaveResultsCommand.ChangeCanExecute();
		OnPropertyChanged(nameof(CanSaveResults));
	}

	private async Task OnCalculate()
	{
		if (!Nodes.Any())
		{
			StatusMessage = "Graph is empty. Nothing to calculate.";
			return;
		}

		SetBusy(true, "Calculating...");
		try
		{
			StatusMessage = "Connecting to scheduler...";
			using var client = new TcpClient();
			await client.ConnectAsync(SchedulerIpAddress, SchedulerPort); 
			await using var stream = client.GetStream();
			StatusMessage = "Connected. Sending graph...";

			var graph = new Graph { Nodes = this.Nodes.ToList(), Edges = this.Edges.ToList() };
			var message = new NetworkMessage
			{
				Type = MessageType.SubmitGraph,
				JsonPayload = JsonSerializer.Serialize(graph)
			};

			await NetworkHelper.WriteMessageAsync(stream, message);
			StatusMessage = "Graph sent. Waiting for results...";

			var resultMessage = await NetworkHelper.ReadMessageAsync(stream);
			if (resultMessage?.Type == MessageType.CalculationComplete)
			{
				var ranks = JsonSerializer.Deserialize<Dictionary<int, double>>(resultMessage.JsonPayload);
				foreach (var node in Nodes)
				{
					if (ranks.TryGetValue(node.Id, out var rank))
					{
						node.Rank = rank;
					}
				}
				InvalidateRequest?.Invoke();
				StatusMessage = "Calculation complete! Ranks updated.";
				_areResultsCalculated = true;
			}
			else
			{
				StatusMessage = "Received an unexpected response from the scheduler.";
			}
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error: {ex.Message}";
			Debug.WriteLine(ex);
		}
		finally
		{
			SetBusy(false);
		}
	}

	private void UpdateCanSave()
	{
		(SaveResultsCommand as Command)?.ChangeCanExecute();
		OnPropertyChanged(nameof(CanSaveResults));
	}

	private void SetBusy(bool isBusy, string message = null)
	{
		IsBusy = isBusy;
		if (message != null)
		{
			StatusMessage = message;
		}
		AddNodeCommand?.ChangeCanExecute();
		AddEdgeCommand?.ChangeCanExecute();
		CalculateCommand?.ChangeCanExecute();
		LoadFromFileCommand?.ChangeCanExecute();
		ClearGraphCommand?.ChangeCanExecute();
		SaveResultsCommand?.ChangeCanExecute();
		OnPropertyChanged(nameof(CanSaveResults));
	}

	public event PropertyChangedEventHandler PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
	protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
	{
		if (EqualityComparer<T>.Default.Equals(backingStore, value)) return false;
		backingStore = value;
		OnPropertyChanged(propertyName);
		return true;
	}
}
