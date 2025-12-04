using System.Diagnostics;

namespace PageRankApp.Tests.Helpers;

public class ProcessRunner : IDisposable
{
	private readonly List<Process> _processes = new();

	private const string SchedulerPath = @"..\..\..\..\PageRankApp.Scheduler\bin\Debug\net9.0\PageRankApp.Scheduler.exe";
	private const string SolverPath = @"..\..\..\..\PageRankApp.Solver\bin\Debug\net9.0\PageRankApp.Solver.exe";

	public void StartScheduler()
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SchedulerPath)),
			UseShellExecute = true,
			CreateNoWindow = false,
			WindowStyle = ProcessWindowStyle.Minimized
		};

		var process = Process.Start(startInfo);
		if (process != null)
		{
			_processes.Add(process);
			Thread.Sleep(1000);
		}
		else
		{
			throw new FileNotFoundException($"Could not find Scheduler executable at {startInfo.FileName}. Did you build the solution?");
		}
	}

	public void StartSolvers(int count)
	{
		var path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SolverPath));
		if (!File.Exists(path)) throw new FileNotFoundException($"Solver exe not found at {path}");

		for (int i = 0; i < count; i++)
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = path,
				UseShellExecute = true,
				CreateNoWindow = true, 
				WindowStyle = ProcessWindowStyle.Hidden
			};
			var process = Process.Start(startInfo);
			if (process != null) _processes.Add(process);
		}
	}

	public void Dispose()
	{
		foreach (var process in _processes)
		{
			try
			{
				if (!process.HasExited)
				{
					process.Kill();
				}
				process.Dispose();
			}
			catch { }
		}
		_processes.Clear();
	}
}