using System.Collections.Specialized;
using System.ComponentModel;

namespace ExplorerEx.Utils.Collections.Internals;

// From: https://github.com/meziantou/Meziantou.Framework
internal static class EventArgsCache {
	internal static readonly PropertyChangedEventArgs CountPropertyChanged = new("Count");
	internal static readonly PropertyChangedEventArgs IndexerPropertyChanged = new("Item[]");
	internal static readonly NotifyCollectionChangedEventArgs ResetCollectionChanged = new(NotifyCollectionChangedAction.Reset);
}
