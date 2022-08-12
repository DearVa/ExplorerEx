using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Threading;

namespace ExplorerEx.Utils.Collections.Internals;

public sealed class DispatchedObservableCollection<T> : ObservableCollectionBase<T>, IReadOnlyObservableCollection<T>, IList<T>, IList {
	private readonly ConcurrentQueue<PendingEvent<T>> pendingEvents = new();
	private readonly ConcurrentObservableCollection<T> collection;
	private readonly Dispatcher dispatcher;

	private bool isDispatcherPending;

	public DispatchedObservableCollection(ConcurrentObservableCollection<T> collection, Dispatcher dispatcher)
		: base(collection) {
		this.collection = collection;
		this.dispatcher = dispatcher;
	}

	private void AssertIsOnDispatcherThread() {
		if (!dispatcher.CheckAccess()) {
			var currentThreadId = Environment.CurrentManagedThreadId;
			throw new InvalidOperationException("The collection must be accessed from the dispatcher thread only. Current thread ID: " + currentThreadId.ToString(CultureInfo.InvariantCulture));
		}
	}

	private static void AssertType(object? value, string argumentName) {
		if (value is null or T) {
			return;
		}

		throw new ArgumentException($"value must be of type '{typeof(T).FullName}'", argumentName);
	}

	public int Count {
		get {
			AssertIsOnDispatcherThread();
			return Items.Count;
		}
	}

	bool ICollection<T>.IsReadOnly {
		get {
			AssertIsOnDispatcherThread();
			return ((ICollection<T>)collection).IsReadOnly;
		}
	}

	int ICollection.Count {
		get {
			AssertIsOnDispatcherThread();
			return Count;
		}
	}

	object ICollection.SyncRoot {
		get {
			AssertIsOnDispatcherThread();
			return ((ICollection)Items).SyncRoot;
		}
	}

	bool ICollection.IsSynchronized {
		get {
			AssertIsOnDispatcherThread();
			return ((ICollection)Items).IsSynchronized;
		}
	}

	bool IList.IsReadOnly {
		get {
			AssertIsOnDispatcherThread();
			return ((IList)Items).IsReadOnly;
		}
	}

	bool IList.IsFixedSize {
		get {
			AssertIsOnDispatcherThread();
			return ((IList)Items).IsFixedSize;
		}
	}

	object? IList.this[int index] {
		get {
			AssertIsOnDispatcherThread();
			return this[index];
		}

		set {
			// it will immediately modify both collections as we are on the dispatcher thread
			AssertType(value, nameof(value));
			AssertIsOnDispatcherThread();
			collection[index] = (T)value!;
		}
	}

	T IList<T>.this[int index] {
		get {
			AssertIsOnDispatcherThread();
			return this[index];
		}
		set {
			// it will immediately modify both collections as we are on the dispatcher thread
			AssertIsOnDispatcherThread();
			collection[index] = value;
		}
	}

