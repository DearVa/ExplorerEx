using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ExplorerEx.Utils;
using ExplorerEx.View.Controls;
using ExplorerEx.Win32;
using static ExplorerEx.Win32.IconHelper;

namespace ExplorerEx.Model;

public class FileSystemItem : FileViewBaseItem {
	public FileSystemInfo FileSystemInfo { get; private set; }

	public DateTime LastWriteTime => FileSystemInfo.LastWriteTime;

	/// <summary>
	/// 类型
	/// </summary>
	public override string Type => IsFolder ? (isEmptyFolder ? "Empty_folder".L() : "Folder".L()) : GetFileTypeDescription(Path.GetExtension(FileSystemInfo.Name));

	public string FileSizeString => FileUtils.FormatByteSize(FileSize);

	public override string FullPath {
		get => FileSystemInfo.FullName;
		protected set {
			if (Directory.Exists(value)) {
				FileSystemInfo = new DirectoryInfo(value);
			} else if (File.Exists(value)) {
				FileSystemInfo = new FileInfo(value);
			} else {
				throw new FileNotFoundException(value);
			}
		}
	}

	/// <summary>
	/// 是否使用大图标
	/// </summary>
	public bool UseLargeIcon { get; set; }

	private bool isEmptyFolder;

	public FileSystemItem(FileSystemInfo fileSystemInfo) {
		FileSystemInfo = fileSystemInfo;
		Name = FileSystemInfo.Name;
		if (fileSystemInfo is FileInfo fi) {
			FileSize = fi.Length;
			IsFolder = false;
			Icon = UnknownTypeFileDrawingImage;
		} else {
			FileSize = -1;
			IsFolder = true;
			LoadDirectoryIcon();
		}
	}

	private void LoadDirectoryIcon() {
		try {
			isEmptyFolder = FolderUtils.IsEmptyFolder(FileSystemInfo.FullName);
			if (isEmptyFolder) {
				Icon = EmptyFolderDrawingImage;
			} else {
				Icon = FolderDrawingImage;
			}
		} catch {
			Icon = EmptyFolderDrawingImage;
		}
	}

	public override async Task LoadIconAsync() {
		Debug.Assert(!IsFolder);
		if (UseLargeIcon) {
			Icon = await Task.Run(() => GetPathThumbnailAsync(FullPath));
		} else {
			Icon = await Task.Run(() => GetPathIconAsync(FullPath, false));
		}
	}

	protected override bool Rename() {
		if (EditingName == null) {
			return false;
		}
		var basePath = Path.GetDirectoryName(FullPath);
		if (Path.GetExtension(FullPath) != Path.GetExtension(EditingName)) {
			if (!MessageBoxHelper.AskWithDefault("RenameExtension", "Are_you_sure_to_change_extension".L())) {
				return false;
			}
		}
		try {
			FileUtils.FileOperation(Win32Interop.FileOpType.Rename, FullPath, Path.Combine(basePath!, EditingName!));
			return true;
		} catch (Exception e) {
			Logger.Exception(e);
		}
		return false;
	}

	public async Task RefreshAsync() {
		if (IsFolder) {
			LoadDirectoryIcon();
		} else {
			await LoadIconAsync();
			PropertyUpdateUI(nameof(FileSize));
		}
		PropertyUpdateUI(nameof(Icon));
	}
}