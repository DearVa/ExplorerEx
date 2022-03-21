using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using ExplorerEx.Shell32;
using static ExplorerEx.Shell32.Shell32Interop;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.Model;

/// <summary>
/// 回收站的一项文件
/// </summary>
public sealed class RecycleBinItem : FileViewBaseItem {
	public override string FullPath { get; protected set; }

	public override string DisplayText => Name;

	private readonly IntPtr pidl;

	public RecycleBinItem(IntPtr pidl) {
		this.pidl = pidl;
		Name = GetDetailOf(0);
		if (Name == null) {
			throw new IOException();
		}
		FullPath = @"$Recycle.Bin\" + Name;
	}

	public override void LoadAttributes() {
		throw new InvalidOperationException();
	}

	public override void LoadIcon() {
		var shFileInfo = new ShFileInfo();
		lock (ShellLock) {
			var hr = SHGetFileInfo(pidl, 0, ref shFileInfo, Marshal.SizeOf<ShFileInfo>(), SHGFI.Icon | SHGFI.SmallIcon | SHGFI.Pidl);
			if (hr < 0) {
				Icon = IconHelper.UnknownFileDrawingImage;
			} else {
				Icon = IconHelper.HIcon2BitmapSource(shFileInfo.hIcon);
				DestroyIcon(shFileInfo.hIcon);
			}
		}
	}

	public override void StartRename() {
		throw new InvalidOperationException();
	}

	protected override bool Rename() {
		throw new InvalidOperationException();
	}

	private string GetDetailOf(uint index) {
		if (RecycleBinFolder.GetDetailsOf(pidl, index, out var shellDetails) < 0) {
			return null;
		}
		return shellDetails.str.ToString();
	}

	// ReSharper disable once CollectionNeverQueried.Global
	public static ObservableCollection<RecycleBinItem> Items { get; } = new();

	/// <summary>
	/// 获取回收站文件的个数
	/// </summary>
	/// <returns></returns>
	public static long GetFileCount() {
		var info = new ShQueryRbinInfo {
			cbSize = Marshal.SizeOf<ShQueryRbinInfo>()
		};
		Marshal.ThrowExceptionForHR(SHQueryRecycleBin(string.Empty, ref info));
		return info.i64NumItems;
	}

	public static void Update() {
		Items.Clear();
		var recycleBin = RecycleBinFolder;
		recycleBin.EnumObjects(IntPtr.Zero, SHCONT.Folders | SHCONT.NonFolders | SHCONT.IncludeHidden, out var enumFiles);
		var pidl = IntPtr.Zero;
		var pFetched = IntPtr.Zero;
		while (enumFiles.Next(1, ref pidl, ref pFetched) != 1) {  // S_FALSE
			if (pFetched.ToInt64() == 0) {
				break;
			}
			var item = new RecycleBinItem(pidl);
			item.LoadIcon();
			Items.Add(item);
		}
		Marshal.ReleaseComObject(enumFiles);
	}
}