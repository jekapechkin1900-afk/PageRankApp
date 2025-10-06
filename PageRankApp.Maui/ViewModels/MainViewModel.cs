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
using PageRankApp.Maui.Utils;

namespace PageRankApp.Maui.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
	private string _statusMessage = "Готово. Создайте или загрузите граф.";
	private bool _isBusy;
	private bool _areResultsCalculated = false;
	private string _schedulerIpAddress;
	private int _schedulerPort;
	private const int LargeGraphThreshold = 500;

	public bool IsGraphLarge => Nodes.Count > LargeGraphThreshold;
	public bool CanSaveResults => !IsBusy && _areResultsCalculated;
	public ObservableRangeCollection<Node> Nodes { get; } = [];
	public ObservableRangeCollection<Edge> Edges { get; } = [];

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
			StatusMessage = "Нечего сохранять. Граф пуст.";
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
				StatusMessage = $"Результаты успешно сохранены в {fileSaverResult.FilePath}";
			}
			else
			{
				StatusMessage = $"Ошибка при сохарнении результатов: {fileSaverResult.Exception?.Message}";
			}
		}
		catch (Exception ex)
		{
			StatusMessage = $"An error occurred while saving: {ex.Message}";
		}
	}

	private void OnGraphStructureChanged(string statusUpdateMessage = "")
	{
		OnPropertyChanged(nameof(IsGraphLarge));
		if (_areResultsCalculated)
		{
			_areResultsCalculated = false;
			UpdateCanSave(); 

			if (string.IsNullOrEmpty(statusUpdateMessage))
			{
				StatusMessage = "Граф изменен. Требуется перерасчет.";
			}
		}
		if (!string.IsNullOrEmpty(statusUpdateMessage))
		{
			StatusMessage = statusUpdateMessage;
		}
		InvalidateRequest?.Invoke();
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
		OnGraphStructureChanged();
	}

	private async Task OnAddEdge()
	{
		var sourceIdStr = await App.Current.MainPage.DisplayPromptAsync("Добавить связь", "Введите исходный узел ID:");
		if (string.IsNullOrWhiteSpace(sourceIdStr) || !int.TryParse(sourceIdStr, out var sourceId)) return;

		var targetIdStr = await App.Current.MainPage.DisplayPromptAsync("Добавить связь", "Введите целевой узел ID:");
		if (string.IsNullOrWhiteSpace(targetIdStr) || !int.TryParse(targetIdStr, out var targetId)) return;

		if (Nodes.Any(n => n.Id == sourceId) && Nodes.Any(n => n.Id == targetId))
		{
			Edges.Add(new Edge { SourceId = sourceId, TargetId = targetId });
			OnGraphStructureChanged();
		}
		else
		{
			await App.Current.MainPage.DisplayAlert("Ошибка", "One or both nodes do not exist.", "OK");
		}
	}

	private async Task OnLoadFromFile()
	{
		try
		{
			var result = await FilePicker.PickAsync(new PickOptions
			{
				PickerTitle = "Выберите файл с данными графа (.txt)"
			});
			if (result == null) return;

			OnClearGraph();
			SetBusy(true, "Загрузка графа из файла...");

			var (loadedNodes, loadedEdges) = await Task.Run(async () =>
			{

				var tempNodesDict = new Dictionary<int, Node>();
				var tempEdges = new List<Edge>();

				using var stream = await result.OpenReadAsync();
				using var reader = new StreamReader(stream);

				string line;
				while ((line = await reader.ReadLineAsync()) != null)
				{
					var parts = line.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length == 2 && int.TryParse(parts[0], out var sourceId) && int.TryParse(parts[1], out var targetId))
					{
						tempEdges.Add(new Edge { SourceId = sourceId, TargetId = targetId });

						if (!tempNodesDict.ContainsKey(sourceId))
						{
							tempNodesDict[sourceId] = new Node { Id = sourceId };
						}
						if (!tempNodesDict.ContainsKey(targetId))
						{
							tempNodesDict[targetId] = new Node { Id = targetId };
						}
					}
				}

				var random = new Random();
				var nodeList = tempNodesDict.Values.OrderBy(n => n.Id).ToList();
				foreach (var node in nodeList)
				{
					node.X = random.Next(50, 850);
					node.Y = random.Next(50, 550);
				}

				return (nodeList, tempEdges);
			});

			StatusMessage = "Подготовка к отображению...";

			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				Nodes.AddRange(loadedNodes);
				Edges.AddRange(loadedEdges);
			});

			var status = $"Граф загружен: {Nodes.Count} узлов, {Edges.Count} связей.";
			OnGraphStructureChanged(status);
		}
		catch (Exception ex)
		{
			StatusMessage = $"Ошибка при загрузке файла: {ex.Message}";
		}
		finally
		{
			SetBusy(false);
		}
	}



	private void OnClearGraph()
	{
		Nodes.Clear();
		Edges.Clear();
		OnGraphStructureChanged("Граф очищен.");
	}

	private async Task OnCalculate()
	{
		if (!Nodes.Any())
		{
			StatusMessage = "Граф пуст. Нечего считать.";
			return;
		}

		SetBusy(true, "Выполняется расчет...");
		try
		{
			StatusMessage = "Подключени к планировщику...";
			using var client = new TcpClient();
			await client.ConnectAsync(SchedulerIpAddress, SchedulerPort); 
			await using var stream = client.GetStream();
			StatusMessage = "Подключено. Отправка графа...";

			var graph = new Graph { Nodes = this.Nodes.ToList(), Edges = this.Edges.ToList() };
			var message = new NetworkMessage
			{
				Type = MessageType.SubmitGraph,
				JsonPayload = JsonSerializer.Serialize(graph)
			};

			await NetworkHelper.WriteMessageAsync(stream, message);
			StatusMessage = "Граф отправлен. Ожидание результатов...";

			var resultMessage = await NetworkHelper.ReadMessageAsync(stream);
			if (resultMessage?.Type == MessageType.CalculationComplete)
			{
				var ranks = JsonSerializer.Deserialize<Dictionary<int, double>>(resultMessage.JsonPayload);
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					StatusMessage = "Обновление рангов...";
					var nodeDict = Nodes.ToDictionary(n => n.Id);
					foreach (var rankEntry in ranks)
					{
						if (nodeDict.TryGetValue(rankEntry.Key, out var node))
						{
							node.Rank = rankEntry.Value;
						}
					}
				});
				var status = "Расчеты завершены! Ранги обновлены.";
				OnGraphStructureChanged(status);
				_areResultsCalculated = true;
			}
			else
			{
				StatusMessage = "Получен неожиданный ответ от планировщика.";
			}
		}
		catch (Exception ex)
		{
			StatusMessage = $"Ошибка: {ex.Message}";
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
