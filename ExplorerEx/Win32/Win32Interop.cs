using ExplorerEx.Utils;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ExplorerEx.Win32;

internal static class Win32Interop {
	[DllImport("shlwapi.dll")]
	public static extern bool PathIsDirectoryEmpty(string path);

	[DllImport("user32.dll")]
	public static extern IntPtr SetCursor(IntPtr hCursor);

	[DllImport("user32.dll")]
	public static extern IntPtr LoadCursor(IntPtr hInstance, long lpCursorName);

	[DllImport("shell32.dll", BestFitMapping = false, CharSet = CharSet.Auto)]
	private static extern IntPtr ExtractAssociatedIcon(ref IntPtr hInst, StringBuilder iconPath, ref int index);

	[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true)]
	private static extern bool GetIconInfo(IntPtr hIcon, IntPtr pIconInfo);

	[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true)]
	private static extern bool DestroyIcon(IntPtr hIcon);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteDC(IntPtr hdc);

	[DllImport("gdi32.dll")]
	private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

	[DllImport("gdi32.dll")]
	private static extern int GetObject(IntPtr h, int c, IntPtr pv);

	[DllImport("gdi32.dll")]
	private static extern int GetDIBits(ref IntPtr hdc, ref IntPtr hbm, uint start, uint cLines, IntPtr lpvBits, IntPtr lpbmi, uint usage);

	[StructLayout(LayoutKind.Sequential)]
	private struct BITMAP {
		public int bmType;
		public int bmWidth;
		public int bmHeight;
		public int bmWidthBytes;
		public short bmPlanes;
		public short bmBitsPixel;
		public IntPtr bmBits;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BITMAPINFOHEADER {
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
	private struct BITMAPINFO {
		public BITMAPINFOHEADER bmiHeader;
		public IntPtr bmiColors;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct ICONINFO {
		public bool fIcon;
		public int xHotspot;
		public int yHotspot;
		public IntPtr hbmMask;
		public IntPtr hbmColor;
	}

	public static async Task<WriteableBitmap> ExtractAssociatedIconAsync(string filePath) {
		if (filePath == null) {
			throw new ArgumentNullException(nameof(filePath));
		}
		Uri uri;
		try {
			uri = new Uri(filePath);
		} catch (UriFormatException) {
			filePath = Path.GetFullPath(filePath);
			uri = new Uri(filePath);
		}
		if (uri.IsUnc) {
			throw new ArgumentException("Resources/Win32_ExtractAssociatedIcon_UNC_path_is_not_supported".L(), nameof(filePath));
		}
		if (uri.IsFile) {
			if (!File.Exists(filePath)) {
				// IntSecurity.DemandReadFileIO(filePath);
				throw new FileNotFoundException(filePath);
			}
			var sb = new StringBuilder(260);
			sb.Append(filePath);
			var index = 0;
			var nullPtr = IntPtr.Zero;
			var hIcon = ExtractAssociatedIcon(ref nullPtr, sb, ref index);
			if (hIcon != IntPtr.Zero) {
				var pIconInfo = Marshal.AllocHGlobal(Marshal.SizeOf<ICONINFO>());
				if (GetIconInfo(hIcon, pIconInfo)) {
					var iconInfo = Marshal.PtrToStructure<ICONINFO>(pIconInfo);
					var hdc = CreateCompatibleDC(IntPtr.Zero);
					var oldBitmap = SelectObject(hdc, iconInfo.hbmColor);

					var sizeOfBitmap = Marshal.SizeOf<BITMAP>();
					var pBitmap = Marshal.AllocHGlobal(sizeOfBitmap);
					if (GetObject(iconInfo.hbmColor, Marshal.SizeOf<BITMAP>(), pBitmap) == sizeOfBitmap) {
						var bmp = Marshal.PtrToStructure<BITMAP>(pBitmap);
						var info = new BITMAPINFO();
						info.bmiHeader.biSize = Marshal.SizeOf<BITMAPINFOHEADER>();
						info.bmiHeader.biWidth = bmp.bmWidth;
						info.bmiHeader.biHeight = bmp.bmHeight;
						info.bmiHeader.biPlanes = 1;
						info.bmiHeader.biBitCount = bmp.bmBitsPixel;
						info.bmiHeader.biCompression = 0;
						info.bmiHeader.biSizeImage = ((bmp.bmWidth * bmp.bmBitsPixel + 31) / 32) * 4 * bmp.bmHeight;

						var pInfo = Marshal.AllocHGlobal(Marshal.SizeOf<BITMAPINFO>());
						Marshal.StructureToPtr(info, pInfo, false);
						var bitmap = new WriteableBitmap(bmp.bmWidth, bmp.bmHeight, 96d, 96d, PixelFormats.Bgra32, null);
						bitmap.Lock();
						GetDIBits(ref hdc, ref pIconInfo, 0, (uint)bmp.bmHeight, bitmap.BackBuffer, pInfo, 0);
						bitmap.Unlock();
						bitmap.Freeze();
						SelectObject(hdc, oldBitmap);
						Marshal.FreeHGlobal(pInfo);
						Marshal.FreeHGlobal(pBitmap);

						DeleteDC(hdc);
						DestroyIcon(hIcon);
						Marshal.FreeHGlobal(pIconInfo);

						return bitmap;
					}
					Marshal.FreeHGlobal(pBitmap);

					DeleteDC(hdc);
					DestroyIcon(hIcon);
				}
				Marshal.FreeHGlobal(pIconInfo);
			}
		}
		return null;
	}

	public static void SetWestEastArrowCursor() {
		var cursor = LoadCursor(IntPtr.Zero, 32644L);
		SetCursor(cursor);
	}
}