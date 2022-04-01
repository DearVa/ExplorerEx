using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;

namespace ExplorerEx.Model;

/// <summary>
/// 在侧边栏显示此电脑的项目，树形结构，只显示文件夹
/// </summary>
internal sealed class FolderItem : FileSystemItem {
	/// <summary>
	/// 此电脑
	/// </summary>
	public static FolderItem Home { get; } = new() {
		Name = "ThisPC".L(),
		children = new ObservableCollection<FolderItem>()
	};

	/// <summary>
	/// 充当占位项目，以便展开
	/// </summary>
	public static readonly FolderItem DefaultItem = new() {
		Name = "EmptyFolder".L()
	};

	private static readonly ObservableCollection<FolderItem> DefaultChildren = new() { DefaultItem };

	private FolderItem() { }

	/// <summary>
	/// 用于初始化zip
	/// </summary>
	/// <param name="fileInfo"></param>
	public FolderItem(FileInfo fileInfo, FolderItem parent) : base(fileInfo) {
		FullPath = fileInfo.FullName;
		Parent = parent;
		InitializeChildren();
	}

	public FolderItem(DirectoryInfo directoryInfo, FolderItem parent) : base(directoryInfo) {
		FullPath = directoryInfo.FullName;
		Parent = parent;
		InitializeChildren();
	}

	public FolderItem(DriveInfo driveInfo) {
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
		Application.Current.Dispatcher.Invoke(() => actualChildren = new ObservableCollection<FolderItem>());
	}

	public override string FullPath { get; protected set; }

	public FolderItem Parent { get; }

	/// <summary>
	/// 枚举之前，先把这个设为<see cref="DefaultChildren"/>，枚举完成后如数量大于1，设为<see cref="actualChildren"/>
	/// </summary>
	public ObservableCollection<FolderItem> Children {
		get => children;
		private set {
			if (children != null) {
				children = value;
				UpdateUI();
			}
		}
	}

	private ObservableCollection<FolderItem> children;

	/// <summary>
	/// 真正存储Children的列表
	/// </summary>
	private ObservableCollection<FolderItem> actualChildren;

	public bool IsExpanded {
		get => isExpanded;
		set {
			if (isExpanded != value) {
				isExpanded = value;
				UpdateUI();
				if (value && FullPath != null) {
					Children = DefaultChildren;
					actualChildren.Clear();
					Task.Run(() => {
						var list = new List<FolderItem>();
						try {
							foreach (var directoryPath in Directory.EnumerateDirectories(FullPath)) {
								try {
									list.Add(new FolderItem(new DirectoryInfo(directoryPath), this));
								} catch {
									// 忽略错误，不添加
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
								item.LoadIcon();
							}
						}
					});
				}
			}
		}
	}

	private bool isExpanded;
}