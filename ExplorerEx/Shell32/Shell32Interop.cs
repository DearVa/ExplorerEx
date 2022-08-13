using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using ExplorerEx.Command;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.Shell32;

internal static class Shell32Interop {
	// ReSharper disable InconsistentNaming
	// ReSharper disable IdentifierTypo
	// ReSharper disable StringLiteralTypo
	// ReSharper disable UnusedMember.Global
	// ReSharper disable FieldCanBeMadeReadOnly.Global
	// ReSharper disable UnusedType.Global
	public const string IID_IShellFolder = "000214E6-0000-0000-C000-000000000046";
	public static Guid GUID_IShellFolder = new(IID_IShellFolder);

	public const string IID_IShellFolder2 = "93F2F68C-1D1B-11D3-A30E-00C04F79ABD1";
	public static Guid GUID_IShellFolder2 = new(IID_IShellFolder2);

	public const string IID_IShellItem = "43826d1e-e718-42ee-bc55-a1e261c37bfe";
	public static Guid GUID_IShellItem = new(IID_IShellItem);

	public const string IID_IShellItem2 = "7E9FB0D3-919F-4307-AB2E-9B1860310C93";
	public static Guid GUID_IShellItem2 = new(IID_IShellItem2);

	public const string IID_IImageList = "46EB5926-582E-4017-9FDF-E8998DAA0950";
	public static Guid GUID_IImageList = new(IID_IImageList);

	public const string IID_IImageList2 = "192b9d83-50fc-457b-90a0-2b82a8b5dae1";
	public static Guid GUID_IImageList2 = new(IID_IImageList2);

	public const string IID_IContextMenu = "000214e4-0000-0000-c000-000000000046";
	public static Guid GUID_IContextMenu = new(IID_IContextMenu);

	public const string IID_ShellLink = "00021401-0000-0000-C000-000000000046";
	public static Guid CLSID_ShellLink = new(IID_ShellLink);

	public const string IID_IShellLink = "000214F9-0000-0000-C000-000000000046";
	public static Guid GUID_IShellLink = new(IID_IShellLink);

	public const string IID_IPersistFile = "0000010b-0000-0000-C000-000000000046";
	public static Guid GUID_IPersistFile = new(IID_IPersistFile);

	public const string IID_IPersistIDList = "1079acfc-29bd-11d3-8e0d-00c04f6837d5";
	public static Guid GUID_IPersistIDList = new(IID_IPersistIDList);

	public static Guid BHID_DataObject = new("b8c0bd9f-ed24-455c-83e6-d5390c4fe8c4");
	public static Guid BHID_SFObject = new("3981e224-f559-11d3-8e3a-00c04f6837d5");
	public static Guid BHID_SFUIObject = new("3981e225-f559-11d3-8e3a-00c04f6837d5");

	/// <summary>
	/// Shell操作共用的锁
	/// </summary>
	public static readonly object ShellLock = new();

	private const string Shell32 = "shell32.dll";

	[DllImport(Shell32, SetLastError = true, CharSet = CharSet.Unicode)]
	public static extern int SHFileOperation(ShFileOpStruct lpFileOp);

	/// <summary>
	/// Retrieves a pointer to the Shell's IMalloc interface.
	/// </summary>
	/// <param name="hObject"></param>
	/// <returns></returns>
	[DllImport("shell32.dll")]
	public static extern int SHGetMalloc(out IntPtr hObject);

	[DllImport(Shell32, SetLastError = true)]
	public static extern int SHGetSpecialFolderLocation(IntPtr hwndOwner, CSIDL nFolder, out IntPtr ppidl);

	[DllImport(Shell32)]
	public static extern int SHGetFolderLocation(IntPtr hwndOwner, CSIDL nFolder, IntPtr hToken, uint dwReserved, out IntPtr ppidl);

	[StructLayout(LayoutKind.Sequential)]
	public struct ShQueryRbinInfo {
		public int cbSize;
		public long i64Size;
		public long i64NumItems;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	public struct SHChangeNotifyEntry {
		public IntPtr pIdl;
		[MarshalAs(UnmanagedType.Bool)] public bool Recursively;
	}

	[Flags]
	public enum OpenAsInfoFlags {
		AllowRegistration = 0x00000001,   // Show "Always" checkbox
		RegisterExt = 0x00000002,   // Perform registration when user hits OK
		Exec = 0x00000004,   // Exec file after registering
		ForceRegistration = 0x00000008,   // Force the checkbox to be registration
		HideRegistration = 0x00000020,   // Vista+: Hide the "always use this file" checkbox
		UrlProtocol = 0x00000040,   // Vista+: cszFile is actually a URI scheme; show handlers for that scheme
		FileIsUri = 0x00000080    // Win8+: The location pointed to by the pcszFile parameter is given as a URI
	}

