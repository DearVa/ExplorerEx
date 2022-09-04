using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ExplorerEx.Utils.Collections.Internals;

namespace ExplorerEx.Utils.Collections;

/// <summary>
/// Thread-safe collection. You can safely bind it to a WPF control using the property <see cref="AsObservable"/>.
/// </summary>
public sealed class ConcurrentObservableCollection<T> : IList<T>, IReadOnlyList<T>, IList {
	private readonly Dispatcher dispatcher;
	private readonly object lockObj = new();

	private ImmutableList<T> items = ImmutableList<T>.Empty;
	private DispatchedObservableCollection<T>? observableCollection;

	public ConcurrentObservableCollection()
		: this(Application.Current.Dispatcher) {
	}

	public ConcurrentObservableCollection(Dispatcher dispatcher) {
		this.dispatcher = dispatcher;
	}

	/// <summary>
	/// 使用HashSet来去重
	/// </summary>
	public bool UseHashSet { get; set; }

	public DispatchedObservableCollection<T> AsObservable {
		get {
			lock (lockObj) {
				return observableCollection ??= new DispatchedObservableCollection<T>(this, dispatcher, UseHashSet);
			}
		}
	}

	bool ICollection<T>.IsReadOnly => false;

	public int Count => items.Count;

	bool IList.IsReadOnly => false;

	bool IList.IsFixedSize => false;

	int ICollection.Count => Count;

	object ICollection.SyncRoot => ((ICollection)items).SyncRoot;

	bool ICollection.IsSynchronized => ((ICollection)items).IsSynchronized;

	object? IList.this[int index] {
		get => this[index];
		set {
			AssertType(value, nameof(value));
			this[index] = (T)value!;
		}
	}

	public T this[int index] {
		get => items[index];
		set {
			lock (lockObj) {
				items = items.SetItem(index, value);
				observableCollection?.EnqueueReplace(index, value);
			}
		}
	}

	public void Add(T item) {
		lock (lockObj) {
			items = items.Add(item);
			observableCollection?.EnqueueAdd(item);
		}
	}

	public void AddRange(params T[] items) {
		AddRange((IEnumerable<T>)items);
	}

	public void AddRange(IEnumerable<T> items) {
		lock (lockObj) {
			var count = this.items.Count;
			this.items = this.items.AddRange(items);
			observableCollection?.EnqueueAddRange(this.items.GetRange(count, this.items.Count - count));
		}
	}

	public bool Replace(T oldItem, T newItem) {
		lock (lockObj) {
			var index = items.IndexOf(oldItem);
			if (index == -1) {
				return false;
			}
			items = items.SetItem(index, newItem);
			observableCollection?.EnqueueReplace(index, newItem);
			return true;
		}
	}

	public bool ReplaceWhere(Predicate<T> predicate, T newItem) {
		lock (lockObj) {
			for (var i = 0; i < items.Count; i++) {
				if (predicate.Invoke(items[i])) {
					items = items.SetItem(i, newItem);
					observableCollection?.EnqueueReplace(i, newItem);
					return true;
				}
			}
			return false;
		}
	}

	public void InsertRange(int index, IEnumerable<T> items) {
		lock (lockObj) {
			var count = this.items.Count;
			this.items = this.items.InsertRange(index, items);
			var addedItemsCount = this.items.Count - count;
			observableCollection?.EnqueueInsertRange(index, this.items.GetRange(index, addedItemsCount));
		}
	}

	public void Clear() {
		lock (lockObj) {
			items = items.Clear();
			observableCollection?.EnqueueClear();
		}
	}

	public void Reset(IEnumerable<T> items) {
		lock (lockObj) {
			this.items = ImmutableList<T>.Empty.AddRange(items);
			observableCollection?.EnqueueReset(this.items);
		}
	}

	public void Insert(int index, T item) {
		lock (lockObj) {
			items = items.Insert(index, item);
			observableCollection?.EnqueueInsert(index, item);
		}
	}

	public bool Remove(T item) {
		lock (lockObj) {
			var newList = items.Remove(item);
			if (items != newList) {
				items = newList;
				observableCollection?.EnqueueRemove(item);
				return true;
			}

			return false;
		}
	}

	public void RemoveAt(int index) {
		lock (lockObj) {
			items = items.RemoveAt(index);
			observableCollection?.EnqueueRemoveAt(index);
		}
	}

	public void RemoveWhere(Predicate<T> predicate) {
		lock (lockObj) {
			for (var i = 0; i < items.Count; i++) {
				if (predicate.Invoke(items[i])) {
					items = items.RemoveAt(i);
					observableCollection?.EnqueueRemoveAt(i);
				}
			}
		}
	}

	public IEnumerator<T> GetEnumerator() {
		return items.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() {
		return GetEnumerator();
	}

	public int IndexOf(T item) {
		return items.IndexOf(item);
	}

	public bool Contains(T item) {
		return items.Contains(item);
	}

	public void CopyTo(T[] array, int arrayIndex) {
		items.CopyTo(array, arrayIndex);
	}

	public void Sort(IComparer<T>? comparer = null) {
		lock (lockObj) {
			items = items.Sort(comparer);
			observableCollection?.EnqueueReset(items);
		}
	}

	public void StableSort(IComparer<T>? comparer = null) {
		lock (lockObj) {
			items = ImmutableList.CreateRange(items.OrderBy(item => item, comparer));
			observableCollection?.EnqueueReset(items);
		}
	}

	int IList.Add(object? value) {
		AssertType(value, nameof(value));
		var item = (T)value!;
		lock (lockObj) {
			var index = items.Count;
			items = items.Add(item);
			observableCollection?.EnqueueAdd(item);
			return index;
		}
	}

	bool IList.Contains(object? value) {
		AssertType(value, nameof(value));
		return Contains((T)value!);
	}

	void IList.Clear() {
		Clear();
	}

	int IList.IndexOf(object? value) {
		AssertType(value, nameof(value));
		return IndexOf((T)value!);
	}

	void IList.Insert(int index, object? value) {
		AssertType(value, nameof(value));
		Insert(index, (T)value!);
	}

	void IList.Remove(object? value) {
		AssertType(value, nameof(value));
		Remove((T)value!);
	}

	void IList.RemoveAt(int index) {
		RemoveAt(index);
	}

	void ICollection.CopyTo(Array array, int index) {
		((ICollection)items).CopyTo(array, index);
	}

	private static void AssertType(object? value, string argumentName) {
		if (value is null or T) {
			return;
		}

		throw new ArgumentException($"value must be of type '{typeof(T).FullName}'", argumentName);
	}
}
