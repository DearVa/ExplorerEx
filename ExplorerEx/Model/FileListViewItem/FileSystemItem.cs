using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.Utils.Collections;
using ExplorerEx.View.Controls;
using static ExplorerEx.Utils.IconHelper;
using File = System.IO.File;

namespace ExplorerEx.Model;

public abstract class FileSystemItem : FileListViewItem {
	protected FileSystemInfo? FileSystemInfo { get; }

	/// <summary>
	/// 自动更新UI
	/// </summary>
	public DateTime DateModified {
		get => dateModified;
		protected set {
			if (dateModified != value) {
				dateModified = value;
				OnPropertyChanged();
			}
		}
	}

	private DateTime dateModified = DateTime.MaxValue;

	/// <summary>
	/// 自动更新UI
	/// </summary>
	public DateTime DateCreated {
		get => dateCreated;
		protected set {
			if (dateCreated != value) {
				dateCreated = value;
				OnPropertyChanged();
			}
		}
	}

	private DateTime dateCreated = DateTime.MaxValue;

	public override string GetRenameName() {
		return Name;
	}

	protected override void InternalRename(string newName) {
		var basePath = Path.GetDirectoryName(FullPath);
		if (basePath == null) {
			throw new InvalidOperationException();
		}
		if (Path.GetExtension(FullPath) != Path.GetExtension(newName)) {
			if (!ContentDialog.ShowWithDefault(Settings.CommonSettings.DontAskWhenChangeExtension, "#AreYouSureToChangeExtension".L())) {
				return;
			}
		}
		File.Move(FullPath, Path.Combine(basePath, newName), false);
	}

	/// <summary>
	/// 重新加载图标和详细信息
	/// </summary>
	public void Refresh() {
		LoadIcon();
		LoadAttributes();
	}

	/// <summary>
	/// 用于虚拟文件或者文件夹，继承的类初始化
	/// </summary>
	/// <param name="isFolder"></param>
	protected FileSystemItem(bool isFolder) : base(isFolder, LoadDetailsOptions.Default) { }

	protected FileSystemItem(string fullPath, string name, bool isFolder, LoadDetailsOptions options) : base(fullPath, name, isFolder, options) { }

	protected FileSystemItem(string fullPath, string name, ImageSource defaultIcon, LoadDetailsOptions options) : base(fullPath, name, defaultIcon, options) { }

	protected FileSystemItem(FileSystemInfo fileSystemInfo, bool isFolder, LoadDetailsOptions options) : base(fileSystemInfo.FullName, fileSystemInfo.Name, isFolder, options) {
		FileSystemInfo = fileSystemInfo;
	}

	protected FileSystemItem(FileSystemInfo fileSystemInfo, ImageSource defaultIcon, LoadDetailsOptions options) : base(fileSystemInfo.FullName, fileSystemInfo.Name, defaultIcon, options) {
		FileSystemInfo = fileSystemInfo;
	}
}

public class FileItem : FileSystemItem {
	/// <summary>
	/// 是否是可执行文件
	/// </summary>
	public bool IsExecutable => FileUtils.IsExecutable(FullPath);

	/// <summary>
	/// 是否为文本文件
	/// </summary>
	public bool IsEditable => FullPath[^4..] is ".txt" or ".log" or ".ini" or ".inf" or ".cmd" or ".bat" or ".ps1";

	public bool IsZip => FullPath[^4..] == ".zip";

	/// <summary>
	/// 是否为.lnk文件
	/// </summary>
	public bool IsLink => FullPath[^4..] == ".lnk";

	public override string DisplayText => Name;

	protected FileItem() : base(false) { }

	public FileItem(FileInfo fileInfo, LoadDetailsOptions options) : base(fileInfo, false, options) {
		FileSize = -1;
	}

	protected override void LoadAttributes() {
		if (FileSystemInfo == null) {
			return;
		}
		FileSystemInfo.Refresh();
		var type = FileUtils.GetFileTypeDescription(FileSystemInfo.Extension);
		if (string.IsNullOrEmpty(type)) {
			Type = "UnknownType".L();
		} else {
			Type = type;
		}
		FileSize = ((FileInfo)FileSystemInfo).Length;
		DateModified = FileSystemInfo.LastWriteTime;
		DateCreated = FileSystemInfo.CreationTime;
		if (FileSystemInfo.Attributes.HasFlag(FileAttributes.Hidden)) {
			Opacity = 0.5d;
		}
	}

	protected override void LoadIcon() {
		if (Options.UseLargeIcon) {
			Icon = GetPathThumbnail(FullPath);
		} else {
			Icon = GetSmallIcon(FullPath, false);
		}
	}
}

public class FolderItem : FileSystemItem {
	/// <summary>
	/// 注册的路径解析器
	/// </summary>
	public static readonly HashSet<Func<string, (FolderItem?, PathType)>> PathParsers = new();

	/// <summary>
	/// 将已经加载过的Folder判断完是否为空缓存下来
	/// </summary>
	private static readonly LimitedDictionary<string, bool> IsEmptyFolderDictionary = new(10243);

