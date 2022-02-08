using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ExplorerEx.Win32;

internal static class Win32Interop {
#pragma warning disable CS0649
	// ReSharper disable InconsistentNaming
	// ReSharper disable IdentifierTypo
	// ReSharper disable StringLiteralTypo
	// ReSharper disable UnusedMember.Global
	// ReSharper disable FieldCanBeMadeReadOnly.Global
	// ReSharper disable UnusedType.Global
	[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
	public static extern bool PathIsDirectoryEmpty(string path);

	[DllImport("user32.dll")]
	public static extern IntPtr SetCursor(IntPtr hCursor);

	[DllImport("user32.dll")]
	public static extern IntPtr LoadCursor(IntPtr hInstance, long lpCursorName);

	[DllImport("user32.dll")]
	public static extern bool GetCursorPos(ref Point point);

	[DllImport("shell32.dll", BestFitMapping = false, CharSet = CharSet.Unicode)]
	public static extern IntPtr ExtractAssociatedIcon(ref IntPtr hInst, StringBuilder iconPath, ref int index);

	[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true)]
	public static extern bool GetIconInfo(IntPtr hIcon, IntPtr pIconInfo);

	[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true)]
	public static extern bool DestroyIcon(IntPtr hIcon);

	[DllImport("user32.dll")]
	public static extern int GetDoubleClickTime();

	[DllImport("gdi32.dll")]
	public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

	[DllImport("gdi32.dll")]
	public static extern bool DeleteDC(IntPtr hdc);

	[DllImport("gdi32.dll")]
	public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

	[DllImport("gdi32.dll")]
	public static extern int GetObject(IntPtr h, int c, IntPtr pv);

	[DllImport("gdi32.dll")]
	public static extern int GetDIBits(ref IntPtr hdc, ref IntPtr hbm, uint start, uint cLines, IntPtr lpvBits, IntPtr lpbmi, uint usage);

	[DllImport("gdi32.dll")]
	public static extern bool DeleteObject(IntPtr hObject);

	[StructLayout(LayoutKind.Sequential)]
	public struct Bitmap {
		public int bmType;
		public int bmWidth;
		public int bmHeight;
		public int bmWidthBytes;
		public short bmPlanes;
		public short bmBitsPixel;
		public IntPtr bmBits;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct BitmapInfoHeader {
		public int biSize;
		public int biWidth;
		public int biHeight;
		public short biPlanes;
		public short biBitCount;
		public int biCompression;
		public int biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public int biClrUsed;
		public int biClrImportant;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct BitmapInfo {
		public BitmapInfoHeader bmiHeader;
		public IntPtr bmiColors;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct IconInfo {
		public bool fIcon;
		public int xHotspot;
		public int yHotspot;
		public IntPtr hbmMask;
		public IntPtr hbmColor;
	}

	// This structure will contain information about the file
	public struct ShFileInfo {
		/// <summary>
		/// Handle to the icon representing the file
		/// </summary>
		public IntPtr hIcon;

		/// <summary>
		/// Index of the icon within the image list
		/// </summary>
		public int iIcon;

		/// <summary>
		/// Various attributes of the file
		/// </summary>
		public uint dwAttributes;

		/// <summary>
		/// Path to the file
		/// </summary>
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
		public string szDisplayName;

		/// <summary>
		/// File type
		/// </summary>
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
		public string szTypeName;
	}

	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool CloseHandle(IntPtr handle);

	public struct ImageListDrawParams {
		public int cbSize;
		public IntPtr himl;
		public int i;
		public IntPtr hdcDst;
		public int x;
		public int y;
		public int cx;
		public int cy;
		public int xBitmap;        // x offset from the upperLeft of bitmap
		public int yBitmap;        // y offset from the upperLeft of bitmap
		public int rgbBk;
		public int rgbFg;
		public int fStyle;
		public int dwRop;
		public int fState;
		public int Frame;
		public int crEffect;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Rect {
		public int x;
		public int y;
		public int width;
		public int height;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Point {
		public int x;
		public int y;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct ImageInfo {
		public IntPtr hbmImage;
		public IntPtr hbmMask;
		public int Unused1;
		public int Unused2;
		public Rect rcImage;
	}

	#region 获取Icon
	[ComImport]
	[Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IImageList {
		[PreserveSig]
		int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);

		[PreserveSig]
		int ReplaceIcon(int i, IntPtr hicon, ref int pi);

		[PreserveSig]
		int SetOverlayImage(int iImage, int iOverlay);

		[PreserveSig]
		int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);

		[PreserveSig]
		int AddMasked(IntPtr hbmImage, int crMask, ref int pi);

		[PreserveSig]
		int Draw(ref ImageListDrawParams pimldp);

		[PreserveSig]
		int Remove(int i);

		[PreserveSig]
		int GetIcon(int i, int flags, ref IntPtr picon);

		[PreserveSig]
		int GetImageInfo(int i, ref ImageInfo pImageInfo);

		[PreserveSig]
		int Copy(int iDst, IImageList punkSrc, int iSrc, int uFlags);

		[PreserveSig]
		int Merge(int i1, IImageList punk2, int i2, int dx, int dy, ref Guid riid, ref IntPtr ppv);

		[PreserveSig]
		int Clone(ref Guid riid, ref IntPtr ppv);

		[PreserveSig]
		int GetImageRect(int i, ref Rect prc);

		[PreserveSig]
		int GetIconSize(ref int cx, ref int cy);

		[PreserveSig]
		int SetIconSize(int cx, int cy);

		[PreserveSig]
		int GetImageCount(ref int pi);

		[PreserveSig]
		int SetImageCount(int uNewCount);

		[PreserveSig]
		int SetBkColor(int clrBk, ref int pclr);

		[PreserveSig]
		int GetBkColor(ref int pclr);

		[PreserveSig]
		int BeginDrag(int iTrack, int dxHotspot, int dyHotspot);

		[PreserveSig]
		int EndDrag();

		[PreserveSig]
		int DragEnter(IntPtr hwndLock, int x, int y);

		[PreserveSig]
		int DragLeave(IntPtr hwndLock);

		[PreserveSig]
		int DragMove(int x, int y);

		[PreserveSig]
		int SetDragCursorImage(ref IImageList punk, int iDrag, int dxHotspot, int dyHotspot);

		[PreserveSig]
		int DragShowNolock(int fShow);

		[PreserveSig]
		int GetDragImage(ref Point ppt, ref Point pptHotspot, ref Guid riid, ref IntPtr ppv);

		[PreserveSig]
		int GetItemFlags(int i, ref int dwFlags);

		[PreserveSig]
		int GetOverlayImage(int iOverlay, ref int piIndex);
	};

	///
	/// SHGetImageList is not exported correctly in XP.  See KB316931
	/// http://support.microsoft.com/default.aspx?scid=kb;EN-US;Q316931
	/// Apparently (and hopefully) ordinal 727 isn't going to change.
	///
	[DllImport("shell32.dll", EntryPoint = "#727")]
	public static extern int SHGetImageList(uint iImageList, ref Guid riid, out IImageList ppv);

	[DllImport("Shell32.dll", CharSet = CharSet.Ansi)]
	public static extern int SHGetFileInfo(string pszPath, uint dwFileAttributes, ref ShFileInfo psfi, int cbFileInfo, uint uFlags);

	[DllImport("Shell32.dll")]
	public static extern int SHGetFileInfo(IntPtr pszPath, uint dwFileAttributes, ref ShFileInfo psfi, int cbFileInfo, uint uFlags);

	[DllImport("shell32.dll", SetLastError = true)]
	public static extern int SHGetSpecialFolderLocation(IntPtr hwndOwner, int nFolder, ref IntPtr ppidl);

	public const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
	public const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
	public const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
	public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
	public const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
	public const uint FILE_ATTRIBUTE_DEVICE = 0x00000040;
	public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
	public const uint FILE_ATTRIBUTE_TEMPORARY = 0x00000100;
	public const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;
	public const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
	public const uint FILE_ATTRIBUTE_COMPRESSED = 0x00000800;
	public const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
	public const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000;
	public const uint FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;
	public const uint FILE_ATTRIBUTE_VIRTUAL = 0x00010000;

	// Constants that we need in the function call
	public const uint SHIL_JUMBO = 0x4;
	public const uint SHIL_EXTRALARGE = 0x2;
	public const uint SHGFI_ICON = 0x000000100;     // get icon
	public const uint SHGFI_DISPLAYNAME = 0x000000200;     // get display name
	public const uint SHGFI_TYPENAME = 0x000000400;     // get type name
	public const uint SHGFI_ATTRIBUTES = 0x000000800;     // get attributes
	public const uint SHGFI_ICONLOCATION = 0x000001000;     // get icon location
	public const uint SHGFI_EXETYPE = 0x000002000;     // return exe type
	public const uint SHGFI_SYSICONINDEX = 0x000004000;     // get system icon index
	public const uint SHGFI_LINKOVERLAY = 0x000008000;     // put a link overlay on icon
	public const uint SHGFI_SELECTED = 0x000010000;     // show icon in selected state
	public const uint SHGFI_ATTR_SPECIFIED = 0x000020000;     // get only specified attributes
	public const uint SHGFI_LARGEICON = 0x000000000;     // get large icon
	public const uint SHGFI_SMALLICON = 0x000000001;     // get small icon
	public const uint SHGFI_OPENICON = 0x000000002;     // get open icon
	public const uint SHGFI_SHELLICONSIZE = 0x000000004;     // get shell size icon
	public const uint SHGFI_PIDL = 0x000000008;     // pszPath is a pidl
	public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;     // use passed dwFileAttribute

	public const int ILD_TRANSPARENT = 1;

	[DllImport("shell32.dll", CharSet = CharSet.Auto)]
	public static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	public struct SHELLEXECUTEINFO {
		public int cbSize;
		public uint fMask;
		public IntPtr hwnd;
		[MarshalAs(UnmanagedType.LPTStr)]
		public string lpVerb;
		[MarshalAs(UnmanagedType.LPTStr)]
		public string lpFile;
		[MarshalAs(UnmanagedType.LPTStr)]
		public string lpParameters;
		[MarshalAs(UnmanagedType.LPTStr)]
		public string lpDirectory;
		public int nShow;
		public IntPtr hInstApp;
		public IntPtr lpIDList;
		[MarshalAs(UnmanagedType.LPTStr)]
		public string lpClass;
		public IntPtr hkeyClass;
		public uint dwHotKey;
		public IntPtr hIcon;
		public IntPtr hProcess;
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
	internal interface IShellItem {
		void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
		void GetParent(out IShellItem ppsi);
		void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
		void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
		void Compare(IShellItem psi, uint hint, out int piOrder);
	}

	internal enum SIGDN : uint {
		NORMALDISPLAY = 0,
		PARENTRELATIVEPARSING = 0x80018001,
		PARENTRELATIVEFORADDRESSBAR = 0x8001c001,
		DESKTOPABSOLUTEPARSING = 0x80028000,
		PARENTRELATIVEEDITING = 0x80031001,
		DESKTOPABSOLUTEEDITING = 0x8004c000,
		FILESYSPATH = 0x80058000,
		URL = 0x80068000
	}

	[Flags]
	public enum ThumbnailOptions {  // IShellItemImageFactory Flags: https://msdn.microsoft.com/en-us/library/windows/desktop/bb761082%28v=vs.85%29.aspx
		None = 0x00,                // Shrink the bitmap as necessary to fit, preserving its aspect ratio. Returns thumbnail if available, else icon.
		BiggerSizeOk = 0x01,        // Passed by callers if they want to stretch the returned image themselves. For example, if the caller passes an icon size of 80x80, a 96x96 thumbnail could be returned. This action can be used as a performance optimization if the caller expects that they will need to stretch the image. Note that the Shell implementation of IShellItemImageFactory performs a GDI stretch blit. If the caller wants a higher quality image stretch than provided through that mechanism, they should pass this flag and perform the stretch themselves.
		InMemoryOnly = 0x02,        // Return the item only if it is already in memory. Do not access the disk even if the item is cached. Note that this only returns an already-cached icon and can fall back to a per-class icon if an item has a per-instance icon that has not been cached. Retrieving a thumbnail, even if it is cached, always requires the disk to be accessed, so GetImage should not be called from the UI thread without passing SIIGBF_MEMORYONLY.
		IconOnly = 0x04,            // Return only the icon, never the thumbnail.
		ThumbnailOnly = 0x08,       // Return only the thumbnail, never the icon. Note that not all items have thumbnails, so SIIGBF_THUMBNAILONLY will cause the method to fail in these cases.
		InCacheOnly = 0x10,         // Allows access to the disk, but only to retrieve a cached item. This returns a cached thumbnail if it is available. If no cached thumbnail is available, it returns a cached per-instance icon but does not extract a thumbnail or icon.
		Win8CropToSquare = 0x20,    // Introduced in Windows 8. If necessary, crop the bitmap to a square.
		Win8WideThumbnails = 0x40,  // Introduced in Windows 8. Stretch and crop the bitmap to a 0.7 aspect ratio.
		Win8IconBackground = 0x80,  // Introduced in Windows 8. If returning an icon, paint a background using the associated app's registered background color.
		Win8ScaleUp = 0x100         // Introduced in Windows 8. If necessary, stretch the bitmap so that the height and width fit the given size.
	}

	[ComImport]
	[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IShellItemImageFactory {
		[PreserveSig]
		int GetImage([In, MarshalAs(UnmanagedType.Struct)] Size size, [In] ThumbnailOptions flags, [Out] out IntPtr phbm);
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct Size {
		public int width;
		public int height;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct RGBQUAD {
		public byte rgbBlue;
		public byte rgbGreen;
		public byte rgbRed;
		public byte rgbReserved;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	internal static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

	#endregion

	private const int SW_SHOW = 5;
	private const uint SEE_MASK_INVOKEIDLIST = 12;

	/// <summary>
	/// 显示文件的属性面板
	/// </summary>
	/// <param name="filePath"></param>
	/// <returns></returns>
	public static bool ShowFileProperties(string filePath) {
		var info = new SHELLEXECUTEINFO();
		info.cbSize = Marshal.SizeOf(info);
		info.lpVerb = "properties";
		info.lpFile = filePath;
		info.nShow = SW_SHOW;
		info.fMask = SEE_MASK_INVOKEIDLIST;
		return ShellExecuteEx(ref info);
	}

	[DllImport("User32.dll", SetLastError = true)]
	public static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	public static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

	public const int WM_DRAWCLIPBOARD = 0x308;
	public const int WM_CHANGECBCHAIN = 0x030D;
	public const int WM_THEMECHANGED = 0x031A;
	public const int WM_DWMCOMPOSITIONCHANGED = 0x31E;
	public const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x320;

	#region 亚克力效果
	public enum AccentState {
		ACCENT_DISABLED = 0,
		ACCENT_ENABLE_GRADIENT = 1,
		ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
		ACCENT_ENABLE_BLURBEHIND = 3,
		ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
		ACCENT_INVALID_STATE = 5
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct AccentPolicy {
		public AccentState AccentState;
		public uint AccentFlags;
		public uint GradientColor;
		public uint AnimationId;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct WindowCompositionAttributeData {
		public WindowCompositionAttribute Attribute;
		public IntPtr Data;
		public int SizeOfData;
	}

	public enum WindowCompositionAttribute {
		WCA_ACCENT_POLICY = 19
	}

	[DllImport("user32.dll")]
	public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

	public enum DwmWindowAttribute {
		DWMWA_WINDOW_CORNER_PREFERENCE = 33,
		DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
		DWMWA_MICA_EFFECT = 1029
	}

	// The DWM_WINDOW_CORNER_PREFERENCE enum for DwmSetWindowAttribute's third parameter, which tells the function
	// what value of the enum to set.
	public enum DWM_WINDOW_CORNER_PREFERENCE {
		DWMWCP_DEFAULT = 0,
		DWMWCP_DONOTROUND = 1,
		DWMWCP_ROUND = 2,
		DWMWCP_ROUNDSMALL = 3
	}

	[DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern long DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute attribute, ref int pvAttribute, uint cbAttribute);
	#endregion

	#region 文件操作

	[DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	public static extern int SHFileOperation(SHFILEOPSTRUCT lpFileOp);

	/// <summary>
	/// Shell文件操作数据类型
	/// </summary>
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public class SHFILEOPSTRUCT {
		public IntPtr hwnd;
		/// <summary>
		/// 设置操作方式
		/// </summary>
		public FileOpType fileOpType;
		/// <summary>
		/// 源文件路径
		/// </summary>
		public string pFrom;
		/// <summary>
		/// 目标文件路径
		/// </summary>
		public string pTo;
		/// <summary>
		/// 允许恢复
		/// </summary>
		public FILEOP_FLAGS fFlags;
		/// <summary>
		/// 监测有无中止
		/// </summary>
		public bool fAnyOperationsAborted;
		/// <summary>
		/// 基本没啥用
		/// </summary>
		public IntPtr hNameMappings;
		/// <summary>
		/// 设置标题
		/// </summary>
		public string lpszProgressTitle;
	}


	/// <summary>
	/// 文件操作方式
	/// </summary>
	public enum FileOpType {
		/// <summary>
		/// 移动
		/// </summary>
		Move = 0x0001,
		/// <summary>
		/// 复制
		/// </summary>
		Copy = 0x0002,
		/// <summary>
		/// 删除
		/// </summary>
		Delete = 0x0003,
		/// <summary>
		/// 重命名
		/// </summary>
		Rename = 0x0004
	}

	/// <summary>
	/// 参见：http://msdn.microsoft.com/zh-cn/library/bb759795(v=vs.85).aspx
	/// </summary>
	[Flags]
	public enum FILEOP_FLAGS {
		/// <summary>
		/// pTo 指定了多个目标文件，而不是单个目录
		/// The pTo member specifies multiple destination files (one for each source file) rather than one directory where all source files are to be deposited.
		/// </summary>
		FOF_MULTIDESTFILES = 0x1,
		/// <summary>
		/// 不再使用
		/// Not currently used.
		/// </summary>
		FOF_CONFIRMMOUSE = 0x2,
		/// <summary>
		/// 不显示一个进度对话框
		/// Do not display a progress dialog box.
		/// </summary>
		FOF_SILENT = 0x4,
		/// <summary>
		/// 碰到有抵触的名字时，自动分配前缀
		/// Give the file being operated on a new name in a move, copy, or rename operation if a file with the target name already exists.
		/// </summary>
		FOF_RENAMEONCOLLISION = 0x8,
		/// <summary>
		/// 不对用户显示提示
		/// Respond with "Yes to All" for any dialog box that is displayed.
		/// </summary>
		FOF_NOCONFIRMATION = 0x10,
		/// <summary>
		/// 填充 hNameMappings 字段，必须使用 SHFreeNameMappings 释放
		/// If FOF_RENAMEONCOLLISION is specified and any files were renamed, assign a name mapping object containing their old and new names to the hNameMappings member.
		/// </summary>
		FOF_WANTMAPPINGHANDLE = 0x20,
		/// <summary>
		/// 允许撤销
		/// Preserve Undo information, if possible. If pFrom does not contain fully qualified path and file names, this flag is ignored.
		/// </summary>
		FOF_ALLOWUNDO = 0x40,
		/// <summary>
		/// 使用 *.* 时, 只对文件操作
		/// Perform the operation on files only if a wildcard file name (*.*) is specified.
		/// </summary>
		FOF_FILESONLY = 0x80,
		/// <summary>
		/// 简单进度条，意味着不显示文件名。
		/// Display a progress dialog box but do not show the file names.
		/// </summary>
		FOF_SIMPLEPROGRESS = 0x100,
		/// <summary>
		/// 建新目录时不需要用户确定
		/// Do not confirm the creation of a new directory if the operation requires one to be created.
		/// </summary>
		FOF_NOCONFIRMMKDIR = 0x200,
		/// <summary>
		/// 不显示出错用户界面
		/// Do not display a user interface if an error occurs.
		/// </summary>
		FOF_NOERRORUI = 0x400,
		/// <summary>
		///  不复制 NT 文件的安全属性
		/// Do not copy the security attributes of the file.
		/// </summary>
		FOF_NOCOPYSECURITYATTRIBS = 0x800,
		/// <summary>
		///  不递归目录
		/// Only operate in the local directory. Don't operate recursively into subdirectories.
		/// </summary>
		FOF_NORECURSION = 0x1000,
		/// <summary>
		/// Do not move connected files as a group. Only move the specified files.
		/// </summary>
		FOF_NO_CONNECTED_ELEMENTS = 0x2000,
		/// <summary>
		/// Send a warning if a file is being destroyed during a delete operation rather than recycled. This flag partially overrides FOF_NOCONFIRMATION.
		/// </summary>
		FOF_WANTNUKEWARNING = 0x4000,
		/// <summary>
		/// Treat reparse points as objects, not containers.
		/// </summary>
		FOF_NORECURSEREPARSE = 0x8000,
	}
	#endregion

	#region 获取进程命令行

	public const uint PROCESS_BASIC_INFORMATION = 0;

	[Flags]
	public enum OpenProcessDesiredAccessFlags : uint {
		PROCESS_VM_READ = 0x0010,
		PROCESS_QUERY_INFORMATION = 0x0400,
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct ProcessBasicInformation {
		public IntPtr Reserved1;
		public IntPtr PebBaseAddress;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
		public IntPtr[] Reserved2;
		public IntPtr UniqueProcessId;
		public IntPtr Reserved3;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct UnicodeString {
		public ushort Length;
		public ushort MaximumLength;
		public IntPtr Buffer;
	}

	// This is not the real struct!
	// I faked it to get ProcessParameters address.
	// Actual struct definition:
	// https://docs.microsoft.com/en-us/windows/win32/api/winternl/ns-winternl-peb
	[StructLayout(LayoutKind.Sequential)]
	public struct PEB {
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public IntPtr[] Reserved;
		public IntPtr ProcessParameters;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct RtlUserProcessParameters {
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] Reserved1;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
		public IntPtr[] Reserved2;
		public UnicodeString ImagePathName;
		public UnicodeString CommandLine;
	}

	[DllImport("ntdll.dll")]
	public static extern uint NtQueryInformationProcess(IntPtr ProcessHandle, uint ProcessInformationClass, IntPtr ProcessInformation, uint ProcessInformationLength, out uint ReturnLength);

	[DllImport("kernel32.dll")]
	public static extern IntPtr OpenProcess(OpenProcessDesiredAccessFlags dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, uint nSize, out uint lpNumberOfBytesRead);


	[DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CommandLineToArgvW")]
	public static extern IntPtr CommandLineToArgv(string lpCmdLine, out int pNumArgs);

	private static bool ReadStructFromProcessMemory<TStruct>(
		IntPtr hProcess, IntPtr lpBaseAddress, out TStruct val) {
		val = default;
		var structSize = Marshal.SizeOf<TStruct>();
		var mem = Marshal.AllocHGlobal(structSize);
		try {
			if (ReadProcessMemory(
				hProcess, lpBaseAddress, mem, (uint)structSize, out var len) &&
				(len == structSize)) {
				val = Marshal.PtrToStructure<TStruct>(mem);
				return true;
			}
		} finally {
			Marshal.FreeHGlobal(mem);
		}
		return false;
	}

	public static string GetCommandLine(this Process process) {
		var hProcess = OpenProcess(
			OpenProcessDesiredAccessFlags.PROCESS_QUERY_INFORMATION |
			OpenProcessDesiredAccessFlags.PROCESS_VM_READ, false, (uint)process.Id);
		if (hProcess == IntPtr.Zero) {
			throw new ApplicationException("Couldn't open process for VM read");
		}
		try {
			var sizePBI = Marshal.SizeOf<ProcessBasicInformation>();
			var memPBI = Marshal.AllocHGlobal(sizePBI);
			try {
				var ret = NtQueryInformationProcess(hProcess, PROCESS_BASIC_INFORMATION, memPBI, (uint)sizePBI, out _);
				if (ret != 0) {
					throw new ApplicationException("NtQueryInformationProcess failed");
				}
				var pbiInfo = Marshal.PtrToStructure<ProcessBasicInformation>(memPBI);
				if (pbiInfo.PebBaseAddress == IntPtr.Zero) {
					throw new ApplicationException("PebBaseAddress is null");
				}
				if (!ReadStructFromProcessMemory<PEB>(hProcess, pbiInfo.PebBaseAddress, out var pebInfo)) {
					throw new ApplicationException("Couldn't read PEB information");
				}
				if (!ReadStructFromProcessMemory<RtlUserProcessParameters>(hProcess, pebInfo.ProcessParameters, out var ruppInfo)) {
					throw new ApplicationException("Couldn't read ProcessParameters");
				}
				var clLen = ruppInfo.CommandLine.MaximumLength;
				var memCL = Marshal.AllocHGlobal(clLen);
				try {
					if (ReadProcessMemory(hProcess, ruppInfo.CommandLine.Buffer, memCL, clLen, out _)) {
						return Marshal.PtrToStringUni(memCL);
					} else {
						throw new Win32Exception("Couldn't read command line buffer");
					}
				} finally {
					Marshal.FreeHGlobal(memCL);
				}
			} finally {
				Marshal.FreeHGlobal(memPBI);
			}
		} finally {
			CloseHandle(hProcess);
		}
	}

	#endregion
	// ReSharper restore InconsistentNaming
	// ReSharper restore IdentifierTypo
	// ReSharper restore StringLiteralTypo
	// ReSharper restore UnusedMember.Global
	// ReSharper restore FieldCanBeMadeReadOnly.Global
	// ReSharper restore UnusedType.Global
#pragma warning restore CS0649
}