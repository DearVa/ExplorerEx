using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;

namespace ExplorerEx.Model;

/// <summary>
/// 主页，也就是“此电脑”
/// </summary>
internal sealed class HomeFolderItem : FolderItem, ISpecialFolder {
	public static HomeFolderItem Singleton { get; } = new();

	public CSIDL Csidl => CSIDL.Drives;

	public IntPtr IdList {
		get {
			Marshal.ThrowExceptionForHR(Shell32Interop.SHGetSpecialFolderLocation(IntPtr.Zero, Csidl, out var pIdList));
			return pIdList;
		}
	}

	// Explicit static constructor to tell C# compiler
	// not to mark type as beforefieldinit
	static HomeFolderItem() { }

	private HomeFolderItem() {
		FullPath = "$Home";
		Name = "ThisPC".L();
		Type = "Home".L();
		Icon = IconHelper.ComputerBitmapImage;
		IsReadonly = IsVirtual = true;
	}

	public override string DisplayText => Name;

	public override void LoadAttributes(LoadDetailsOptions options) {
		throw new InvalidOperationException();
	}

	public override void LoadIcon(LoadDetailsOptions options) { }

	protected override bool InternalRename(string newName) {
		throw new InvalidOperationException();
	}

	public override List<FileListViewItem> EnumerateItems(string? selectedPath, out FileListViewItem? selectedItem, CancellationToken token) {
		selectedItem = null;
		var list = new List<FileListViewItem>();
		foreach (var drive in DriveInfo.GetDrives()) {
			if (token.IsCancellationRequested) {
				return list;
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