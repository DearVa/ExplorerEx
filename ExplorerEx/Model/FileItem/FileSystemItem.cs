using System;
using System.Diagnostics;
using System.IO;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using static ExplorerEx.Shell32.IconHelper;

namespace ExplorerEx.Model;

public class FileSystemItem : FileViewBaseItem {
	/// <summary>
	/// 将已经加载过的Folder判断完是否为空缓存下来
	/// </summary>
	private static readonly LimitedDictionary<string, bool> IsEmptyFolderDictionary = new(10243);

	public FileSystemInfo FileSystemInfo { get; private set; }

	/// <summary>
	/// 自动更新UI
	/// </summary>
	public DateTime ModificationDate {
		get => modificationDate;
		private set {
			if (modificationDate != value) {
				modificationDate = value;
				UpdateUI();
			}
		}
	}

	private DateTime modificationDate;

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
	public bool IsExecutable => !IsFolder && Path.GetExtension(Name) is ".exe" or ".com" or ".cmd" or ".bat";

	/// <summary>
	/// 是否为文本文件
	/// </summary>
	public bool IsEditable => !IsFolder && Path.GetExtension(Name) is ".txt" or ".log" or ".ini" or ".inf" or ".cmd" or ".bat" or ".ps1";

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
		Name = fileInfo.Name;
		IsFolder = false;
		FileSize = -1;
		Icon = UnknownFileDrawingImage;
		EditCommand = new SimpleCommand(_ => OpenWith(Settings.Instance.TextEditor));
	}

	public FileSystemItem(DirectoryInfo directoryInfo) {
		FileSystemInfo = directoryInfo;
		Name = directoryInfo.Name;
		IsFolder = true;
		FileSize = -1;
		if (IsEmptyFolderDictionary.TryGetValue(directoryInfo.FullName, out var isEmpty)) {
			isEmptyFolder = isEmpty;
			Icon = isEmpty ? EmptyFolderDrawingImage : FolderDrawingImage;
		} else {
			Icon = FolderDrawingImage;
		}
	}

	public override void LoadAttributes() {
		if (FileSystemInfo == null) {
			return;
		}
		if (IsFolder) {
			isEmptyFolder = FolderUtils.IsEmptyFolder(FileSystemInfo.FullName);
			Type = isEmptyFolder ? "Empty_folder".L() : "Folder".L();
			IsEmptyFolderDictionary.Add(FullPath, isEmptyFolder);
		} else {
			var type = FileUtils.GetFileTypeDescription(Path.GetExtension(Name));
			if (string.IsNullOrEmpty(type)) {
				Type = "Unknown_type".L();
			} else {
				Type = type;
			}
			FileSize = ((FileInfo)FileSystemInfo).Length;
		}
		ModificationDate = FileSystemInfo.LastWriteTime;
		CreationTime = FileSystemInfo.CreationTime;
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

	public override void StartRename() {
		EditingName = Name;
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
			FileUtils.FileOperation(FileOpType.Rename, FullPath, Path.Combine(basePath!, EditingName!));
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