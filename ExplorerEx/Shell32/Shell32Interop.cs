using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
	public static extern int SHGetImageList(SHIL iImageList, [In] ref Guid riid, out IImageList ppv);

	[DllImport(Shell32, CharSet = CharSet.Ansi)]
	public static extern int SHGetFileInfo(string pszPath, FileAttribute dwFileAttributes, ref ShFileInfo psfi, int cbFileInfo, SHGFI uFlags);

	[DllImport(Shell32)]
	public static extern int SHGetFileInfo(IntPtr pszPath, FileAttribute dwFileAttributes, ref ShFileInfo psfi, int cbFileInfo, SHGFI uFlags);

	[DllImport(Shell32, CharSet = CharSet.Unicode, SetLastError = true)]
	internal static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

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
	public static extern int CoCreateInstance(ref Guid clsid, [MarshalAs(UnmanagedType.IUnknown)] object inner, uint context, ref Guid uuid, out IntPtr rReturnedComObject);

	[Flags]
	public enum EmptyRecycleBinFlags {
		Default = 0x0,
		NoConfirmation = 0x1,
		NoProgressUI = 0x2,
		NoSound = 0x4
	}

	[DllImport(Shell32)]
	public static extern int SHEmptyRecycleBin(IntPtr hWnd, string pszRootPath, EmptyRecycleBinFlags dwFlags);

	/// <summary>
	/// 显示文件的属性面板
	/// </summary>
	/// <param name="filePath"></param>
	/// <returns></returns>
	public static void ShowFileProperties(string filePath) {
		var info = new ShellExecuteInfo {
			lpVerb = "properties",
			lpFile = filePath ?? string.Empty,
			nShow = 5,
			fMask = 12
		};
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
	public static void CreateLnk(string targetPath, string lnkPath, string description = null, string iconPath = null) {
		Marshal.ThrowExceptionForHR(CoCreateInstance(ref CLSID_ShellLink, null, 1, ref GUID_IShellLink, out var pShellLink));
		var shellLink = (IShellLinkW)Marshal.GetTypedObjectForIUnknown(pShellLink, typeof(IShellLinkW));
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
		var persistFile = (IPersistFile)Marshal.GetTypedObjectForIUnknown(pPersistFile, typeof(IPersistFile));
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
		var shellLink = (IShellLinkW)Marshal.GetTypedObjectForIUnknown(pShellLink, typeof(IShellLinkW));
		var exception = Marshal.GetExceptionForHR(Marshal.QueryInterface(pShellLink, ref GUID_IPersistFile, out var pPersistFile));
		if (exception != null) {
			Marshal.ReleaseComObject(shellLink);
			throw exception;
		}
		var persistFile = (IPersistFile)Marshal.GetTypedObjectForIUnknown(pPersistFile, typeof(IPersistFile));
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

	public static IMalloc Malloc { get; private set; }

	public static IShellFolder DesktopFolder { get; private set; }

	public static IShellFolder2 RecycleBinFolder { get; private set; }

	public static IImageList JumboImageList { get; private set; }

	private static bool isInitialized;

	public static void Initialize() {
		lock (ShellLock) {
			if (isInitialized) {
				return;
			}

			Marshal.ThrowExceptionForHR(SHGetMalloc(out var pMalloc));
			Malloc = (IMalloc)Marshal.GetTypedObjectForIUnknown(pMalloc, typeof(IMalloc));

			Marshal.ThrowExceptionForHR(SHGetDesktopFolder(out var desktopFolder));
			DesktopFolder = desktopFolder;

			Marshal.ThrowExceptionForHR(SHGetSpecialFolderLocation(IntPtr.Zero, CSIDL.BitBucket, out var pidlRecycleBin));
			Marshal.ThrowExceptionForHR(DesktopFolder.BindToObject(pidlRecycleBin, IntPtr.Zero, GUID_IShellFolder2, out var pRecycleBin));
			RecycleBinFolder = (IShellFolder2)Marshal.GetTypedObjectForIUnknown(pRecycleBin, typeof(IShellFolder2));
			Malloc.Free(pidlRecycleBin);

			Marshal.ThrowExceptionForHR(SHGetImageList(SHIL.Jumbo, ref GUID_IImageList, out var imageList));
			JumboImageList = imageList;

			isInitialized = true;
		}
	}
	// ReSharper restore InconsistentNaming
	// ReSharper restore IdentifierTypo
	// ReSharper restore StringLiteralTypo
	// ReSharper restore UnusedMember.Global
	// ReSharper restore FieldCanBeMadeReadOnly.Global
	// ReSharper restore UnusedType.Global
}