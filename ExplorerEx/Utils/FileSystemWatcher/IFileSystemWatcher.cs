using System;
using System.IO;

namespace ExplorerEx.Utils;

/// <summary>
/// 用于监视一个文件系统项的变化，通常是文件夹
/// </summary>
public interface IFileSystemWatcher : IDisposable {
	string Path { get; set; }

	bool Enabled { get; set; }

	Func<string, bool>? Filter { get; set; }

	NotifyFilters NotifyFilter { get; set; }

	event Action<FileSystemChangeEvent> Changed;

	event Action<Exception> Error;
}