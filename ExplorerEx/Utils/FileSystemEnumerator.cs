using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using ExplorerEx.Model;
using static ExplorerEx.Model.FileListViewItem;

namespace ExplorerEx.Utils;

/// <summary>
/// 感谢微软大爹留接口，谢谢微软爹，谢谢微软爹
/// </summary>
internal class FileSystemEnumerator : FileSystemEnumerator<FileSystemItem>, IEnumerable<FileSystemItem> {
	private readonly LoadDetailsOptions loadOptions;

	public FileSystemEnumerator(string directory, in LoadDetailsOptions loadOptions) : base(directory, GetEnumerationOptions()) {
		this.loadOptions = loadOptions;
	}

	private static EnumerationOptions GetEnumerationOptions() {
		var options = new EnumerationOptions();
		if (Settings.Current[Settings.CommonSettings.ShowHiddenFilesAndFolders].GetBoolean()) {
			options.AttributesToSkip ^= FileAttributes.Hidden;
		}
		if (Settings.Current[Settings.CommonSettings.ShowProtectedSystemFilesAndFolders].GetBoolean()) {
			options.AttributesToSkip ^= FileAttributes.System;
		}
		return options;
	}

	protected override FileSystemItem TransformEntry(ref FileSystemEntry entry) {
		if (entry.IsDirectory) {
			return new FolderItem(new DirectoryInfo(entry.ToFullPath()), loadOptions);  // TODO：或许可以直接用entry？
		}
		return new FileItem(new FileInfo(entry.ToFullPath()), loadOptions);
	}

	public IEnumerator<FileSystemItem> GetEnumerator() => this;

	IEnumerator IEnumerable.GetEnumerator() => this;
}