using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ExplorerEx.Utils;
using ExplorerEx.Win32;
using static ExplorerEx.Win32.IconHelper;

namespace ExplorerEx.Model;

internal class FileSystemItem : FileViewBaseItem {
	public FileSystemInfo FileSystemInfo { get; }

	public DateTime LastWriteTime => FileSystemInfo.LastWriteTime;

	public string FileSizeString => FileUtils.FormatByteSize(FileSize);

	public string FullPath => FileSystemInfo.FullName;

	public FileSystemItem(FileSystemInfo fileSystemInfo) {
		FileSystemInfo = fileSystemInfo;
		Name = FileSystemInfo.Name;
		if (fileSystemInfo is FileInfo fi) {
			FileSize = fi.Length;
			IsDirectory = false;
			Icon = UnknownTypeFileDrawingImage;
		} else {
			FileSize = -1;
			IsDirectory = true;
			try {
				if (Win32Interop.PathIsDirectoryEmpty(fileSystemInfo.FullName)) {
					Icon = FolderDrawingImage;
				} else {
					Icon = EmptyFolderDrawingImage;
				}
			} catch {
				Icon = EmptyFolderDrawingImage;
			}
		}
	}

	public override async Task LoadIconAsync() {
		Debug.Assert(!IsDirectory);
		Icon = await GetPathIconAsync(FullPath, false, true, false);
	}
}