using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace PageRankApp.Maui.Utils;

public class ObservableRangeCollection<T> : ObservableCollection<T>
{
	public void AddRange(IEnumerable<T> collection)
	{
		ArgumentNullException.ThrowIfNull(collection);

		CheckReentrancy();
		foreach (var i in collection)
		{
			Items.Add(i);
		}
		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}
}
