using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ExplorerEx.Utils.Collections;

public interface IReadOnlyObservableCollection<out T> : IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged { }
