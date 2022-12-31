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
using ExplorerEx.Utils.Enumerators;

namespace ExplorerEx.Models;

/// <summary>
/// 只显示文件夹，可枚举子文件夹
/// </summary>
public sealed class FolderOnlyItem : FolderItem {
	/// <summary>
	/// 此电脑
	/// </summary>
	public static FolderOnlyItem Home { get; } = new(Strings.Resources.ThisPC, null);

	/// <summary>
	/// 充当占位项目，以便展开
	/// </summary>
	public static FolderOnlyItem DefaultItem { get; } = new(Strings.Resources.Loading, Home);

	/// <summary>
	/// 充当占位项目，以便展开
	/// </summary>
	public static FolderOnlyItem EmptyItem { get; } = new(Strings.Resources.EmptyFolder, Home);

	private static readonly ReadOnlyCollection<FolderOnlyItem> LoadingChildren = new(new List<FolderOnlyItem> { DefaultItem });

	private static readonly ReadOnlyCollection<FolderOnlyItem> EmptyChildren = new(new List<FolderOnlyItem> { EmptyItem });

	public override string DisplayText => Name;

	private CancellationTokenSource? cts;
	private readonly string? zipPath;
	/// <summary>
	/// 用于存储zip里的相对路径，注意是/而不是\
	/// </summary>
	private readonly string? relativePath;

	private FolderOnlyItem(string name, FolderOnlyItem? parent) : base(parent == null ? "$Home" : "$Unknown", name, null) {
		if (parent == null) {
			InitializeChildren();
			UpdateDriveChildren();
			Parent = this;
		} else {
			IsEnabled = false;
			Parent = parent;
		}
		IsVirtual = true;
	}

	/// <summary>
	/// 用于初始化zip
	/// </summary>
	/// <param name="zipPath"></param>
	/// <param name="relativePath"></param>
	/// <param name="parent"></param>
	public FolderOnlyItem(string zipPath, string relativePath, FolderOnlyItem parent) : 
		base(relativePath == string.Empty ? zipPath : zipPath + '\\' + relativePath.Replace('/', '\\'),
			relativePath == string.Empty ? Path.GetFileName(zipPath) : Path.GetFileName(relativePath),
			null) {

		if (!File.Exists(zipPath) || zipPath[^4..] != ".zip") {
			throw new ArgumentException("Not a zip file");
		}

		FileSystemInfo = new FileInfo(zipPath);
		this.zipPath = zipPath;
		this.relativePath = relativePath;
		Parent = parent;
		IsReadonly = true;
		InitializeChildren();
	}

	public FolderOnlyItem(DirectoryInfo directoryInfo, FolderOnlyItem parent) : base(directoryInfo.FullName, directoryInfo.Name, null) {
		Parent = parent;
		InitializeChildren();
	}

	public FolderOnlyItem(DriveInfo driveInfo): base(driveInfo.Name, DriveUtils.GetFriendlyName(driveInfo), null) {
		Parent = Home;
		InitializeChildren();
		Icon = IconHelper.GetDriveThumbnail(driveInfo.Name);
	}

	/// <summary>
	/// 需要注意的是，这个只看有没有文件夹或者zip
	/// </summary>
	/// <returns></returns>
	private bool HasChildren() {
		if (zipPath != null) {
			System.Diagnostics.Debug.Assert(relativePath != null);
			using var zip = ZipFile.OpenRead(zipPath);
			return zip.Entries.Select(static e => e.FullName).Where(entryName => entryName.StartsWith(relativePath)).Any(entryName => entryName[relativePath.Length..].Contains('/'));
		}
		return Directory.Exists(FullPath) && new FolderOnlyItemEnumerator(FullPath, null!).Any();
	}

	private void InitializeChildren() {
		Task.Run(() => {
			if (HasChildren()) {
				Children = LoadingChildren;
				actualChildren = new ConcurrentObservableCollection<FolderOnlyItem>();
			}
		});
	}

	protected override void LoadAttributes() { }

	protected override void LoadIcon() {
		if (IsVirtual) {
			return;
		}
		if (zipPath != null) {
			if (relativePath == string.Empty) {
				Icon = IconHelper.GetSmallIcon(".zip", true);
			} else if (HasChildren()) {
				Icon = IconHelper.FolderDrawingImage;
			} else {
				Icon = IconHelper.EmptyFolderDrawingImage;
			}
		} else if (HasChildren()) {
			Icon = IconHelper.FolderDrawingImage;
		} else {
			Icon = IconHelper.EmptyFolderDrawingImage;
		}
	}

	public FolderOnlyItem Parent { get; }

	/// <summary>
	/// 枚举之前，先把这个设为<see cref="LoadingChildren"/>，枚举完成后如数量大于1，设为<see cref="actualChildren"/>
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
					// ReSharper disable once PossibleUnintendedReferenceComparison
					if (this == Home) {
						UpdateDriveChildren();
						return;
					}

					cts?.Cancel();
					Children = LoadingChildren;
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
								var showHidden = Settings.Current[Settings.CommonSettings.ShowHiddenFilesAndFolders].AsBoolean();
								var showSystem = Settings.Current[Settings.CommonSettings.ShowProtectedSystemFilesAndFolders].AsBoolean();

								using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Read, Encoding.GetEncoding("gbk"));
								foreach (var entry in archive.Entries) {
									if (token.IsCancellationRequested) {
										return;
									}
									var slashRelativePath = relativePath.Replace('\\', '/');
									if (slashRelativePath.Length > 0 && !slashRelativePath.EndsWith('/')) {
										slashRelativePath += '/';
									}
									var entryName = entry.FullName;
									if (entryName.Length <= slashRelativePath.Length || !entryName.StartsWith(slashRelativePath)) {
										continue;
									}
									var attributes = (FileAttributes)entry.ExternalAttributes;
									if (attributes.HasFlag(FileAttributes.System) && !showSystem) {
										continue;
									}
									if (attributes.HasFlag(FileAttributes.Hidden) && !showHidden) {
										continue;
									}
									var indexOfSlash = entryName.IndexOf('/', relativePath.Length + 1);
									if (indexOfSlash != -1) {
										var folderName = entryName[relativePath.Length..indexOfSlash].TrimStart('/');
										if (actualChildren.All(i => Path.GetFileName(i.relativePath!) != folderName)) {
											actualChildren.Add(new FolderOnlyItem(zipPath, Path.Combine(relativePath, folderName), this));
										}
									}
								}
							} else {
								foreach (var folderOnlyItem in new FolderOnlyItemEnumerator(FullPath, this)) {
									if (token.IsCancellationRequested) {
										return;
									}
									actualChildren.Add(folderOnlyItem);
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
								item.LoadIcon();
							}
						} else {
							Children = EmptyChildren;
						}
					}, token);
				} else if (actualChildren != null && this != Home) {
					actualChildren.Clear();  // 释放内存
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
			Children = LoadingChildren;
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