	public struct OpenAsInfo {
		[MarshalAs(UnmanagedType.LPWStr)]
		public string cszFile;

		[MarshalAs(UnmanagedType.LPWStr)]
		public string cszClass;

		[MarshalAs(UnmanagedType.I4)]
		public OpenAsInfoFlags oaifInFlags;
	}

	[DllImport(Shell32)]
	public static extern int SHQueryRecycleBin(string pszRootPath, ref ShQueryRbinInfo pSHQueryRBInfo);


	[DllImport(Shell32)]
	public static extern int SHGetDesktopFolder(out IShellFolder ppshf);

	///
	/// SHGetImageList is not exported correctly in XP.  See KB316931
	/// http://support.microsoft.com/default.aspx?scid=kb;EN-US;Q316931
	/// Apparently (and hopefully) ordinal 727 isn't going to change.
	///
	[DllImport(Shell32, EntryPoint = "#727")]
	public static extern int SHGetImageList(SHIL iImageList, [In] ref Guid riid, out IntPtr ppv);

	[DllImport(Shell32, CharSet = CharSet.Unicode)]
	public static extern int SHGetFileInfo(string pszPath, FileAttribute dwFileAttributes, ref ShFileInfo psfi, int cbFileInfo, SHGFI uFlags);

	[DllImport(Shell32)]
	public static extern int SHGetFileInfo(IntPtr pszPath, FileAttribute dwFileAttributes, ref ShFileInfo psfi, int cbFileInfo, SHGFI uFlags);

	[DllImport(Shell32, CharSet = CharSet.Auto)]
	public static extern bool ShellExecuteEx(ref ShellExecuteInfo lpExecInfo);

	[DllImport(Shell32, BestFitMapping = false, CharSet = CharSet.Unicode)]
	public static extern IntPtr ExtractAssociatedIcon(ref IntPtr hInst, StringBuilder iconPath, ref int index);

