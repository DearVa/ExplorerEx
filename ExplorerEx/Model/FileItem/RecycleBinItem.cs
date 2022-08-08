using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ExplorerEx.Command;
using ExplorerEx.Converter;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using static ExplorerEx.Shell32.Shell32Interop;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.Model;

/// <summary>
/// 回收站的一项文件
/// </summary>
public sealed class RecycleBinItem : FileListViewItem, IFilterable {
	public override string DisplayText => Name;

	public SimpleCommand Command { get; }

	private readonly IntPtr pidl;

	public RecycleBinItem(IntPtr pidl) : base(null!, null!) {
		this.pidl = pidl;
		Name = GetDetailOf(0) ?? throw new IOException();
		FullPath = @"$Recycle.Bin\" + Name;
		Command = new SimpleCommand(o => {
			if (o?.ToString() == null) {
				return;
			}
			try {
				ExecuteCommand(o.ToString()!);
			} catch (Exception e) {
				Logger.Exception(e);
			}
		});
	}

	public override void LoadAttributes(LoadDetailsOptions options) {
		throw new InvalidOperationException();
	}

	public override void LoadIcon(LoadDetailsOptions options) {
		var shFileInfo = new ShFileInfo();
		lock (ShellLock) {
			var flags = SHGFI.Icon | SHGFI.Pidl;
			if (options.UseLargeIcon) {
				flags |= SHGFI.LargeIcon;
			} else {
				flags |= SHGFI.SmallIcon;
			}
			var hr = SHGetFileInfo(pidl, 0, ref shFileInfo, Marshal.SizeOf<ShFileInfo>(), flags);
			if (hr < 0) {
				Icon = IconHelper.UnknownFileDrawingImage;
			} else {
				Icon = IconHelper.HIcon2BitmapSource(shFileInfo.hIcon);
				DestroyIcon(shFileInfo.hIcon);
			}
		}
	}

	/// <summary>
	/// 恢复
	/// </summary>
	public void Restore() {
		// ReSharper disable once StringLiteralTypo
		ExecuteCommand("undelete");
	}

	private void ExecuteCommand(string command) {
		// ReSharper disable once InconsistentlySynchronizedField
		Marshal.ThrowExceptionForHR(RecycleBinFolder.GetUIObjectOf(IntPtr.Zero, 1, new[] { pidl }, ref GUID_IContextMenu, 0, out var pCtxMenu));
		var ctxMenu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(pCtxMenu, typeof(IContextMenu));
		var hMenuCtx = CreatePopupMenu();
		ctxMenu.QueryContextMenu(hMenuCtx, 0, 1, 0x7FFF, CMF.Normal);
		var itemCount = GetMenuItemCount(hMenuCtx);
		var hr = -1;
		var verb = new StringBuilder(128);
		for (uint i = 0; i < itemCount; i++) {
			var id = GetMenuItemID(hMenuCtx, i);
			if (id is 0 or uint.MaxValue) {
				continue;
			}
			var uiCommand = id - 1;
			if (ctxMenu.GetCommandString((int)(id - 1), GCS.VerbW, 0, verb, 128) < 0) {
				continue;
			}
			if (verb.ToString() == command) {
				var cmi = new CtxMenuInvokeCommandInfo {
					cbSize = Marshal.SizeOf<CtxMenuInvokeCommandInfo>(),
					fMask = 0x400,  // CMIC_MASK_FLAG_NO_UI
					hwnd = IntPtr.Zero,
					lpParameters = null,
					lpDirectory = null,
					lpVerb = (IntPtr)uiCommand,
					nShow = 1, // SW_SHOWNORMAL
					dwHotKey = 0,
					hIcon = IntPtr.Zero
				};
				hr = ctxMenu.InvokeCommand(cmi);
				break;
			}
		}
		DestroyMenu(hMenuCtx);
		Marshal.ReleaseComObject(ctxMenu);
		Marshal.ThrowExceptionForHR(hr);
	}

	public override string GetRenameName() {
		throw new InvalidOperationException();
	}

	protected override bool InternalRename(string newName) {
		throw new InvalidOperationException();
	}

	/// <summary>
	/// 获取详细信息
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	private string? GetDetailOf(uint index) {
		// ReSharper disable once InconsistentlySynchronizedField
		if (RecycleBinFolder.GetDetailsOf(pidl, index, out var shellDetails) < 0) {
			return null;
		}
		return shellDetails.str.ToString();
	}
	
	public static ObservableCollection<RecycleBinItem> Items { get; } = new();

	private static readonly object Locker = new();
	private static Task? updateTask;

	/// <summary>
	/// 更新回收站文件列表
	/// </summary>
	public static void Update() {
		lock (Locker) {
			if (updateTask is { IsCompleted: false }) {
				return;
			}
			updateTask = Task.Run(() => {
				Trace.WriteLine("Update RecycleBin");
				var dispatcher = Application.Current.Dispatcher;
				dispatcher.Invoke(Items.Clear);
				var recycleBin = RecycleBinFolder;
				recycleBin.EnumObjects(IntPtr.Zero, SHCONT.Folders | SHCONT.NonFolders | SHCONT.IncludeHidden, out var enumFiles);
				var pidl = IntPtr.Zero;
				var pFetched = IntPtr.Zero;
				while (enumFiles.Next(1, ref pidl, ref pFetched) != 1) { // S_FALSE
					if (pFetched.ToInt64() == 0) {
						break;
					}
					var item = new RecycleBinItem(pidl);
					item.LoadIcon(LoadDetailsOptions.Default);
					dispatcher.Invoke(() => Items.Add(item));
				}
				Marshal.ReleaseComObject(enumFiles);
			});
		}
	}

	private static readonly FileSystemWatcher?[] Watchers = new FileSystemWatcher[26];

	/// <summary>
	/// 注册回收站变化监视，同时更新回收站列表
	/// </summary>
	public static void RegisterWatcher() {
		for (var i = 0; i < 26; i++) {
			if (Watchers[i] != null && !Directory.Exists((char)(i + 'A') + @":\$Recycle.Bin")) {
				Watchers[i]!.Dispose();
				Watchers[i] = null;
			}
		}
		foreach (var drive in DriveInfo.GetDrives()) {
			var i = drive.Name[0] - 'A';
			var recycleBinPath = drive.Name + "$Recycle.Bin";
			if (!Directory.Exists(recycleBinPath)) {
				continue;
			}
			if (Watchers[i] == null) {
				var watcher = Watchers[i] = new FileSystemWatcher(recycleBinPath) {
					IncludeSubdirectories = true
				};
				watcher.Changed += (_, _) => Update();
				watcher.Error += (_, _) => Update();
				try {
					watcher.EnableRaisingEvents = true;
				} catch {
					watcher.Dispose();
					Watchers[i] = null;
				}
			}
		}
		Update();
	}

	public static void UnregisterWatcher() {
		for (var i = 0; i < 26; i++) {
			if (Watchers[i] != null) {
				Watchers[i]!.Dispose();
				Watchers[i] = null;
			}
		}
	}

	public bool Filter(string filter) {
		return Name.ToLower().Contains(filter);
	}
}