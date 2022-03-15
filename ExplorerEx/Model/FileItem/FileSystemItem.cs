using System;
using System.Diagnostics;
using System.IO;
using ExplorerEx.Utils;
using ExplorerEx.Win32;
using static ExplorerEx.Win32.IconHelper;

namespace ExplorerEx.Model;

public class FileSystemItem : FileViewBaseItem {
	public FileSystemInfo FileSystemInfo { get; private set; }

	/// <summary>
	/// 自动更新UI
	/// </summary>
	public DateTime LastWriteTime {
		get => lastWriteTime;
		private set {
			if (lastWriteTime != value) {
				lastWriteTime = value;
				UpdateUI();
			}
		}
	}

	private DateTime lastWriteTime;

	/// <summary>
	/// 自动更新UI
	/// </summary>
	public DateTime CreationTime {
		get => creationTime;
		private set {
			if (creationTime != value) {
				creationTime = value;
				UpdateUI();
			}
		}
	}

	private DateTime creationTime;

	/// <summary>
	/// 是否是可执行文件
	/// </summary>
	public bool IsRunnable => !IsFolder && Name[^4..] is ".exe" or ".com" or ".cmd" or ".bat";

	public bool IsEditable => !IsFolder && Name[^4..] is ".txt" or ".log" or ".ini" or ".inf" or ".cmd" or ".bat" or ".ps1";

	/// <summary>
	/// 点击“编辑”选项
	/// </summary>
	public SimpleCommand EditCommand { get; }

	public override string FullPath {
		get => FileSystemInfo.FullName;
		protected set {
			if (Directory.Exists(value)) {
				FileSystemInfo = new DirectoryInfo(value);
			} else if (File.Exists(value)) {
				FileSystemInfo = new FileInfo(value);
			} else {
				FileSystemInfo = null;
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

	public FileSystemItem(FileInfo fileInfo) {
		FileSystemInfo = fileInfo;
		Name = FileSystemInfo.Name;
		IsFolder = false;
		FileSize = -1;
		Icon = UnknownTypeFileDrawingImage;
		EditCommand = new SimpleCommand(_ => OpenWith(Settings.Instance.TextEditor));
	}

	public FileSystemItem(DirectoryInfo directoryInfo) {
		FileSystemInfo = directoryInfo;
		Name = FileSystemInfo.Name;
		IsFolder = true;
		FileSize = -1;
		Icon = EmptyFolderDrawingImage;
	}

	public override void LoadAttributes() {
		if (FileSystemInfo == null) {
			return;
		}
		if (IsFolder) {
			isEmptyFolder = FolderUtils.IsEmptyFolder(FileSystemInfo.FullName);
			Type = isEmptyFolder ? "Empty_folder".L() : "Folder".L();
		} else {
			Type = GetFileTypeDescription(Path.GetExtension(FileSystemInfo.Name));
			LastWriteTime = FileSystemInfo.LastWriteTime;
			CreationTime = FileSystemInfo.CreationTime;
			FileSize = ((FileInfo)FileSystemInfo).Length;
		}
	}

	public override void LoadIcon() {
		if (IsFolder) {
			if (isEmptyFolder) {
				Icon = EmptyFolderDrawingImage;
			} else {
				Icon = FolderDrawingImage;
			}
		} else {
			if (UseLargeIcon) {
				Icon = GetPathThumbnail(FullPath);
			} else {
				Icon = GetPathIcon(FullPath, false);
			}
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
		if (!IsFolder) {
			LoadIcon();
		}
		UpdateUI(nameof(Icon));
		LoadAttributes();
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