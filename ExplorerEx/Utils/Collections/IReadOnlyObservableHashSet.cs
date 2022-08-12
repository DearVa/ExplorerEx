using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ExplorerEx.Utils.Collections;

public interface IReadOnlyObservableHashSet<T> : IReadOnlySet<T>, INotifyCollectionChanged, INotifyPropertyChanged { }
