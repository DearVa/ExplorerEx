using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;

namespace ExplorerEx.Model;

/// <summary>
/// 主页，也就是“此电脑”
/// </summary>
internal sealed class HomeFolderItem : FolderItem {
	public static HomeFolderItem Instance { get; } = new();

	// Explicit static constructor to tell C# compiler
	// not to mark type as beforefieldinit
	static HomeFolderItem() { }

	private HomeFolderItem() {
		Name = "ThisPC".L();
		Type = "Home".L();
		Icon = IconHelper.ComputerBitmapImage;
	}

	public override string DisplayText => Name;

	public override void LoadAttributes() {
		throw new InvalidOperationException();
	}

	public override void LoadIcon() { }

	public override void StartRename() {
		throw new InvalidOperationException();
	}

	protected override bool Rename() {
		throw new InvalidOperationException();
	}

	public override List<FileListViewItem> EnumerateItems(string selectedPath, out FileListViewItem selectedItem, CancellationToken token) {
		selectedItem = null;
		var list = new List<FileListViewItem>();
		foreach (var drive in DriveInfo.GetDrives()) {
			if (token.IsCancellationRequested) {
				return null;
			}
			var item = new DiskDriveItem(drive);
			list.Add(item);
			if (drive.Name == selectedPath) {
				item.IsSelected = true;
				selectedItem = item;
			}
		}
		return list;
	}
}