using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using ExplorerEx.Utils;

namespace ExplorerEx.Model;

public class ZipFileItem : FileItem {
	public string ZipPath { get; }

	public ZipArchive ZipArchive => zipArchiveEntry.Archive;

	private readonly ZipArchiveEntry zipArchiveEntry;
	private readonly string extension;

	public ZipFileItem(ZipArchiveEntry zipArchiveEntry, string zipPath) {
		ZipPath = zipPath;
		this.zipArchiveEntry = zipArchiveEntry;
		DateModified = zipArchiveEntry.LastWriteTime.DateTime;
		FullPath = zipPath + '\\' + zipArchiveEntry.FullName.Replace('/', '\\');
		Name = zipArchiveEntry.Name;
		FileSize = zipArchiveEntry.Length;
		extension = Path.GetExtension(zipArchiveEntry.Name);
	}

	protected override void LoadAttributes() {
		Type = FileUtils.GetFileTypeDescription(extension);
	}

	protected override void LoadIcon() {
		Icon = IconHelper.GetSmallIcon(extension, true);
	}

	/// <summary>
	/// 提取文件
	/// </summary>
	/// <param name="path">目标目录</param>
	/// <param name="relativePath">true表示按照相对目录解压，false表示直接解压到根目录</param>
	/// <returns>解压的路径</returns>
	public string Extract(string path, bool relativePath) {
		var destFilePath = relativePath ? Path.Combine(path, zipArchiveEntry.FullName.Replace('/', '\\')) : Path.Combine(path, zipArchiveEntry.Name);
		zipArchiveEntry.ExtractToFile(destFilePath);
		return destFilePath;
	}
}

public class ZipFolderItem : FolderItem, IDisposable {
	private readonly ZipArchive zipArchive;
	private readonly string zipPath;
	private readonly string relativePath;  // 不能以\结尾
	private bool hasItems;

	/// <summary>
	/// 初始化ZipFolderItem，仅供在在外部使用，一旦初始化就会打开一个文件流
	/// </summary>
	/// <param name="fullPath">一定以\结尾，如F:\test.zip\</param>
	/// <param name="zipPath">如F:\test.zip</param>
	public ZipFolderItem(string fullPath, string zipPath) : this(ZipFile.Open(zipPath, ZipArchiveMode.Read, Encoding.GetEncoding("gbk")), fullPath, zipPath) { }

	private ZipFolderItem(ZipArchiveEntry zipArchiveEntry, string fullPath, string zipPath) : this(zipArchiveEntry.Archive, fullPath, zipPath) {
		DateModified = zipArchiveEntry.LastWriteTime.DateTime;
	}

	/// <summary>
	/// fullPath形如F:\123.zip\444
	/// zipPath形如F:\123.zip
	/// </summary>
	/// <param name="zipArchive"></param>
	/// <param name="fullPath"></param>
	/// <param name="zipPath"></param>
	private ZipFolderItem(ZipArchive zipArchive, string fullPath, string zipPath) : base(null!, LoadDetailsOptions.Default) {
		Debug.Assert(fullPath.StartsWith(zipPath));
		this.zipArchive = zipArchive;
		FullPath = fullPath;
		relativePath = fullPath[zipPath.Length..].TrimStart('\\');
		Name = Path.GetFileName(relativePath == string.Empty ? zipPath : relativePath);
		IsFolder = true;
		FileSize = -1;
		Type = "Folder".L();
		this.zipPath = zipPath;
	}

	protected override void LoadAttributes() { }  // TODO

	protected override void LoadIcon() {
		if (relativePath == string.Empty) {
			Icon = IconHelper.GetSmallIcon(".zip", true);
		} else if (hasItems) {
			Icon = IconHelper.FolderDrawingImage;
		} else {
			Icon = IconHelper.EmptyFolderDrawingImage;
		}
	}

	public override List<FileListViewItem> EnumerateItems(string? selectedPath, in LoadDetailsOptions options, out FileListViewItem? selectedItem, CancellationToken token) {
		var showHidden = Settings.Current[Settings.CommonSettings.ShowHiddenFilesAndFolders].GetBoolean();
		var showSystem = Settings.Current[Settings.CommonSettings.ShowProtectedSystemFilesAndFolders].GetBoolean();

		selectedItem = null;
		var list = new List<FileListViewItem>();
		var slashRelativePath = relativePath.Replace('\\', '/');
		if (slashRelativePath.Length > 0 && !slashRelativePath.EndsWith('/')) {
			slashRelativePath += '/';
		}
		foreach (var entry in zipArchive.Entries) {
			if (token.IsCancellationRequested) {
				return list;
			}
			var attributes = (FileAttributes)entry.ExternalAttributes;
			if (attributes.HasFlag(FileAttributes.System) && !showSystem) {
				continue;
			}
			if (attributes.HasFlag(FileAttributes.Hidden) && !showHidden) {
				continue;
			}
			var entryName = entry.FullName;
			if (entryName.Length <= slashRelativePath.Length || !entryName.StartsWith(slashRelativePath)) {
				continue;
			}
			var indexOfSlash = entryName.IndexOf('/', relativePath.Length + 1);
			if (indexOfSlash != -1) {
				var folderName = entryName[relativePath.Length..indexOfSlash].TrimStart('/');
				var exists = false;
				foreach (var item in list) {
					if (item is not ZipFolderItem folder) {
						continue;
					}
					if (folder.Name == folderName) {
						exists = true;
						if (entryName.Length > indexOfSlash + 1) {  // 还有文件或文件夹，非空
							folder.hasItems = true;
							break;
						}
					}
				}
				if (!exists) {
					var fullPath = Path.Combine(zipPath, relativePath, folderName);
					list.Add(new ZipFolderItem(entry, fullPath, zipPath) { hasItems = entryName.Length > indexOfSlash + 1 });
				}
			} else {
				list.Add(new ZipFileItem(entry, zipPath));
			}
		}
		return list;
	}

	public void Dispose() {
		zipArchive.Dispose();
	}
}