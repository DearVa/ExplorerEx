using System.IO;

namespace ExplorerEx.Utils;

public record FileSystemChangeEvent(WatcherChangeTypes ChangeType, string FullPath) {
	public WatcherChangeTypes ChangeType { get; } = ChangeType;

	/// <summary>
	/// 发生更改的文件的完整路径
	/// </summary>
	public string FullPath { get; } = FullPath;
}