using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ExplorerEx.Utils;

namespace ExplorerEx.Model;

/// <summary>
/// 只显示文件夹，可枚举子文件夹
/// </summary>
internal sealed class FolderOnlyItem : FileListViewItem {
	/// <summary>
	/// 此电脑
	/// </summary>
	public static FolderOnlyItem Home { get; } = new() {
		Name = "ThisPC".L(),
		children = new ObservableCollection<FolderOnlyItem>()
	};

	/// <summary>
	/// 充当占位项目，以便展开
	/// </summary>
	public static readonly FolderOnlyItem DefaultItem = new() {
		Name = "EmptyFolder".L()
	};

	private static readonly ObservableCollection<FolderOnlyItem> DefaultChildren = new() { DefaultItem };

	public override string DisplayText => Name;

	private CancellationTokenSource cts;
	private readonly bool hasItems;
	private readonly string zipPath;
	private readonly string relativePath;

	private FolderOnlyItem() { }

	/// <summary>
	/// 用于初始化zip
	/// </summary>
	/// <param name="zipPath"></param>
	/// <param name="relativePath"></param>
	/// <param name="parent"></param>
	public FolderOnlyItem(string zipPath, string relativePath, FolderOnlyItem parent) {
		if (!File.Exists(zipPath) || zipPath[^4..] != ".zip") {
			throw new ArgumentException("Not a zip file");
		}
		this.zipPath = zipPath;
		this.relativePath = relativePath;
		if (relativePath == string.Empty) {
			FullPath = zipPath + '\\';
			Name = Path.GetFileName(zipPath);
		} else {
			FullPath = zipPath + '\\' + relativePath;
			Name = Path.GetFileName(relativePath[..^1]);
		}
		Parent = parent;
		IsFolder = true;
		using var zip = ZipFile.OpenRead(zipPath);
		foreach (var entryName in zip.Entries.Select(e => e.FullName)) {
			if (entryName.StartsWith(relativePath)) {
				hasItems = true;
				if (entryName[relativePath.Length..].Contains('/')) {
					InitializeChildren();
					break;
				}
			}
		}
	}

	public FolderOnlyItem(DirectoryInfo directoryInfo, FolderOnlyItem parent) {
		FullPath = directoryInfo.FullName;
		Name = directoryInfo.Name;
		Parent = parent;
		IsFolder = true;
		if (Directory.EnumerateDirectories(FullPath).Any()) {
			InitializeChildren();
		}
	}

	public FolderOnlyItem(DriveInfo driveInfo) {
		FullPath = driveInfo.Name;
		Parent = Home;
		IsFolder = true;
		if (driveInfo.IsReady) {
			InitializeChildren();
		}
		Name = DriveUtils.GetFriendlyName(driveInfo);
		Icon = IconHelper.GetDriveThumbnail(driveInfo);
	}

	private void InitializeChildren() {
		children = DefaultChildren;
		Application.Current.Dispatcher.Invoke(() => actualChildren = new ObservableCollection<FolderOnlyItem>());
	}

	public override void LoadAttributes() {
		throw new InvalidOperationException();
	}

	public override void LoadIcon() {
		if (zipPath != null) {
			if (relativePath == string.Empty) {
				Icon = IconHelper.GetSmallIcon(".zip", true);
			} else if (hasItems) {
				Icon = IconHelper.FolderDrawingImage;
			} else {
				Icon = IconHelper.EmptyFolderDrawingImage;
			}
		} else if (FolderUtils.IsEmptyFolder(FullPath)) {
			Icon = IconHelper.EmptyFolderDrawingImage;
		} else {
			Icon = IconHelper.FolderDrawingImage;
		}
	}

	public override void StartRename() {
		throw new InvalidOperationException();
	}

	protected override bool Rename() {
		throw new InvalidOperationException();
	}

	public FolderOnlyItem Parent { get; }

	/// <summary>
	/// 枚举之前，先把这个设为<see cref="DefaultChildren"/>，枚举完成后如数量大于1，设为<see cref="actualChildren"/>
	/// </summary>
	public ObservableCollection<FolderOnlyItem> Children {
		get => children;
		private set {
			if (children != null) {
				children = value;
				UpdateUI();
			}
		}
	}

	private ObservableCollection<FolderOnlyItem> children;

	/// <summary>
	/// 真正存储Children的列表
	/// </summary>
	private ObservableCollection<FolderOnlyItem> actualChildren;

	public bool IsExpanded {
		get => isExpanded;
		set {
			if (isExpanded != value) {
				cts?.Cancel();
				isExpanded = value;
				UpdateUI();
				if (value && FullPath != null) {
					Children = DefaultChildren;
					actualChildren.Clear();
					cts = new CancellationTokenSource();
					var token = cts.Token;
					Task.Run(() => {
						var list = new List<FolderOnlyItem>();
						try {
							if (zipPath != null) {
								using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Read, Encoding.GetEncoding("gb2312"));
								foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith(relativePath) && e.FullName[relativePath.Length..].Contains('/'))) {
									if (token.IsCancellationRequested) {
										return;
									}
									var entryName = entry.FullName;
									var indexOfSlash = entryName.IndexOf('/', relativePath.Length);
									if (indexOfSlash != -1) {
										var folderName = entryName[relativePath.Length..indexOfSlash];
										if (list.All(i => Path.GetFileName(i.relativePath[..^1]) != folderName)) {
											list.Add(new FolderOnlyItem(zipPath, relativePath + folderName + '\\', this));
										}
									}
								}
							} else {
								foreach (var directoryPath in Directory.EnumerateDirectories(FullPath)) {
									if (token.IsCancellationRequested) {
										return;
									}
									try {
										list.Add(new FolderOnlyItem(new DirectoryInfo(directoryPath), this));
									} catch {
										// 忽略错误，不添加
									}
								}
								foreach (var zipPath in Directory.EnumerateFiles(FullPath, "*.zip")) {
									if (token.IsCancellationRequested) {
										return;
									}
									try {
										list.Add(new FolderOnlyItem(zipPath, string.Empty, this));
									} catch {
										// 忽略错误，不添加
									}
								}
							}
						} catch {
							// 忽略错误，不添加
						}
						if (list.Count > 0) {
							Application.Current.Dispatcher.Invoke(() => {
								foreach (var item in list) {
									actualChildren.Add(item);
								}
							});
							Children = actualChildren;
							foreach (var item in list) {
								if (token.IsCancellationRequested) {
									return;
								}
								item.LoadIcon();
							}
						}
					}, token);
				}
			}
		}
	}

	private bool isExpanded;
}