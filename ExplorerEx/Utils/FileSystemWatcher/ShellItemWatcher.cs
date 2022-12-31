using System;
using System.IO;

namespace ExplorerEx.Utils;

/// <summary>
/// 监视一个Shell项目的变化
/// </summary>
public class ShellItemWatcher : IFileSystemWatcher {
	public string? Path { get; set; }
	public bool Enabled { get; set; }
	public Func<string, bool>? Filter { get; set; }
	public NotifyFilters NotifyFilter { get; set; }
	public event Action<FileSystemChangeEvent>? Changed;
	public event Action<Exception>? Error;
	public void Dispose() {
		throw new NotImplementedException();
	}
}