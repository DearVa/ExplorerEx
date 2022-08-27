using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ExplorerEx.Utils;

/// <summary>
/// Changed事件是通过线程发起的，排在Queue里逐个调用
/// </summary>
public class ThreadedFileSystemWatcher : IFileSystemWatcher {
	public string Path {
		get => watcher.Path;
		set => watcher.Path = value;
	}

	public bool Enabled {
		get => watcher.EnableRaisingEvents;
		set => watcher.EnableRaisingEvents = value;
	}

	public Func<string, bool>? Filter { get; set; }

	public NotifyFilters NotifyFilter {
		get => watcher.NotifyFilter;
		set => watcher.NotifyFilter = value;
	}

	public event Action<FileSystemChangeEvent>? Changed;

	public event Action<Exception>? Error;
	
	private readonly ManualResetEvent waitHandle;
	private readonly CancellationTokenSource cts;
	private readonly ConcurrentQueue<FileSystemChangeEvent> changeQueue;
	private readonly FileSystemWatcher watcher;

	public ThreadedFileSystemWatcher() {
		changeQueue = new ConcurrentQueue<FileSystemChangeEvent>();
		waitHandle = new ManualResetEvent(false);
		cts = new CancellationTokenSource();
		Task.Run(WorkingTask);
		watcher = new FileSystemWatcher();
		watcher.Created += Watcher_OnCreated;
		watcher.Changed += Watcher_OnChanged;
		watcher.Renamed += Watcher_OnRenamed;
		watcher.Deleted += Watcher_OnDeleted;
		watcher.Error += Watcher_OnError;
	}

	private void WorkingTask() {
		while (true) {
			waitHandle.WaitOne();
			if (cts.IsCancellationRequested) {
				return;
			}
			waitHandle.Reset();
			if (Changed == null) {
				changeQueue.Clear();
				continue;
			}
			while (changeQueue.TryDequeue(out var change)) {
				Changed.Invoke(change);
			}
		}
	}

	private void EnqueueEvent(WatcherChangeTypes changeType, string fullPath) {
		changeQueue.Enqueue(new FileSystemChangeEvent(changeType, fullPath));
		waitHandle.Set();
	}

	private void Watcher_OnCreated(object sender, FileSystemEventArgs e) {
		if (Changed != null && (Filter == null || Filter.Invoke(e.FullPath))) {
			EnqueueEvent(WatcherChangeTypes.Created, e.FullPath);
		}
	}

	private void Watcher_OnChanged(object sender, FileSystemEventArgs e) {
		if (Changed != null && (Filter == null || Filter.Invoke(e.FullPath))) {
			EnqueueEvent(e.ChangeType, e.FullPath);
		}
	}

	private void Watcher_OnRenamed(object sender, RenamedEventArgs e) {
		if (Changed != null && (Filter == null || Filter.Invoke(e.FullPath))) {
			EnqueueEvent(WatcherChangeTypes.Renamed, e.FullPath);
		}
	}

	private void Watcher_OnDeleted(object sender, FileSystemEventArgs e) {
		if (Changed != null && (Filter == null || Filter.Invoke(e.FullPath))) {
			EnqueueEvent(WatcherChangeTypes.Deleted, e.FullPath);
		}
	}

	private void Watcher_OnError(object sender, ErrorEventArgs e) {
		Error?.Invoke(e.GetException());
	}

	public void Dispose() {
		waitHandle.Dispose();
		cts.Dispose();
		watcher.Dispose();
	}
}