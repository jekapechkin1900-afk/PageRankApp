using System.Collections.Concurrent;

namespace PageRankApp.Tests.UnitTests;

public class TaskPriorityTests
{
	private class MockTask
	{
		public int Id;
		public bool IsLarge;
	}

	[Fact]
	public void PriorityLogic_ShouldPickLargeTaskFirst_IfResourcesAvailable()
	{
		// Arrange
		var largeQueue = new ConcurrentQueue<MockTask>();
		var smallQueue = new ConcurrentQueue<MockTask>();

		largeQueue.Enqueue(new MockTask { Id = 1, IsLarge = true });
		smallQueue.Enqueue(new MockTask { Id = 2, IsLarge = false });

		int totalSolvers = 10;
		int availableSolvers = 10;
		bool isLargeRunning = false;
		double largeQuota = 0.7;

		MockTask pickedTask = null;

		// Act 
		if (!isLargeRunning && largeQueue.TryPeek(out _))
		{
			int required = (int)Math.Ceiling(totalSolvers * largeQuota);
			if (availableSolvers >= required)
			{
				largeQueue.TryDequeue(out pickedTask);
				isLargeRunning = true;
			}
		}

		if (pickedTask == null && smallQueue.TryPeek(out _))
		{
			smallQueue.TryDequeue(out pickedTask);
		}

		// Assert
		Assert.NotNull(pickedTask);
		Assert.True(pickedTask.IsLarge);
		Assert.Equal(1, pickedTask.Id);
	}

	[Fact]
	public void PriorityLogic_ShouldPickSmallTask_IfLargeBlockedByResources()
	{
		// Arrange
		var largeQueue = new ConcurrentQueue<MockTask>();
		var smallQueue = new ConcurrentQueue<MockTask>();

		largeQueue.Enqueue(new MockTask { Id = 1, IsLarge = true });
		smallQueue.Enqueue(new MockTask { Id = 2, IsLarge = false });

		int totalSolvers = 10;
		int availableSolvers = 2; 
		bool isLargeRunning = false;
		double largeQuota = 0.7; 

		MockTask pickedTask = null;

		// Act
		if (!isLargeRunning && largeQueue.TryPeek(out _))
		{
			int required = (int)Math.Ceiling(totalSolvers * largeQuota);
			if (availableSolvers >= required) 
			{
				largeQueue.TryDequeue(out pickedTask);
			}
		}

		if (pickedTask == null && smallQueue.TryPeek(out _))
		{
			if (availableSolvers >= 2)
			{
				smallQueue.TryDequeue(out pickedTask);
			}
		}

		// Assert
		Assert.NotNull(pickedTask);
		Assert.False(pickedTask.IsLarge); 
		Assert.Equal(2, pickedTask.Id);
	}
}