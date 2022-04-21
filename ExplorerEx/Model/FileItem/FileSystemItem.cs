using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using static ExplorerEx.Utils.IconHelper;
using File = System.IO.File;

namespace ExplorerEx.Model;

public abstract class FileSystemItem : FileListViewItem {
	/// <summary>
	/// 自动更新UI
	/// </summary>
	public DateTime DateModified {
		get => dateModified;
		protected set {
			if (dateModified != value) {
				dateModified = value;
				UpdateUI();
			}
		}
	}

	private DateTime dateModified;

	/// <summary>
	/// 自动更新UI
	/// </summary>
	public DateTime DateCreated {
		get => dateCreated;
		protected set {
			if (dateCreated != value) {
				dateCreated = value;
				UpdateUI();
			}
		}
	}

	private DateTime dateCreated;

	public override void StartRename() {
		EditingName = Name;
	}

	protected override bool Rename() {
		if (EditingName == null) {
			return false;
		}
		var basePath = Path.GetDirectoryName(FullPath);
		if (Path.GetExtension(FullPath) != Path.GetExtension(EditingName)) {
			if (!MessageBoxHelper.AskWithDefault("RenameExtension", "#AreYouSureToChangeExtension".L())) {
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
}

public class FileItem : FileSystemItem {
	public FileInfo FileInfo { get; }

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

	/// <summary>
	/// 是否使用大图标
	/// </summary>
	public bool UseLargeIcon { get; set; }

	protected FileItem() { }

	public FileItem(FileInfo fileInfo) {
		FileInfo = fileInfo;
		FullPath = fileInfo.FullName;
		Name = fileInfo.Name;
		IsFolder = false;
		FileSize = -1;
		Icon = UnknownFileDrawingImage;
	}

	public override void LoadAttributes() {
		if (FileInfo == null) {
			return;
		}
		var type = FileUtils.GetFileTypeDescription(Path.GetExtension(Name));
		if (string.IsNullOrEmpty(type)) {
			Type = "UnknownType".L();
		} else {
			Type = type;
		}
		FileSize = FileInfo.Length;
		DateModified = FileInfo.LastWriteTime;
		DateCreated = FileInfo.CreationTime;
	}

	public override void LoadIcon() {
		if (UseLargeIcon) {
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
	public static readonly HashSet<Func<string, (FolderItem, string, PathType)>> PathParsers = new();

	/// <summary>
	/// 将已经加载过的Folder判断完是否为空缓存下来
	/// </summary>
	private static readonly LimitedDictionary<string, bool> IsEmptyFolderDictionary = new(10243);

	/// <summary>
	/// 将所给的path解析为FolderItem对象，如果出错抛出异常，如果没有出错，但格式不支持，返回null
	/// </summary>
	/// <param name="path"></param>
	/// <returns></returns>
	public static (FolderItem, string, PathType) ParsePath(string path) {
		path = path?.Trim();
		if (string.IsNullOrEmpty(path) || path == "ThisPC".L() || path.ToUpper() is "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" or "::{5E5F29CE-E0A8-49D3-AF32-7A7BDC173478}") {  // 加载“此电脑”
			return (HomeFolderItem.Instance, null, PathType.Home);
		}
		path = path.Replace('/', '\\');
		if (path.Length >= 3) {
			if (path[0] is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') && path[1] == ':') {  // 以驱动器作为开头，表示是本地的目录
				if (path.Length == 2 || (path.Length == 3 && path[2] == '\\')) {  // 长度为2或3，表示本地驱动器根目录 TODO: 可能为映射的网络驱动器
					if (Directory.Exists(path)) {
						return (new DiskDriveItem(new DriveInfo(path[..1])), path, PathType.LocalFolder);
					}
					throw new IOException("#PathNotExistOrAccessDenied".L());
				}
				// 本地的一个文件地址
				var zipIndex = path.IndexOf(@".zip\", StringComparison.CurrentCulture);
				if (zipIndex == -1) { // 没找到.zip\，不是zip文件
					if (Directory.Exists(path)) {
						return (new FolderItem(path), path, PathType.LocalFolder);
					}
					if (File.Exists(path)) {
						return (null, null, PathType.LocalFile);
					}
					throw new IOException("#PathNotExistOrAccessDenied".L());
				}
				if (path[^1] != '\\') {
					throw new IOException("#ZipMustEndsWithSlash".L());
				}
				return (new ZipFolderItem(path, path[..(zipIndex + 4)], path[(zipIndex + 5)..]), null, PathType.Zip);
			}
		}
		// ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
		foreach (var pathParser in PathParsers) {
			var result = pathParser.Invoke(path);
			if (result.Item1 != null) {
				return result;
			}
		}
		return (null, null, PathType.Unknown);
	}


	private bool isEmptyFolder;

	protected FolderItem() { }

	public FolderItem(string fullPath) {
		FullPath = fullPath;
		Name = Path.GetFileName(fullPath);
		IsFolder = true;
		FileSize = -1;
		if (IsEmptyFolderDictionary.TryGetValue(fullPath, out var isEmpty)) {
			isEmptyFolder = isEmpty;
			Icon = isEmpty ? EmptyFolderDrawingImage : FolderDrawingImage;
		} else {
			Icon = FolderDrawingImage;
		}
	}

	public override string DisplayText => Name;

	public override void LoadAttributes() {
		isEmptyFolder = FolderUtils.IsEmptyFolder(FullPath);
		Type = isEmptyFolder ? "EmptyFolder".L() : "Folder".L();
		IsEmptyFolderDictionary.Add(FullPath, isEmptyFolder);
		var directoryInfo = new DirectoryInfo(FullPath);
		DateModified = directoryInfo.LastWriteTime;
		DateCreated = directoryInfo.CreationTime;
	}

	public override void LoadIcon() {
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
	/// <param name="selectedItem"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	/// <exception cref="NotImplementedException"></exception>
	public virtual List<FileListViewItem> EnumerateItems(string selectedPath, out FileListViewItem selectedItem, CancellationToken token) {
		selectedItem = null;
		var list = new List<FileListViewItem>();
		foreach (var directoryPath in Directory.EnumerateDirectories(FullPath)) {
			if (token.IsCancellationRequested) {
				return null;
			}
			var item = new FolderItem(directoryPath);
			list.Add(item);
			if (directoryPath == selectedPath) {
				item.IsSelected = true;
				selectedItem = item;
			}
		}
		foreach (var filePath in Directory.EnumerateFiles(FullPath)) {
			if (token.IsCancellationRequested) {
				return null;
			}
			var item = new FileItem(new FileInfo(filePath));
			list.Add(item);
			if (filePath == selectedPath) {
				item.IsSelected = true;
				selectedItem = item;
			}
		}
		return list;
	}
}
