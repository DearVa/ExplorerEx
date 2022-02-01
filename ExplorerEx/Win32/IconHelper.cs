using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.Win32; 

internal static class IconHelper {
	public static DrawingImage FolderDrawingImage { get; private set; }
	public static DrawingImage EmptyFolderDrawingImage { get; private set; }
	public static DrawingImage UnknownTypeFileDrawingImage { get; private set; }

	public static void InitializeDefaultIcons(ResourceDictionary resources) {
		FolderDrawingImage = (DrawingImage)resources["FolderDrawingImage"];
		EmptyFolderDrawingImage = (DrawingImage)resources["EmptyFolderDrawingImage"];
		UnknownTypeFileDrawingImage = (DrawingImage)resources["UnknownTypeFileDrawingImage"];
	}

	private static BitmapSource Icon2BitmapSource(IntPtr hIcon) {
		var ic2 = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
		ic2.Freeze();
		return ic2;
	}

	public static BitmapSource SystemIcon(bool small, int csidl) {
		var pPid = IntPtr.Zero;
		var hr = SHGetSpecialFolderLocation(IntPtr.Zero, csidl, ref pPid);
		Debug.Assert(hr == 0);

		var shFileInfo = new SHFILEINFO();

		// Get a handle to the large icon
		uint flags;
		if (!small) {
			flags = SHGFI_PIDL | SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES;
		} else {
			flags = SHGFI_PIDL | SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES;
		}

		var res = SHGetFileInfo(pPid, 0, ref shFileInfo, Marshal.SizeOf(shFileInfo), flags);
		Debug.Assert(res != 0);

		var myIcon = shFileInfo.hIcon;
		Marshal.FreeCoTaskMem(pPid);
		var bs = Icon2BitmapSource(myIcon);
		DestroyIcon(shFileInfo.hIcon);
		return bs;
	}

	public static Task<ImageSource> GetPathIconAsync(string fileName, bool smallIcon, bool checkDisk, bool addOverlay) {
		return Task.Run(() => {
			var shFileInfo = new SHFILEINFO();

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

			var res = SHGetFileInfo(fileName, 0, ref shFileInfo, Marshal.SizeOf(shFileInfo), flags);
			if (res == 0) {
				Trace.WriteLine($"无法获取 {fileName} 的图标，Res: {res}");
				return UnknownTypeFileDrawingImage;
			}

			var bs = Icon2BitmapSource(shFileInfo.hIcon);
			DestroyIcon(shFileInfo.hIcon);
			return (ImageSource)bs;
		});
	}

	public static string GetFileTypeDescription(string fileNameOrExtension) {
		var shFileInfo = new SHFILEINFO();

		var res = SHGetFileInfo(fileNameOrExtension, FILE_ATTRIBUTE_NORMAL, ref shFileInfo, Marshal.SizeOf(shFileInfo), SHGFI_USEFILEATTRIBUTES | SHGFI_TYPENAME);
		if (res == 0) {
			Trace.WriteLine($"无法获取 {fileNameOrExtension} 的描述，Res: {res}");
			return null;
		}

		return shFileInfo.szTypeName;
	}

	public static Task<BitmapSource> GetLargePathIcon(string fileName, bool jumbo, bool checkDisk) {
		return Task.Run(() => {
			var shFileInfo = new SHFILEINFO();
			var flags = SHGFI_SYSICONINDEX;

			if (!checkDisk) {  // This does not seem to work. If I try it, a folder icon is always returned.
				flags |= SHGFI_USEFILEATTRIBUTES;
			}

			var res = SHGetFileInfo(fileName, FILE_ATTRIBUTE_NORMAL, ref shFileInfo, Marshal.SizeOf(shFileInfo), flags);
			if (res == 0) {
				throw new System.IO.FileNotFoundException();
			}
			var iconIndex = shFileInfo.iIcon;

			// Get the System IImageList object from the Shell:
			var iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

			var size = jumbo ? SHIL_JUMBO : SHIL_EXTRALARGE;
			var hr = SHGetImageList(size, ref iidImageList, out var iml); // writes iml
			if (hr != 0) {
				throw new Exception("Error SHGetImageList");
			}

			var hIcon = IntPtr.Zero;
			hr = iml.GetIcon(iconIndex, ILD_TRANSPARENT, ref hIcon);
			if (hr != 0) {
				throw new Exception("Error iml.GetIcon");
			}

			var bs = Icon2BitmapSource(hIcon);
			DestroyIcon(hIcon);
			return bs;
		});
	}
}