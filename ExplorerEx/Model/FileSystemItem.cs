using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ExplorerEx.Utils;
using ExplorerEx.Win32;

namespace ExplorerEx.Model;

internal class FileSystemItem : FileViewBaseItem {
	public FileSystemInfo FileSystemInfo { get; }

	public DateTime LastWriteTime => FileSystemInfo.LastWriteTime;

	public string FileSizeString => FileUtils.FormatByteSize(FileSize);

	public string FullPath => FileSystemInfo.FullName;

	private static DrawingImage folderDrawingImage, emptyFolderDrawingImage, unknownTypeFileDrawingImage;

	public FileSystemItem(FileSystemInfo fileSystemInfo) {
		if (folderDrawingImage == null) {
			Application.Current.Dispatcher.Invoke(() => {
				var resources = Application.Current.Resources;
				folderDrawingImage = (DrawingImage)resources["FolderDrawingImage"];
				emptyFolderDrawingImage = (DrawingImage)resources["EmptyFolderDrawingImage"];
				unknownTypeFileDrawingImage = (DrawingImage)resources["UnknownTypeFileDrawingImage"];
			});
		}

		FileSystemInfo = fileSystemInfo;
		Name = FileSystemInfo.Name;
		if (fileSystemInfo is FileInfo fi) {
			FileSize = fi.Length;
			IsDirectory = false;
			Icon = unknownTypeFileDrawingImage;
		} else {
			FileSize = -1;
			IsDirectory = true;
			try {
				if (Win32Interop.PathIsDirectoryEmpty(fileSystemInfo.FullName)) {
					Icon = folderDrawingImage;
				} else {
					Icon = emptyFolderDrawingImage;
				}
			} catch {
				Icon = emptyFolderDrawingImage;
			}
		}
	}

	public override async Task LoadIconAsync() {
		Debug.Assert(!IsDirectory);
		//Icon = await Win32Interop.ExtractAssociatedIconAsync(FullPath);
		Icon = IconHelper.GetPathIcon(FullPath, false, true, false);
	}
}