	[DllImport(Shell32, SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CommandLineToArgvW")]
	public static extern IntPtr CommandLineToArgv(string lpCmdLine, out int pNumArgs);

	[DllImport(Shell32, SetLastError = true, EntryPoint = "#2", CharSet = CharSet.Auto)]
	public static extern uint SHChangeNotifyRegister(IntPtr hwnd, SHCNF fSources, SHCNE fEvents, uint wMsg, int cEntries, ref SHChangeNotifyEntry pFsne);

	[DllImport(Shell32, SetLastError = true, EntryPoint = "#4", CharSet = CharSet.Auto)]
	public static extern bool SHChangeNotifyUnregister(uint hNotify);

	[DllImport(Shell32)]
	public static extern uint SHFormatDrive(IntPtr hwnd, uint drive, uint fmtID, uint options);

	[DllImport(Shell32)]
	public static extern int SHOpenWithDialog(IntPtr hWndParent, ref OpenAsInfo oOAI);

	[DllImport(Shell32)]
	public static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);

	[DllImport("ole32.dll")]
	public static extern int CoCreateInstance(ref Guid clsid, [MarshalAs(UnmanagedType.IUnknown)] object? inner, uint context, ref Guid uuid, out IntPtr rReturnedComObject);

	[DllImport(Shell32, CharSet = CharSet.Unicode)]
	public static extern int SHAssocEnumHandlers(string pszExtra, AssocFilter afFilter, out IEnumAssocHandlers ppEnumHandler);

	[DllImport(Shell32, CharSet = CharSet.Unicode)]
	public static extern int SHCreateItemFromParsingName(string pszPath, IBindCtx? pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem ppv);

	[DllImport(Shell32, CharSet = CharSet.Unicode)]
	public static extern int SHCreateShellItemArrayFromIDLists(uint cidl, IntPtr[] rgpidl, out IShellItemArray ppsiItemArray);

	[DllImport(Shell32, CharSet = CharSet.Unicode)]
	public static extern int SHGetIDListFromObject([In, MarshalAs(UnmanagedType.IUnknown)] object punk, out IntPtr pidl);

	[Flags]
	public enum EmptyRecycleBinFlags {
		Default = 0x0,
		NoConfirmation = 0x1,
		NoProgressUI = 0x2,
		NoSound = 0x4
	}

	[DllImport(Shell32)]
	public static extern int SHEmptyRecycleBin(IntPtr hWnd, string pszRootPath, EmptyRecycleBinFlags dwFlags);

	public static T GetTypedObjectForIUnknown<T>(IntPtr pUnk) {
		return (T)Marshal.GetTypedObjectForIUnknown(pUnk, typeof(T));
	}

	/// <summary>
	/// 显示文件或者文件夹的属性面板
	/// </summary>
	/// <param name="item"></param>
	/// <returns></returns>
	public static void ShowProperties(FileListViewItem item) {
		var info = new ShellExecuteInfo {
			lpVerb = "properties",
			nShow = 5,
			fMask = 12  // SEE_MASK_INVOKEIDLIST
		};
		if (item is ISpecialFolder specialFolder) {
			info.lpIDList = specialFolder.IdList;
		} else {
			info.lpFile = item.FullPath;
		}
		info.cbSize = Marshal.SizeOf(info);
		ShellExecuteEx(ref info);
	}

	/// <summary>
	/// 显示一个格式化驱动器的对话框
	/// </summary>
	/// <param name="drive"></param>
	public static Task ShowFormatDriveDialog(DriveInfo drive) {
		return Task.Run(() => SHFormatDrive(IntPtr.Zero, (uint)(drive.Name[0] - 'A'), 0xFFFF, 0));
	}

	/// <summary>
	/// 显示一个 打开方式 对话框
	/// </summary>
	/// <param name="filePath"></param>
	public static void ShowOpenAsDialog(string filePath) {
		var info = new OpenAsInfo {
			cszClass = string.Empty,
			cszFile = filePath,
			oaifInFlags = OpenAsInfoFlags.AllowRegistration | OpenAsInfoFlags.Exec
		};
		SHOpenWithDialog(IntPtr.Zero, ref info);
	}

	/// <summary>
	/// 创建一个lnk快捷方式
	/// </summary>
	/// <param name="targetPath"></param>
	/// <param name="lnkPath"></param>
	/// <param name="description"></param>
	/// <param name="iconPath"></param>
	public static void CreateLnk(string targetPath, string lnkPath, string? description = null, string? iconPath = null) {
		Marshal.ThrowExceptionForHR(CoCreateInstance(ref CLSID_ShellLink, null, 1, ref GUID_IShellLink, out var pShellLink));
		var shellLink = GetTypedObjectForIUnknown<IShellLinkW>(pShellLink);
		shellLink.SetPath(targetPath);
		if (description != null) {
			shellLink.SetDescription(description);
		}
		if (iconPath != null) {
			shellLink.SetIconLocation(iconPath, 1);
		}
		var exception = Marshal.GetExceptionForHR(Marshal.QueryInterface(pShellLink, ref GUID_IPersistFile, out var pPersistFile));
		if (exception != null) {
			Marshal.ReleaseComObject(shellLink);
			throw exception;
		}
		var persistFile = GetTypedObjectForIUnknown<IPersistFile>(pPersistFile);
		exception = Marshal.GetExceptionForHR(persistFile.Save(lnkPath, true));
		Marshal.ReleaseComObject(persistFile);
		Marshal.ReleaseComObject(shellLink);
		if (exception != null) {
			throw exception;
		}
	}

	/// <summary>
	/// 获取lnk快捷方式的目标
	/// </summary>
	/// <param name="lnkPath"></param>
	public static string GetLnkTargetPath(string lnkPath) {
		Marshal.ThrowExceptionForHR(CoCreateInstance(ref CLSID_ShellLink, null, 1, ref GUID_IShellLink, out var pShellLink));
		var shellLink = GetTypedObjectForIUnknown<IShellLinkW>(pShellLink);
		var exception = Marshal.GetExceptionForHR(Marshal.QueryInterface(pShellLink, ref GUID_IPersistFile, out var pPersistFile));
		if (exception != null) {
			Marshal.ReleaseComObject(shellLink);
			throw exception;
		}
		var persistFile = GetTypedObjectForIUnknown<IPersistFile>(pPersistFile);
		exception = Marshal.GetExceptionForHR(persistFile.Load(lnkPath, 0));
		if (exception != null) {
			Marshal.ReleaseComObject(persistFile);
			Marshal.ReleaseComObject(shellLink);
			throw exception;
		}
		var sb = new StringBuilder(260);
		shellLink.GetPath(sb, 260, IntPtr.Zero, SLGP.RawPath);
		Marshal.ReleaseComObject(persistFile);
		Marshal.ReleaseComObject(shellLink);
		return sb.ToString();
	}

	public static IMalloc Malloc { get; }

	public static IShellFolder DesktopFolder { get; }

	public static IShellFolder2 RecycleBinFolder { get; }

	// ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
	private static readonly Thread loadIconThread;

	private static readonly ManualResetEvent loadIconEvent = new(false);

	private static readonly Queue<LoadIconParameters> loadIconParametersQueue = new();

	static Shell32Interop() {
		Marshal.ThrowExceptionForHR(SHGetMalloc(out var pMalloc));
		Malloc = GetTypedObjectForIUnknown<IMalloc>(pMalloc);

		Marshal.ThrowExceptionForHR(SHGetDesktopFolder(out var desktopFolder));
		DesktopFolder = desktopFolder;

		Marshal.ThrowExceptionForHR(SHGetSpecialFolderLocation(IntPtr.Zero, CSIDL.BitBucket, out var pidlRecycleBin));
		Marshal.ThrowExceptionForHR(DesktopFolder.BindToObject(pidlRecycleBin, IntPtr.Zero, GUID_IShellFolder2, out var pRecycleBin));
		RecycleBinFolder = GetTypedObjectForIUnknown<IShellFolder2>(pRecycleBin);
		Malloc.Free(pidlRecycleBin);

		loadIconThread = new Thread(LoadIconThreadWork) {
			IsBackground = true
		};
		loadIconThread.SetApartmentState(ApartmentState.STA);
		loadIconThread.Start();
	}

	public static void Initizlize() { }

	private record LoadIconParameters(int index, ILD flags, IntPtr pIcon) {
		public readonly int index = index;
		public readonly ILD flags = flags;
		public IntPtr pIcon = pIcon;
		public readonly ManualResetEvent continueEvent = new(false);
		public int hResult;
	}

	private static void LoadIconThreadWork() {
		Marshal.ThrowExceptionForHR(SHGetImageList(SHIL.Jumbo, ref GUID_IImageList, out var pJumboImageList));
		var jumboImageList = (IImageList)Marshal.GetTypedObjectForIUnknown(pJumboImageList, typeof(IImageList));

		while (true) {
			loadIconEvent.WaitOne();
			loadIconEvent.Reset();

			while (true) {
				LoadIconParameters parameters;
				lock (loadIconParametersQueue) {
					if (loadIconParametersQueue.Count == 0) {
						break;
					}
					parameters = loadIconParametersQueue.Dequeue();
				}
				parameters.hResult = jumboImageList.GetIcon(parameters.index, parameters.flags, ref parameters.pIcon);
				parameters.continueEvent.Set();
			}
		}
	}

	/// <summary>
	/// 在STA线程上执行，获取大图标
	/// </summary>
	/// <param name="i"></param>
	/// <param name="flags"></param>
	/// <param name="pIcon"></param>
	/// <returns></returns>
	public static int GetLargeIcon(int i, ILD flags, ref IntPtr pIcon) {
		var parameters = new LoadIconParameters(i, flags, pIcon);
		lock (loadIconParametersQueue) {
			loadIconParametersQueue.Enqueue(parameters);
		}
		loadIconEvent.Set();
		parameters.continueEvent.WaitOne();
		pIcon = parameters.pIcon;
		return parameters.hResult;
	}

	/// <summary>
	/// 在鼠标处显示文件或者文件夹的右键菜单
	/// </summary>
	/// <param name="fullPaths"></param>
	public static void ShowShellContextMenu(params string[] fullPaths) {
		var list = new List<(IntPtr, IShellItem)>();
		foreach (var fullPath in fullPaths) {
			if (string.IsNullOrWhiteSpace(fullPath)) {
				continue;
			}
			Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(fullPath, null, GUID_IShellItem, out var item));
			Marshal.ThrowExceptionForHR(SHGetIDListFromObject(item, out var pidl));
			if (pidl == IntPtr.Zero) {
				Marshal.ReleaseComObject(item);
				continue;
			}
			list.Add((pidl, item));
		}
		if (list.Count == 0) {
			return;
		}

		Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromIDLists((uint)list.Count, list.Select(i => i.Item1).ToArray(), out var itemArray));
		itemArray.BindToHandler(IntPtr.Zero, BHID_SFUIObject, GUID_IContextMenu, out var pMenu);
		var menu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(pMenu, typeof(IContextMenu));
		var hMenuCtx = CreatePopupMenu();

