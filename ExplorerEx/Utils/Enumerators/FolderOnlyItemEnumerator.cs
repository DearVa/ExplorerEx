using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using ExplorerEx.Models;

namespace ExplorerEx.Utils.Enumerators;

/// <summary>
/// 感谢微软大爹留接口，谢谢微软爹，谢谢微软爹
/// </summary>
internal class FolderOnlyItemEnumerator : FileSystemEnumerator<FolderOnlyItem>, IEnumerable<FolderOnlyItem> {
	private readonly FolderOnlyItem parent;

	public FolderOnlyItemEnumerator(string directory, in FolderOnlyItem parent) : base(directory, GetEnumerationOptions()) {
		this.parent = parent;
	}

	private static EnumerationOptions GetEnumerationOptions() {
		var options = new EnumerationOptions();
		if (Settings.Current[Settings.CommonSettings.ShowHiddenFilesAndFolders].AsBoolean()) {
			options.AttributesToSkip ^= FileAttributes.Hidden;
		}
		if (Settings.Current[Settings.CommonSettings.ShowProtectedSystemFilesAndFolders].AsBoolean()) {
			options.AttributesToSkip ^= FileAttributes.System;
		}
		return options;
	}

	protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) {
		if (entry.IsDirectory) {
			return true;
		}
		if (entry.FileName.EndsWith(".zip")) {
			return true;
		}
		return false;
	}

	protected override FolderOnlyItem TransformEntry(ref FileSystemEntry entry) {
		if (entry.IsDirectory) {
			return new FolderOnlyItem(new DirectoryInfo(entry.ToFullPath()), parent);  // TODO：或许可以直接用entry？
		}
		return new FolderOnlyItem(entry.ToFullPath(), string.Empty, parent);
	}

	public IEnumerator<FolderOnlyItem> GetEnumerator() => this;

	IEnumerator IEnumerable.GetEnumerator() => this;
}