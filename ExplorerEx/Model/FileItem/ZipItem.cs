using System;
using ExplorerEx.Shell32;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

	public override void LoadAttributes() {
		Type = FileUtils.GetFileTypeDescription(extension);
	}

	public override void LoadIcon() {
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
	private readonly string relativePath;  // 一定以\结尾或者为string.Empty
	private bool hasItems;

	/// <summary>
	/// 初始化ZipFolderItem，仅供在在外部使用，一旦初始化就会打开一个文件流
	/// </summary>
	/// <param name="fullPath">一定以\结尾，如F:\test.zip\</param>
	/// <param name="zipPath">如F:\test.zip</param>
	/// <param name="relativePath">开头不为\</param>
	public ZipFolderItem(string fullPath, string zipPath, string relativePath) : this(ZipFile.Open(zipPath, ZipArchiveMode.Read, Encoding.GetEncoding("gb2312")), fullPath, zipPath, relativePath) { }

	private ZipFolderItem(ZipArchiveEntry zipArchiveEntry, string fullPath, string zipPath, string relativePath) : this(zipArchiveEntry.Archive, fullPath, zipPath, relativePath) {
		DateModified = zipArchiveEntry.LastWriteTime.DateTime;
	}

	private ZipFolderItem(ZipArchive zipArchive, string fullPath, string zipPath, string relativePath) {
		Debug.Assert(relativePath == string.Empty || relativePath[^1] == '\\');
		this.zipArchive = zipArchive;
		FullPath = fullPath;
		Name = Path.GetFileName(relativePath == string.Empty ? zipPath : relativePath[..^1]);
		IsFolder = true;
		FileSize = -1;
		Type = "Folder".L();
		this.zipPath = zipPath;
		this.relativePath = relativePath;
	}

	public override void LoadAttributes() { }

	public override void LoadIcon() {
		if (relativePath == string.Empty) {
			Icon = IconHelper.GetSmallIcon(".zip", true);
		} else if (hasItems) {
			Icon = IconHelper.FolderDrawingImage;
		} else {
			Icon = IconHelper.EmptyFolderDrawingImage;
		}
	}

	public override List<FileListViewItem> EnumerateItems(string selectedPath, out FileListViewItem selectedItem, CancellationToken token) {
		selectedItem = null;
		var list = new List<FileListViewItem>();
		var slashRelativePath = relativePath.Replace('\\', '/');
		foreach (var entry in zipArchive.Entries.Where(e => e.FullName.Length > slashRelativePath.Length && e.FullName.StartsWith(slashRelativePath))) {
			if (token.IsCancellationRequested) {
				return null;
			}
			var entryName = entry.FullName;
			var indexOfSlash = entryName.IndexOf('/', relativePath.Length);
			if (indexOfSlash != -1) {
				var folderName = entryName[relativePath.Length..indexOfSlash];
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
					var newRelativePath = relativePath + folderName + '\\';
					list.Add(new ZipFolderItem(entry, zipPath + "\\" + newRelativePath, zipPath, newRelativePath) { hasItems = entryName.Length > indexOfSlash + 1 });
				}
			} else {
				list.Add(new ZipFileItem(entry, zipPath));
			}
		}
		return list;
	}

	public void Dispose() {
		zipArchive?.Dispose();
	}
}