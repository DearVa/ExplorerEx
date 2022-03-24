using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ExplorerEx.Model;
using System.Windows.Documents;
using ExplorerEx.Utils;

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

	[DllImport(Shell32)]
	public static extern uint SHFormatDrive(IntPtr hwnd, uint drive, uint fmtID, uint options);

	/// <summary>
	/// 显示文件的属性面板
	/// </summary>
	/// <param name="filePath"></param>
	/// <returns></returns>
	public static void ShowFileProperties(string filePath) {
		var info = new ShellExecuteInfo();
		info.cbSize = Marshal.SizeOf(info);
		info.lpVerb = "properties";
		info.lpFile = filePath ?? string.Empty;
		info.nShow = 5;
		info.fMask = 12;
		ShellExecuteEx(ref info);
	}

	/// <summary>
	/// 显示一个格式化驱动器的对话框
	/// </summary>
	/// <param name="drive"></param>
	public static Task ShowFormatDriveDialog(DriveInfo drive) {
		return Task.Run(() => SHFormatDrive(IntPtr.Zero, (uint)(drive.Name[0] - 'A'), 0xFFFF, 0));
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