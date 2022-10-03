using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.Utils.Collections;
using ExplorerEx.Utils.Enumerators;

namespace ExplorerEx.Model;

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

	protected FolderItem(ImageSource? defaultIcon) : base(defaultIcon) {
		IsFolder = true;
	}

	public FolderItem(DirectoryInfo directoryInfo, LoadDetailsOptions options) : base(directoryInfo, InitializeIsEmptyFolder(directoryInfo.FullName), options) {
		IsFolder = true;
		FileSize = -1;
	}

	protected static ImageSource InitializeIsEmptyFolder(string fullPath) {
		if (IsEmptyFolderDictionary.TryGetValue(fullPath, out var isEmpty)) {
			return isEmpty ? IconHelper.EmptyFolderDrawingImage : IconHelper.FolderDrawingImage;
		}
		return IconHelper.FolderDrawingImage;
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
			Icon = IconHelper.EmptyFolderDrawingImage;
		} else {
			Icon = IconHelper.FolderDrawingImage;
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
		selectedItem = null;
		var list = new List<FileListViewItem>();
		// 谢谢微软喵！谢谢微软喵！谢谢微软喵！谢谢微软喵！谢谢微软喵！谢谢微软喵！谢谢微软喵！谢谢微软喵！谢谢微软喵！
		foreach (var fileSystemItem in new FileSystemItemEnumerator(FullPath, options)) {
			if (token.IsCancellationRequested) {
				return list;
			}
			list.Add(fileSystemItem);
			if (fileSystemItem.FullPath == selectedPath) {
				fileSystemItem.IsSelected = true;
				selectedItem = fileSystemItem;
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