	public IEnumerator<T> GetEnumerator() {
		AssertIsOnDispatcherThread();
		return Items.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public void CopyTo(T[] array, int arrayIndex) {
		AssertIsOnDispatcherThread();
		Items.CopyTo(array, arrayIndex);
	}

	public int IndexOf(T item) {
		AssertIsOnDispatcherThread();
		return Items.IndexOf(item);
	}

	public bool Contains(T item) {
		AssertIsOnDispatcherThread();
		return Items.Contains(item);
	}

	public T this[int index] {
		get {
			AssertIsOnDispatcherThread();
			return Items[index];
		}
	}

	internal void EnqueueReplace(int index, T value) {
		EnqueueEvent(PendingEvent.Replace(index, value));
	}

	internal void EnqueueReset(System.Collections.Immutable.ImmutableList<T> items) {
		EnqueueEvent(PendingEvent.Reset(items));
	}

	internal void EnqueueAdd(T item) {
		EnqueueEvent(PendingEvent.Add(item));
	}

	internal void EnqueueAddRange(System.Collections.Immutable.ImmutableList<T> items) {
		EnqueueEvent(PendingEvent.AddRange(items));
	}

	internal bool EnqueueRemove(T item) {
		EnqueueEvent(PendingEvent.Remove(item));
		return true;
	}

	internal void EnqueueRemoveAt(int index) {
		EnqueueEvent(PendingEvent.RemoveAt<T>(index));
	}

	internal void EnqueueClear() {
		EnqueueEvent(PendingEvent.Clear<T>());
	}

	internal void EnqueueInsert(int index, T item) {
		EnqueueEvent(PendingEvent.Insert(index, item));
	}

	internal void EnqueueInsertRange(int index, System.Collections.Immutable.ImmutableList<T> items) {
		EnqueueEvent(PendingEvent.InsertRange(index, items));
	}

	private void EnqueueEvent(PendingEvent<T> @event) {
		pendingEvents.Enqueue(@event);
		ProcessPendingEventsOrDispatch();
	}

	private void ProcessPendingEventsOrDispatch() {
		if (!dispatcher.CheckAccess()) {
			if (!isDispatcherPending) {
				isDispatcherPending = true;
				dispatcher.BeginInvoke(ProcessPendingEvents);
			}

			return;
		}

		ProcessPendingEvents();
	}

	private void ProcessPendingEvents() {
		isDispatcherPending = false;
		while (pendingEvents.TryDequeue(out var pendingEvent)) {
			switch (pendingEvent.Type) {
			case PendingEventType.Add:
				AddItem(pendingEvent.Item);
				break;

			case PendingEventType.AddRange:
				AddItems(pendingEvent.Items!);
				break;

			case PendingEventType.Remove:
				RemoveItem(pendingEvent.Item);
				break;

			case PendingEventType.Clear:
				ClearItems();
				break;

			case PendingEventType.Insert:
				InsertItem(pendingEvent.Index, pendingEvent.Item);
				break;

			case PendingEventType.InsertRange:
				InsertItems(pendingEvent.Index, pendingEvent.Items!);
				break;

			case PendingEventType.RemoveAt:
				RemoveItemAt(pendingEvent.Index);
				break;

			case PendingEventType.Replace:
				ReplaceItem(pendingEvent.Index, pendingEvent.Item);
				break;

			case PendingEventType.Reset:
				Reset(pendingEvent.Items!);
				break;
			}
		}
	}

	void IList<T>.Insert(int index, T item) {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertIsOnDispatcherThread();
		collection.Insert(index, item);
	}

	void IList<T>.RemoveAt(int index) {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertIsOnDispatcherThread();
		collection.RemoveAt(index);
	}

	void ICollection<T>.Add(T item) {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertIsOnDispatcherThread();
		collection.Add(item);
	}

	void ICollection<T>.Clear() {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertIsOnDispatcherThread();
		collection.Clear();
	}

	bool ICollection<T>.Remove(T item) {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertIsOnDispatcherThread();
		return collection.Remove(item);
	}

	void ICollection.CopyTo(Array array, int index) {
		((ICollection)Items).CopyTo(array, index);
	}

	int IList.Add(object? value) {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertType(value, nameof(value));
		AssertIsOnDispatcherThread();
		return ((IList)collection).Add(value);
	}

	bool IList.Contains(object? value) {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertType(value, nameof(value));
		AssertIsOnDispatcherThread();
		return ((IList)collection).Contains(value);
	}

	void IList.Clear() {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertIsOnDispatcherThread();
		((IList)collection).Clear();
	}

	int IList.IndexOf(object? value) {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertType(value, nameof(value));
		AssertIsOnDispatcherThread();
		return Items.IndexOf((T)value!);
	}

	void IList.Insert(int index, object? value) {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertType(value, nameof(value));
		AssertIsOnDispatcherThread();
		((IList)collection).Insert(index, value);
	}

	void IList.Remove(object? value) {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertType(value, nameof(value));
		AssertIsOnDispatcherThread();
		((IList)collection).Remove(value);
	}

	void IList.RemoveAt(int index) {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertIsOnDispatcherThread();
		((IList)collection).RemoveAt(index);
	}
}
