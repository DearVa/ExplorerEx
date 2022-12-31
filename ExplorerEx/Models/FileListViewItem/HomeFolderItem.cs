using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;

namespace ExplorerEx.Models;

/// <summary>
/// 主页，也就是“此电脑”
/// </summary>
internal sealed class HomeFolderItem : FolderItem, ISpecialFolder {
	public static HomeFolderItem Singleton { get; }

	public CSIDL Csidl => CSIDL.Drives;

	public IntPtr IdList {
		get {
			Marshal.ThrowExceptionForHR(Shell32Interop.SHGetSpecialFolderLocation(IntPtr.Zero, Csidl, out var pIdList));
			return pIdList;
		}
	}

	static HomeFolderItem() {
		Singleton = new HomeFolderItem();
		RegisterSpecialFolder("$Home", Singleton, PathType.Home);
	}

	private HomeFolderItem() {
		FullPath = "$Home";
		Name = Strings.Resources.ThisPC;
		Type = Strings.Resources.Home;
		Icon = IconHelper.ComputerBitmapImage;
		IsReadonly = IsVirtual = true;
	}

	public override string DisplayText => Name;

	protected override void LoadAttributes() {
		throw new InvalidOperationException();
	}

	protected override void LoadIcon() { }

	protected override void InternalRename(string newName) {
		throw new InvalidOperationException();
	}

	public override List<FileListViewItem> EnumerateItems(string? selectedPath, in LoadDetailsOptions options, out FileListViewItem? selectedItem, CancellationToken token) {
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