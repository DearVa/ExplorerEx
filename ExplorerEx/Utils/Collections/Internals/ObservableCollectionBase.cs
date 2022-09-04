using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ExplorerEx.Utils.Collections.Internals;

// From: https://github.com/meziantou/Meziantou.Framework
public abstract class ObservableCollectionBase<T> : INotifyCollectionChanged, INotifyPropertyChanged {
	public event NotifyCollectionChangedEventHandler? CollectionChanged;
	public event PropertyChangedEventHandler? PropertyChanged;

	private protected HashSet<T>? Set { get; }
	private protected List<T> Items { get; }

	/// <summary>
	/// 初始化
	/// </summary>
	/// <param name="useHash">是否使用HashSet来去重</param>
	protected ObservableCollectionBase(bool useHash) {
		Items = new List<T>();
		if (useHash) {
			Set = new HashSet<T>();
		}
	}

	protected ObservableCollectionBase(IList<T>? items, bool useHash) {
		if (items == null) {
			Items = new List<T>();
			if (useHash) {
				Set = new HashSet<T>();
			}
		} else {
			Items = new List<T>(items);
			if (useHash) {
				Set = new HashSet<T>(items);
			}
		}
	}

	public void EnsureCapacity(int capacity) {
		Items.EnsureCapacity(capacity);
	}

	protected void ReplaceItem(int index, T item) {
		var oldItem = Items[index];
		Items[index] = item;
		if (Set != null) {
			Set.Remove(oldItem);
			Set.Add(item);
		}

		OnIndexerPropertyChanged();
		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, oldItem, index));
	}

	protected void InsertItem(int index, T item) {
		if (Set != null) {
			if (Set.Contains(item)) {
				return;
			}
			Set.Add(item);
		}
		Items.Insert(index, item);

		OnCountPropertyChanged();
		OnIndexerPropertyChanged();
		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
	}

	protected void InsertItems(int index, ImmutableList<T> items) {
		if (Set == null) {
			foreach (var item in items) {
				Items.Insert(index++, item);

				OnCountPropertyChanged();
				OnIndexerPropertyChanged();
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
			}
		} else {
			foreach (var item in items.Where(item => !Set.Contains(item))) {
				Items.Insert(index++, item);
				Set.Add(item);

				OnCountPropertyChanged();
				OnIndexerPropertyChanged();
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
			}
		}
	}

	protected void AddItem(T item) {
		if (Set != null) {
			if (Set.Contains(item)) {
				return;
			}
			Set.Add(item);
		}
		var index = Items.Count;
		Items.Add(item);

		OnCountPropertyChanged();
		OnIndexerPropertyChanged();
		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
	}

	protected void AddItems(ImmutableList<T> items) {
		if (Set == null) {
			foreach (var item in items) {
				var index = Items.Count;
				Items.Add(item);

				OnCountPropertyChanged();
				OnIndexerPropertyChanged();
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
			}
		} else {
			foreach (var item in items.Where(item => !Set.Contains(item))) {
				var index = Items.Count;
				Items.Add(item);
				Set.Add(item);

				OnCountPropertyChanged();
				OnIndexerPropertyChanged();
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
			}
		}
	}

	protected void RemoveItemAt(int index) {
		var item = Items[index];
		Items.RemoveAt(index);
		Set?.Remove(item);

		OnCountPropertyChanged();
		OnIndexerPropertyChanged();
		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
	}

	protected void RemoveItem(T item) {
		var index = Items.IndexOf(item);
		if (index < 0) {
			return;
		}

		Items.RemoveAt(index);
		Set?.Remove(item);

		OnCountPropertyChanged();
		OnIndexerPropertyChanged();
		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
	}

	protected void ClearItems() {
		Items.Clear();
		Set?.Clear();
		OnCountPropertyChanged();
		OnIndexerPropertyChanged();
		CollectionChanged?.Invoke(this, EventArgsCache.ResetCollectionChanged);
	}

	protected void Reset(ImmutableList<T> items) {
		Items.Clear();
		Items.AddRange(items);
		if (Set != null) {
			Set.Clear();
			foreach (var item in items) {
				Set.Add(item);
			}
		}
		OnIndexerPropertyChanged();
		OnCollectionChanged(EventArgsCache.ResetCollectionChanged);
	}

	private void OnCountPropertyChanged() => OnPropertyChanged(EventArgsCache.CountPropertyChanged);
	private void OnIndexerPropertyChanged() => OnPropertyChanged(EventArgsCache.IndexerPropertyChanged);

	protected void OnPropertyChanged(PropertyChangedEventArgs args) => PropertyChanged?.Invoke(this, args);
	protected void OnCollectionChanged(NotifyCollectionChangedEventArgs args) => CollectionChanged?.Invoke(this, args);
}
