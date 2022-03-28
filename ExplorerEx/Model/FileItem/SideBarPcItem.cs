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
internal sealed class SideBarPcItem : FileSystemItem {
	public static ObservableCollection<SideBarPcItem> RootItems { get; } = new();

	/// <summary>
	/// 充当占位项目，以便展开
	/// </summary>
	private static readonly SideBarPcItem DefaultItem = new();

	private SideBarPcItem() { }

	/// <summary>
	/// 用于初始化zip
	/// </summary>
	/// <param name="fileInfo"></param>
	public SideBarPcItem(FileInfo fileInfo) : base(fileInfo) {
		FullPath = fileInfo.FullName;
		InitializeChildren();
	}

	public SideBarPcItem(DirectoryInfo directoryInfo) : base(directoryInfo) {
		FullPath = directoryInfo.FullName;
		InitializeChildren();
	}

	public SideBarPcItem(DriveInfo driveInfo) {
		FullPath = driveInfo.Name;
		IsFolder = true;
		if (driveInfo.IsReady) {
			InitializeChildren();
		}
		Name = DriveUtils.GetFriendlyName(driveInfo);
		Icon = IconHelper.GetDriveThumbnail(driveInfo);
	}

	private void InitializeChildren() {
		var dispatcher = Application.Current.Dispatcher;
		dispatcher.Invoke(() => Children = new ObservableCollection<SideBarPcItem>());
		if (Directory.EnumerateDirectories(FullPath).Any()) {
			dispatcher.Invoke(() => Children.Add(DefaultItem));
		}
	}

	public override string FullPath { get; protected set; }

	public ObservableCollection<SideBarPcItem> Children { get; private set; }

	public bool IsExpanded {
		get => isExpanded;
		set {
			if (isExpanded != value) {
				isExpanded = value;
				UpdateUI();
				if (value) {
					Children.Clear();
					Task.Run(() => {
						var dispatcher = Application.Current.Dispatcher;
						try {
							foreach (var directoryPath in Directory.EnumerateDirectories(FullPath)) {
								try {
									var item = new SideBarPcItem(new DirectoryInfo(directoryPath));
									item.LoadIcon();
									dispatcher.Invoke(() => Children.Add(item));
								} catch { }
							}
						} catch { }
					});
				}
			}
		}
	}

	private bool isExpanded;
}