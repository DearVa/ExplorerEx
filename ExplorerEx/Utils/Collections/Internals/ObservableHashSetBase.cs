using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ExplorerEx.Utils.Collections.Internals; 

public abstract class ObservableHashSetBase<T> : INotifyCollectionChanged, INotifyPropertyChanged {
	public event NotifyCollectionChangedEventHandler? CollectionChanged;
	public event PropertyChangedEventHandler? PropertyChanged;

	private protected HashSet<T> Items { get; }

	protected ObservableHashSetBase() {
		Items = new HashSet<T>();
	}

	protected ObservableHashSetBase(IEnumerable<T>? items) {
		if (items == null) {
			Items = new HashSet<T>();
		} else {
			Items = new HashSet<T>(items);
		}
	}

	public void EnsureCapacity(int capacity) {
		Items.EnsureCapacity(capacity);
	}

	protected bool ReplaceItem(T oldItem, T item) {
		if (Items.Contains(oldItem)) {
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, oldItem));
			return true;
		}
		return false;
	}

	protected bool AddItem(T item) {
		if (Items.Add(item)) {
			OnCountPropertyChanged();
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
			return true;
		}
		return false;
	}

	protected void AddItems(ImmutableList<T> items) {
		foreach (var item in items) {
			AddItem(item);
		}
	}

	protected bool RemoveItem(T item) {
		if (Items.Remove(item)) {
			OnCountPropertyChanged();
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
			return true;
		}
		return false;
	}

	protected void ClearItems() {
		Items.Clear();
		OnCountPropertyChanged();
		CollectionChanged?.Invoke(this, EventArgsCache.ResetCollectionChanged);
	}

	protected void Reset(ImmutableList<T> items) {
		Items.Clear();
		AddItems(items);
		OnCollectionChanged(EventArgsCache.ResetCollectionChanged);
	}

	private void OnCountPropertyChanged() => OnPropertyChanged(EventArgsCache.CountPropertyChanged);

	protected virtual void OnPropertyChanged(PropertyChangedEventArgs args) => PropertyChanged?.Invoke(this, args);
	protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs args) => CollectionChanged?.Invoke(this, args);

}