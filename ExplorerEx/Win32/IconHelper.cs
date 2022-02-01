using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ExplorerEx.Win32; 

public static class IconHelper {
	// Constants that we need in the function call
	private const int SHGFI_ICON = 0x100;
	private const int SHGFI_SMALLICON = 0x1;
	private const int SHGFI_LARGEICON = 0x0;
	private const int SHIL_JUMBO = 0x4;
	private const int SHIL_EXTRALARGE = 0x2;

	// This structure will contain information about the file
	private struct SHFILEINFO {
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
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string szDisplayName;

		/// <summary>
		/// File type
		/// </summary>
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
		public string szTypeName;
	};

	[DllImport("Kernel32.dll")]
	private static extern bool CloseHandle(IntPtr handle);

	private struct IMAGELISTDRAWPARAMS {
		public int cbSize;
		public IntPtr himl;
		public int i;
		public IntPtr hdcDst;
		public int x;
		public int y;
		public int cx;
		public int cy;
		public int xBitmap;        // x offest from the upperleft of bitmap
		public int yBitmap;        // y offset from the upperleft of bitmap
		public int rgbBk;
		public int rgbFg;
		public int fStyle;
		public int dwRop;
		public int fState;
		public int Frame;
		public int crEffect;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT {
		public int x;
		public int y;
		public int width;
		public int height;
	}	
	
	[StructLayout(LayoutKind.Sequential)]
	private struct POINT {
		public int x;
		public int y;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct IMAGEINFO {
		public IntPtr hbmImage;
		public IntPtr hbmMask;
		public int Unused1;
		public int Unused2;
		public RECT rcImage;
	}