		var typeStrBuf = Marshal.AllocCoTaskMem(512);
		var mii = new MenuItemInfo {
			cbSize = (uint)Marshal.SizeOf<MenuItemInfo>(),
			fMask = MenuItemInfoMask.Bitmap | MenuItemInfoMask.FType | MenuItemInfoMask.String | MenuItemInfoMask.ID | MenuItemInfoMask.Submenu,
			dwTypeData = typeStrBuf,
			cch = 511
		};

		Marshal.ThrowExceptionForHR(menu.QueryContextMenu(hMenuCtx, 0, 1, 0x7FFF, CMF.Explore | CMF.ItemMenu));
		var contextMenu = new ContextMenu();
		var paramters = string.Join(' ', fullPaths);
		EnumerateContextMenu(contextMenu.Items, menu, hMenuCtx, typeStrBuf, ref mii, paramters);

		Marshal.FreeCoTaskMem(typeStrBuf);
		Marshal.ReleaseComObject(itemArray);
		foreach (var tuple in list) {
			Marshal.ReleaseComObject(tuple.Item2);
		}
		contextMenu.IsOpen = true;
		contextMenu.Closed += (_, _) => {
			DestroyMenu(hMenuCtx);
			Marshal.ReleaseComObject(menu);
		};
	}

	private static void EnumerateContextMenu(in ItemCollection collection, IContextMenu menu, IntPtr hMenuCtx, IntPtr typeStrBuf, ref MenuItemInfo mii, string paramters) {
		var itemCount = GetMenuItemCount(hMenuCtx);
		for (uint i = 0; i < itemCount; i++) {
			mii.cch = 511;
			var hr = GetMenuItemInfo(hMenuCtx, i, true, ref mii);
			if (!hr) {
				continue;
			}
			switch (mii.fType) {
			case MenuItemType.String: {
				var header = Marshal.PtrToStringUni(typeStrBuf, (int)mii.cch);
				var indexOfAnd = header.IndexOf('&');
				if (indexOfAnd != -1 && indexOfAnd < header.Length - 1) {
					// item.InputGestureText = char.ToUpper(header[indexOfAnd + 1]).ToString();
					if (indexOfAnd == 0) {
						header = header[1..];
					} else if (indexOfAnd < header.Length - 2 && header[indexOfAnd - 1] == '(' && header[indexOfAnd + 2] == ')') {
						header = header[..(indexOfAnd - 1)] + header[(indexOfAnd + 3)..];
					} else {
						header = header[..indexOfAnd] + header[(indexOfAnd + 1)..];
					}
				}
				var item = new MenuItem {
					Header = header
				};

				// 命令
				var id = GetMenuItemID(hMenuCtx, i);  
				if (id is not uint.MinValue and not uint.MaxValue) {
					item.Command = new SimpleCommand(() => {
						var cmi = new CtxMenuInvokeCommandInfo {
							cbSize = Marshal.SizeOf<CtxMenuInvokeCommandInfo>(),
							fMask = 0,
							hwnd = IntPtr.Zero,
							lpParameters = paramters,
							lpDirectory = null,
							lpVerb = (IntPtr)(id - 1),
							nShow = 1, // SW_SHOWNORMAL
							dwHotKey = 0,
							hIcon = IntPtr.Zero
						};
						menu.InvokeCommand(cmi);
						DestroyMenu(hMenuCtx);
						Marshal.ReleaseComObject(menu);
					});
				}

				// 图标
				if (mii.hbmpItem != IntPtr.Zero && !Enum.IsDefined(typeof(HBitmapHMenu), mii.hbmpItem.ToInt64())) {
					item.Icon = IconHelper.HBitmap2BitmapSource(mii.hbmpItem);
				}

				// 子菜单
				if (mii.hSubMenu != IntPtr.Zero) {
					try {
						// WM_INITMENUPOPUP弹出
						(menu as IContextMenu2)?.HandleMenuMsg(279u, mii.hSubMenu, new IntPtr(i));
					} catch {
						// Only for dynamic/owner drawn? (open with, etc)
					}
					EnumerateContextMenu(item.Items, menu, mii.hSubMenu, typeStrBuf, ref mii, paramters);
				}
				collection.Add(item);
				break;
			}
			case MenuItemType.Separator:
				collection.Add(new Separator());
				break;
			}
		}
	}
	// ReSharper restore InconsistentNaming
	// ReSharper restore IdentifierTypo
	// ReSharper restore StringLiteralTypo
	// ReSharper restore UnusedMember.Global
	// ReSharper restore FieldCanBeMadeReadOnly.Global
	// ReSharper restore UnusedType.Global
}