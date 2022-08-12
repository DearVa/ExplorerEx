using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Threading;

namespace ExplorerEx.Utils.Collections.Internals;

public sealed class DispatchedObservableHashSet<T> : ObservableHashSetBase<T>, IReadOnlyObservableHashSet<T>, ISet<T> {
	private readonly ConcurrentQueue<PendingEvent<T>> pendingEvents = new();
	private readonly ConcurrentObservableHashSet<T> set;
	private readonly Dispatcher dispatcher;

	private bool isDispatcherPending;

	public DispatchedObservableHashSet(ConcurrentObservableHashSet<T> set, Dispatcher dispatcher)
		: base(set) {
		this.set = set;
		this.dispatcher = dispatcher;
	}

	private void AssertIsOnDispatcherThread() {
		if (!dispatcher.CheckAccess()) {
			var currentThreadId = Environment.CurrentManagedThreadId;
			throw new InvalidOperationException("The set must be accessed from the dispatcher thread only. Current thread ID: " + currentThreadId.ToString(CultureInfo.InvariantCulture));
		}
	}

	bool ICollection<T>.Remove(T item) {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertIsOnDispatcherThread();
		return set.Remove(item);
	}

	public int Count {
		get {
			AssertIsOnDispatcherThread();
			return Items.Count;
		}
	}

	bool ICollection<T>.IsReadOnly => false;

	public IEnumerator<T> GetEnumerator() {
		AssertIsOnDispatcherThread();
		return Items.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public void CopyTo(T[] array, int arrayIndex) {
		Items.CopyTo(array, arrayIndex);
	}

	void ICollection<T>.Add(T item) {
		// it will immediately modify both collections as we are on the dispatcher thread
		AssertIsOnDispatcherThread();
		set.Add(item);
	}

	void ISet<T>.ExceptWith(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	void ISet<T>.IntersectWith(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	bool ISet<T>.IsProperSubsetOf(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	bool ISet<T>.IsProperSupersetOf(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	bool ISet<T>.IsSubsetOf(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	bool ISet<T>.IsSupersetOf(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	bool ISet<T>.Overlaps(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	bool ISet<T>.SetEquals(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	void ISet<T>.SymmetricExceptWith(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	void ISet<T>.UnionWith(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	bool ISet<T>.Add(T item) {
		AssertIsOnDispatcherThread();
		return ((ISet<T>)set).Add(item);
	}

	void ICollection<T>.Clear() {
		AssertIsOnDispatcherThread();
		set.Clear();
	}

	public bool Contains(T item) {
		AssertIsOnDispatcherThread();
		return Items.Contains(item);
	}

	bool IReadOnlySet<T>.IsProperSubsetOf(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	bool IReadOnlySet<T>.IsProperSupersetOf(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	bool IReadOnlySet<T>.IsSubsetOf(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	bool IReadOnlySet<T>.IsSupersetOf(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	bool IReadOnlySet<T>.Overlaps(IEnumerable<T> other) {
		throw new NotImplementedException();
	}

	bool IReadOnlySet<T>.SetEquals(IEnumerable<T> other) {
		throw new NotImplementedException();
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

	internal void EnqueueClear() {
		EnqueueEvent(PendingEvent.Clear<T>());
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

			case PendingEventType.Reset:
				Reset(pendingEvent.Items!);
				break;
			}
		}
	}
}