	#region Private ImageList COM Interop (XP)
	[ComImport]
	[Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	//helpstring("Image List"),
	private interface IImageList {
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
		int Draw(ref IMAGELISTDRAWPARAMS pimldp);

		[PreserveSig]
		int Remove(int i);

		[PreserveSig]
		int GetIcon(int i, int flags, ref IntPtr picon);

		[PreserveSig]
		int GetImageInfo(int i, ref IMAGEINFO pImageInfo);

		[PreserveSig]
		int Copy(int iDst, IImageList punkSrc, int iSrc, int uFlags);

		[PreserveSig]
		int Merge(int i1, IImageList punk2, int i2, int dx, int dy, ref Guid riid, ref IntPtr ppv);

		[PreserveSig]
		int Clone(ref Guid riid, ref IntPtr ppv);

		[PreserveSig]
		int GetImageRect(int i, ref RECT prc);

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
		int GetDragImage(ref POINT ppt, ref POINT pptHotspot, ref Guid riid, ref IntPtr ppv);

		[PreserveSig]
		int GetItemFlags(int i, ref int dwFlags);

		[PreserveSig]
		int GetOverlayImage(int iOverlay, ref int piIndex);
	};
	#endregion

	///
	/// SHGetImageList is not exported correctly in XP.  See KB316931
	/// http://support.microsoft.com/default.aspx?scid=kb;EN-US;Q316931
	/// Apparently (and hopefully) ordinal 727 isn't going to change.
	///
	[DllImport("shell32.dll", EntryPoint = "#727")]
	private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

	// The signature of SHGetFileInfo (located in Shell32.dll)
	[DllImport("Shell32.dll")]
	private static extern int SHGetFileInfo(string pszPath, int dwFileAttributes, ref SHFILEINFO psfi, int cbFileInfo, uint uFlags);

	[DllImport("Shell32.dll")]
	private static extern int SHGetFileInfo(IntPtr pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, int cbFileInfo, uint uFlags);

	[DllImport("shell32.dll", SetLastError = true)]
	private static extern int SHGetSpecialFolderLocation(IntPtr hwndOwner, int nFolder, ref IntPtr ppidl);

	[DllImport("user32")]
	private static extern int DestroyIcon(IntPtr hIcon);

	private static BitmapSource Icon2BitmapSource(IntPtr hIcon) {
		var ic2 = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
		ic2.Freeze();
		return ic2;
	}

	public static BitmapSource SystemIcon(bool small, int csidl) {
		var pidlTrash = IntPtr.Zero;
		var hr = SHGetSpecialFolderLocation(IntPtr.Zero, csidl, ref pidlTrash);
		Debug.Assert(hr == 0);

		var shinfo = new SHFILEINFO();

		const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

		// Get a handle to the large icon
		uint flags;
		const uint SHGFI_PIDL = 0x000000008;
		if (!small) {
			flags = SHGFI_PIDL | SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES;
		} else {
			flags = SHGFI_PIDL | SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES;
		}

		var res = SHGetFileInfo(pidlTrash, 0, ref shinfo, Marshal.SizeOf(shinfo), flags);
		Debug.Assert(res != 0);

		var myIcon = shinfo.hIcon;
		Marshal.FreeCoTaskMem(pidlTrash);
		var bs = Icon2BitmapSource(myIcon);
		bs.Freeze(); // importantissimo se no fa memory leak
		DestroyIcon(shinfo.hIcon);
		CloseHandle(shinfo.hIcon);
		return bs;
	}

	public static Task<BitmapSource> GetPathIcon(string fileName, bool smallIcon, bool checkDisk, bool addOverlay) {
		return Task.Run(() => {
			var shinfo = new SHFILEINFO();

			const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
			const uint SHGFI_LINKOVERLAY = 0x000008000;

			uint flags;
			if (smallIcon) {
				flags = SHGFI_ICON | SHGFI_SMALLICON;
			} else {
				flags = SHGFI_ICON | SHGFI_LARGEICON;
			}
			if (!checkDisk) {
				flags |= SHGFI_USEFILEATTRIBUTES;
			}
			if (addOverlay) {
				flags |= SHGFI_LINKOVERLAY;
			}

			var res = SHGetFileInfo(fileName, 0, ref shinfo, Marshal.SizeOf(shinfo), flags);
			if (res == 0) {
				throw new System.IO.FileNotFoundException();
			}

			var bs = Icon2BitmapSource(shinfo.hIcon);
			bs.Freeze(); // importantissimo se no fa memory leak
			DestroyIcon(shinfo.hIcon);
			return bs;
		});
	}

	public static Task<BitmapSource> GetLargePathIcon(string fileName, bool jumbo, bool checkDisk) {
		return Task.Run(() => {
			var shinfo = new SHFILEINFO();

			const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
			const uint SHGFI_SYSICONINDEX = 0x4000;

			var FILE_ATTRIBUTE_NORMAL = 0x80;

			var flags = SHGFI_SYSICONINDEX;

			if (!checkDisk) {  // This does not seem to work. If I try it, a folder icon is always returned.
				flags |= SHGFI_USEFILEATTRIBUTES;
			}

			var res = SHGetFileInfo(fileName, FILE_ATTRIBUTE_NORMAL, ref shinfo, Marshal.SizeOf(shinfo), flags);
			if (res == 0) {
				throw new System.IO.FileNotFoundException();
			}
			var iconIndex = shinfo.iIcon;

			// Get the System IImageList object from the Shell:
			var iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

			var size = jumbo ? SHIL_JUMBO : SHIL_EXTRALARGE;
			var hr = SHGetImageList(size, ref iidImageList, out var iml); // writes iml
			if (hr != 0) {
				throw new Exception("Error SHGetImageList");
			}

			var hIcon = IntPtr.Zero;
			const int ILD_TRANSPARENT = 1;
			hr = iml.GetIcon(iconIndex, ILD_TRANSPARENT, ref hIcon);
			if (hr != 0) {
				throw new Exception("Error iml.GetIcon");
			}

			var bs = Icon2BitmapSource(hIcon);
			bs.Freeze(); // very important to avoid memory leak
			DestroyIcon(hIcon);

			return bs;
		});
	}
}