	/// <summary>
	/// 将所给的path解析为FolderItem对象，如果出错抛出异常，如果没有出错，但格式不支持，返回null
	/// </summary>
	/// <param name="path"></param>
	/// <returns></returns>
	public static (FolderItem?, PathType) ParsePath(string path) {
		// TODO: Shell位置解析
		if (path == "ThisPC".L() || path.ToUpper() is "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" or "::{5E5F29CE-E0A8-49D3-AF32-7A7BDC173478}") {  // 加载“此电脑”
			return (HomeFolderItem.Singleton, PathType.Home);
		}
		path = Environment.ExpandEnvironmentVariables(path).Replace('/', '\\').TrimEnd('\\');
		if (path.Length >= 2) {
			if (path[0] is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') && path[1] == ':') {  // 以驱动器作为开头，表示是本地的目录
				if (path.Length == 2) {  // 长度为2，表示本地驱动器根目录 TODO: 可能为映射的网络驱动器
					if (Directory.Exists(path)) {
						return (new DiskDriveItem(new DriveInfo(path[..1])), PathType.LocalFolder);
					}
					throw new IOException("#PathNotExistOrAccessDenied".L());
				}
				// 本地的一个文件地址
				var zipIndex = path.IndexOf(".zip", StringComparison.CurrentCulture);
				if (zipIndex == -1) { // 没找到.zip，不是zip文件
					if (Directory.Exists(path)) {
						return (new FolderItem(new DirectoryInfo(path), LoadDetailsOptions.Default), PathType.LocalFolder);
					}
					if (File.Exists(path)) {
						return (null, PathType.LocalFile);
					}
					throw new IOException("#PathNotExistOrAccessDenied".L());
				}
				return (new ZipFolderItem(path, path[..(zipIndex + 4)]), PathType.Zip);
			}
		}
		return PathParsers.Select(pathParser => pathParser.Invoke(path)).FirstOrDefault(result => result.Item1 != null, (null, PathType.Unknown));
	}

	public bool IsReadonly { get; protected init; }

	/// <summary>
	/// 是否为虚拟文件夹，如主页就是
	/// </summary>
	public bool IsVirtual { get; protected init; }

	private bool isEmptyFolder;

	protected FolderItem() : base(true) { }

	public FolderItem(DirectoryInfo directoryInfo, LoadDetailsOptions options) : base(directoryInfo, InitializeIsEmptyFolder(directoryInfo.FullName), options) {
		IsFolder = true;
		FileSize = -1;
	}

	protected static ImageSource InitializeIsEmptyFolder(string fullPath) {
		if (IsEmptyFolderDictionary.TryGetValue(fullPath, out var isEmpty)) {
			return isEmpty ? EmptyFolderDrawingImage : FolderDrawingImage;
		}
		return FolderDrawingImage;
	}

	public override string DisplayText => Name;

	protected override void LoadAttributes() {
		if (FileSystemInfo == null) {
			return;
		}
		isEmptyFolder = FolderUtils.IsEmptyFolder(FullPath);
		IsEmptyFolderDictionary.Add(FullPath, isEmptyFolder);
		Type = isEmptyFolder ? "EmptyFolder".L() : "Folder".L();
		var directoryInfo = (DirectoryInfo)FileSystemInfo;
		directoryInfo.Refresh();
		DateModified = directoryInfo.LastWriteTime;
		DateCreated = directoryInfo.CreationTime;
		if (directoryInfo.Attributes.HasFlag(FileAttributes.Hidden)) {
			Opacity = 0.5d;
		}
	}

	protected override void LoadIcon() {
		if (isEmptyFolder) {
			Icon = EmptyFolderDrawingImage;
		} else {
			Icon = FolderDrawingImage;
		}
	}

	/// <summary>
	/// 枚举当前目录下的文件项
	/// </summary>
	/// <param name="selectedPath">筛选选中的项</param>
	/// <param name="options"></param>
	/// <param name="selectedItem"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	/// <exception cref="NotImplementedException"></exception>
	public virtual List<FileListViewItem> EnumerateItems(string? selectedPath, in LoadDetailsOptions options, out FileListViewItem? selectedItem, CancellationToken token) {
		var showHidden = Settings.Current[Settings.CommonSettings.ShowHiddenFilesAndFolders].GetBoolean();
		var showSystem = Settings.Current[Settings.CommonSettings.ShowProtectedSystemFilesAndFolders].GetBoolean();

		selectedItem = null;
		var list = new List<FileListViewItem>();
		foreach (var directoryPath in Directory.EnumerateDirectories(FullPath)) {
			if (token.IsCancellationRequested) {
				return list;
			}
			var di = new DirectoryInfo(directoryPath);
			//if (di.Attributes.HasFlag(FileAttributes.System) && !showSystem) {
			//	continue;
			//}
			//if (di.Attributes.HasFlag(FileAttributes.Hidden) && !showHidden) {
			//	continue;
			//}
			var item = new FolderItem(di, options);
			list.Add(item);
			if (directoryPath == selectedPath) {
				item.IsSelected = true;
				selectedItem = item;
			}
		}
		foreach (var filePath in Directory.EnumerateFiles(FullPath)) {
			if (token.IsCancellationRequested) {
				return list;
			}
			var fi = new FileInfo(filePath);
			//if (fi.Attributes.HasFlag(FileAttributes.System) && !showSystem) {
			//	continue;
			//}
			//if (fi.Attributes.HasFlag(FileAttributes.Hidden) && !showHidden) {
			//	continue;
			//}
			var item = new FileItem(fi, options);
			list.Add(item);
			if (filePath == selectedPath) {
				item.IsSelected = true;
				selectedItem = item;
			}
		}
		return list;
	}
}

/// <summary>
/// 表示Shell中的特殊文件夹，他有自己的CSIDL并且可以获取IdList
/// </summary>
public interface ISpecialFolder {
	CSIDL Csidl { get; }

	IntPtr IdList { get; }
}
