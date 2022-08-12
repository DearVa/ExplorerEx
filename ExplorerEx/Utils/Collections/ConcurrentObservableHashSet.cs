using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Windows;
using System.Windows.Threading;
using ExplorerEx.Utils.Collections.Internals;

namespace ExplorerEx.Utils.Collections;

/// <summary>
/// Thread-safe collection. You can safely bind it to a WPF control using the property <see cref="AsObservable"/>.
/// </summary>
public sealed class ConcurrentObservableHashSet<T> : ISet<T>, IReadOnlyCollection<T> {
	private readonly Dispatcher dispatcher;
	private readonly object lockObj = new();

	private ImmutableHashSet<T> items = ImmutableHashSet<T>.Empty;
	private DispatchedObservableHashSet<T>? observableHashSet;

	public ConcurrentObservableHashSet()
		: this(GetCurrentDispatcher()) {
	}

	public ConcurrentObservableHashSet(Dispatcher dispatcher) {
		this.dispatcher = dispatcher;
	}

	private static Dispatcher GetCurrentDispatcher() {
		return Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
	}

	public DispatchedObservableHashSet<T> AsObservable {
		get {
			lock (lockObj) {
				return observableHashSet ??= new DispatchedObservableHashSet<T>(this, dispatcher);
			}
		}
	}

	bool ICollection<T>.IsReadOnly => false;

	public int Count => items.Count;

	public void Add(T item) {
		lock (lockObj) {
			items = items.Add(item);
			observableHashSet?.EnqueueAdd(item);
		}
	}

	public void ExceptWith(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	public void IntersectWith(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	public bool IsProperSubsetOf(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	public bool IsProperSupersetOf(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	public bool IsSubsetOf(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	public bool IsSupersetOf(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	public bool Overlaps(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	public bool SetEquals(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	public void SymmetricExceptWith(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	public void UnionWith(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	public void AddRange(params T[] items) {
		AddRange((IEnumerable<T>)items);
	}

	public void AddRange(IEnumerable<T> items) {
		lock (lockObj) {
			var list = items.ToImmutableList();
			this.items = this.items.Union(list);
			observableHashSet?.EnqueueAddRange(list);
		}
	}

	bool ISet<T>.Add(T item) {
		lock (lockObj) {
			if (items.Contains(item)) {
				return false;
			}
			items = items.Add(item);
			observableHashSet?.EnqueueAdd(item);
			return true;
		}
	}

	public void Clear() {
		lock (lockObj) {
			items = items.Clear();
			observableHashSet?.EnqueueClear();
		}
	}

	public void Reset(IEnumerable<T> items) {
		lock (lockObj) {
			this.items = ImmutableHashSet<T>.Empty.Union(items);
			observableHashSet?.EnqueueReset(this.items.ToImmutableList());
		}
	}

	public void CopyTo(T[] array, int arrayIndex) {
		((ICollection)items).CopyTo(array, arrayIndex);
	}

	public bool Remove(T item) {
		lock (lockObj) {
			var newList = items.Remove(item);
			if (items != newList) {
				items = newList;
				observableHashSet?.EnqueueRemove(item);
				return true;
			}

			return false;
		}
	}

	public IEnumerator<T> GetEnumerator() {
		return items.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() {
		return GetEnumerator();
	}

	public bool Contains(T item) {
		return items.Contains(item);
	}
}
