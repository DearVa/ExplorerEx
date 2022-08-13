using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExplorerEx.Utils;
using ExplorerEx.Utils.Collections;

namespace ExplorerEx.Model;

/// <summary>
/// 只显示文件夹，可枚举子文件夹
/// </summary>
internal sealed class FolderOnlyItem : FileListViewItem {
	/// <summary>
	/// 此电脑
	/// </summary>
	public static FolderOnlyItem Home { get; } = new((FolderOnlyItem?)null) {
		Name = "ThisPC".L()
	};

	/// <summary>
	/// 充当占位项目，以便展开
	/// </summary>
	public static readonly FolderOnlyItem DefaultItem = new(Home) {
		Name = "EmptyFolder".L()
	};

	private static readonly ReadOnlyCollection<FolderOnlyItem> DefaultChildren = new(new List<FolderOnlyItem> { DefaultItem });

	public override string DisplayText => Name;

	private CancellationTokenSource? cts;
	private readonly bool hasItems;
	private readonly string? zipPath;
	/// <summary>
	/// 用于存储zip里的相对路径，注意是/而不是\
	/// </summary>
	private readonly string? relativePath;

	private FolderOnlyItem(FolderOnlyItem? parent) : base(null!, null!, true) {
		if (parent == null) {
			InitializeChildren();
			UpdateDriveChildren();
			Parent = this;
		} else {
			IsEnabled = false;
			Parent = parent;
		}
	}

	/// <summary>
	/// 用于初始化zip
	/// </summary>
	/// <param name="zipPath"></param>
	/// <param name="relativePath"></param>
	/// <param name="parent"></param>
	public FolderOnlyItem(string zipPath, string relativePath, FolderOnlyItem parent) : base(null!, null!, true) {
		if (!File.Exists(zipPath) || zipPath[^4..] != ".zip") {
			throw new ArgumentException("Not a zip file");
		}
		this.zipPath = zipPath;
		this.relativePath = relativePath;
		if (relativePath == string.Empty) {
			FullPath = zipPath + '\\';
			Name = Path.GetFileName(zipPath);
		} else {
			FullPath = zipPath + '\\' + relativePath.Replace('/', '\\');
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

	public FolderOnlyItem(DirectoryInfo directoryInfo, FolderOnlyItem parent) : base(directoryInfo.FullName, directoryInfo.Name, true) {
		Parent = parent;
		IsFolder = true;
		if (Directory.EnumerateDirectories(FullPath).Any()) {
			InitializeChildren();
		}
	}

	public FolderOnlyItem(DriveInfo driveInfo): base(driveInfo.Name, DriveUtils.GetFriendlyName(driveInfo), true) {
		Parent = Home;
		IsFolder = true;
		if (driveInfo.IsReady) {
			InitializeChildren();
		}
		Icon = IconHelper.GetDriveThumbnail(driveInfo.Name);
	}

	private void InitializeChildren() {
		Children = DefaultChildren;
		actualChildren = new ConcurrentObservableCollection<FolderOnlyItem>();
	}

	public override void LoadAttributes(LoadDetailsOptions options) {
		throw new InvalidOperationException();
	}

	public override void LoadIcon(LoadDetailsOptions options) {
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

	public override string GetRenameName() {
		throw new InvalidOperationException();
	}

	protected override bool InternalRename(string newName) {
		throw new InvalidOperationException();
	}

	public FolderOnlyItem Parent { get; }

	/// <summary>
	/// 枚举之前，先把这个设为<see cref="DefaultChildren"/>，枚举完成后如数量大于1，设为<see cref="actualChildren"/>
	/// </summary>
	public IList<FolderOnlyItem>? Children {
		get => children;
		set {
			// ReSharper disable once PossibleUnintendedReferenceComparison
			if (children != value) {
				children = value;
				OnPropertyChanged();
			}
		}
	}

	private IList<FolderOnlyItem>? children;

	/// <summary>
	/// 真正存储Children的列表
	/// </summary>
	private ConcurrentObservableCollection<FolderOnlyItem>? actualChildren;

	public bool IsExpanded {
		get => isExpanded;
		set {
			if (isExpanded != value) {
				isExpanded = value;
				OnPropertyChanged();
				if (value) {
					if (this == Home) {
						UpdateDriveChildren();
						return;
					}

					cts?.Cancel();
					Children = DefaultChildren;
					if (actualChildren == null) {
						actualChildren = new ConcurrentObservableCollection<FolderOnlyItem>();
					} else {
						actualChildren.Clear();
					}
					cts = new CancellationTokenSource();
					var token = cts.Token;
					Task.Run(() => {
						try {
							if (zipPath != null && relativePath != null) {
								using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Read, Encoding.GetEncoding("gb2312"));
								foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith(relativePath) && e.FullName[relativePath.Length..].Contains('/'))) {
									if (token.IsCancellationRequested) {
										return;
									}
									var entryName = entry.FullName;
									var indexOfSlash = entryName.IndexOf('/', relativePath.Length);
									if (indexOfSlash != -1) {
										var folderName = entryName[relativePath.Length..indexOfSlash];
										if (actualChildren.All(i => Path.GetFileName(i.relativePath![..^1]) != folderName)) {
											actualChildren.Add(new FolderOnlyItem(zipPath, relativePath + folderName + '/', this));
										}
									}
								}
							} else {
								foreach (var directoryPath in Directory.EnumerateDirectories(FullPath)) {
									if (token.IsCancellationRequested) {
										return;
									}
									try {
										actualChildren.Add(new FolderOnlyItem(new DirectoryInfo(directoryPath), this));
									} catch {
										// 忽略错误，不添加
									}
								}
								foreach (var zipPath in Directory.EnumerateFiles(FullPath, "*.zip")) {
									if (token.IsCancellationRequested) {
										return;
									}
									try {
										actualChildren.Add(new FolderOnlyItem(zipPath, string.Empty, this));
									} catch {
										// 忽略错误，不添加
									}
								}
							}
						} catch {
							// 忽略错误，不添加
						}
						if (actualChildren.Count > 0) {
							Children = actualChildren;
							foreach (var item in actualChildren) {
								if (token.IsCancellationRequested) {
									return;
								}
								item.LoadIcon(LoadDetailsOptions.Default);
							}
						}
					}, token);
				}
			}
		}
	}

	private bool isExpanded;

	public bool IsEnabled { get; } = true;

	public new bool IsSelected {
		get => false;
		// ReSharper disable once ValueParameterNotUsed
		set => OnPropertyChanged();
	}

	public void UpdateDriveChildren() {
		cts?.Cancel();
		cts = new CancellationTokenSource();
		var token = cts.Token;
		Task.Run(() => {
			Children = DefaultChildren;
			if (actualChildren == null) {
				actualChildren = new ConcurrentObservableCollection<FolderOnlyItem>();
			} else {
				actualChildren.Clear();
			}
			foreach (var driveInfo in DriveInfo.GetDrives()) {
				if (token.IsCancellationRequested) {
					return;
				}
				actualChildren.Add(new FolderOnlyItem(driveInfo));
			}
			Children = actualChildren;
		}, token);
	}
}