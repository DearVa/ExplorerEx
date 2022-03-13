using System;
using System.Diagnostics;
using System.IO;
using ExplorerEx.Utils;
using ExplorerEx.Win32;
using static ExplorerEx.Win32.IconHelper;

namespace ExplorerEx.Model;

public class FileSystemItem : FileViewBaseItem {
	public FileSystemInfo FileSystemInfo { get; private set; }

	public DateTime LastWriteTime => FileSystemInfo.LastWriteTime;

	/// <summary>
	/// 是否是可执行文件
	/// </summary>
	public bool IsRunnable => !IsFolder && Name[..^4] is ".exe" or ".com" or ".cmd" or ".bat";

	public bool IsEditable => !IsFolder && Name[..^4] is ".txt" or ".log" or ".ini" or ".inf" or ".cmd" or ".bat" or ".ps1";

	/// <summary>
	/// 点击“编辑”选项
	/// </summary>
	public SimpleCommand EditCommand { get; }

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

	public override string DisplayText => Name;

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
			EditCommand = new SimpleCommand(_ => OpenWith("notepad.exe"));
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

	public override void LoadIcon() {
		Debug.Assert(!IsFolder);
		if (UseLargeIcon) {
			Icon = GetPathThumbnail(FullPath);
		} else {
			Icon = GetPathIcon(FullPath, false);
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

	public void Refresh() {
		if (IsFolder) {
			LoadDirectoryIcon();
		} else {
			LoadIcon();
			PropertyUpdateUI(nameof(FileSize));
		}
		PropertyUpdateUI(nameof(Icon));
	}

	/// <summary>
	/// 用某应用打开此文件
	/// </summary>
	/// <param name="app"></param>
	public void OpenWith(string app) {
		try {
			Process.Start(new ProcessStartInfo {
				FileName = app,
				Arguments = FullPath,
				UseShellExecute = true
			});
		} catch (Exception e) {
			HandyControl.Controls.MessageBox.Error(e.Message, "Fail to open file".L());
		}
	}
}