using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.Win32; 

internal static class IconHelper {
	public static DrawingImage FolderDrawingImage { get; private set; }
	public static DrawingImage EmptyFolderDrawingImage { get; private set; }
	public static DrawingImage UnknownTypeFileDrawingImage { get; private set; }

	/// <summary>
	/// 这些文件缩略图要每次都加载，不能缓存
	/// </summary>
	private static readonly HashSet<string> NoIconCacheExtensions = new() {
		".exe", ".lnk", ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".gif", ".svg",
		".mp3", ".wav", ".flac", ".mp4", ".wmv", ".avi", ".docx", ".pptx", ".pdf"
	};

	/// <summary>
	/// 如dll等文件，其图标都一样，就存拓展名下来，直接取，不用每次都生成
	/// </summary>
	private static readonly Dictionary<string, ImageSource> CachedIcons = new();

	private static readonly Dictionary<string, string> CachedDescriptions = new();

	public static void InitializeDefaultIcons(ResourceDictionary resources) {
		FolderDrawingImage = (DrawingImage)resources["FolderDrawingImage"];
		EmptyFolderDrawingImage = (DrawingImage)resources["EmptyFolderDrawingImage"];
		UnknownTypeFileDrawingImage = (DrawingImage)resources["UnknownTypeFileDrawingImage"];
	}

	private static BitmapSource Icon2BitmapSource(IntPtr hIcon) {
		var ic2 = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
		ic2.Freeze();
		return ic2;
	}

	public static Task<ImageSource> GetPathIconAsync(string fileName, bool smallIcon, bool checkDisk, bool addOverlay) {
		var extension = Path.GetExtension(fileName);
		if (string.IsNullOrEmpty(extension)) {
			return Task.FromResult((ImageSource)UnknownTypeFileDrawingImage);
		}
		var useCache = extension is not ".exe" or ".lnk";
		if (useCache && CachedIcons.TryGetValue(extension, out var icon)) {
			return Task.FromResult(icon);
		}

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

			var icon = (ImageSource)Icon2BitmapSource(shFileInfo.hIcon);
			DestroyIcon(shFileInfo.hIcon);
			if (useCache) {
				lock (CachedIcons) {
					CachedIcons.Add(extension, icon);
				}
			}

			return icon;
		});
	}

	public static string GetFileTypeDescription(string extension) {
		if (CachedDescriptions.TryGetValue(extension, out var desc)) {
			return desc;
		}

		var shFileInfo = new SHFILEINFO();
		var res = SHGetFileInfo(extension, FILE_ATTRIBUTE_NORMAL, ref shFileInfo, Marshal.SizeOf(shFileInfo), SHGFI_USEFILEATTRIBUTES | SHGFI_TYPENAME);
		if (res == 0) {
			Trace.WriteLine($"无法获取 {extension} 的描述，Res: {res}");
			return null;
		}
		
		CachedDescriptions.Add(extension, shFileInfo.szTypeName);
		return shFileInfo.szTypeName;
	}

	public static Task<ImageSource> GetLargePathIcon(string fileName, bool jumbo, bool checkDisk) {
		if (fileName.Length > 3) {
			var extension = Path.GetExtension(fileName);
			if (string.IsNullOrEmpty(extension)) {
				return Task.FromResult((ImageSource)UnknownTypeFileDrawingImage);
			}
			var useCache = !NoIconCacheExtensions.Contains(extension);
			if (useCache && CachedIcons.TryGetValue(extension, out var icon)) {
				return Task.FromResult(icon);
			}
		}

		return Task.Run(() => {
			var shFileInfo = new SHFILEINFO();
			var flags = SHGFI_SYSICONINDEX;

			if (!checkDisk) {  // This does not seem to work. If I try it, a folder icon is always returned.
				flags |= SHGFI_USEFILEATTRIBUTES;
			}

			var res = SHGetFileInfo(fileName, FILE_ATTRIBUTE_NORMAL, ref shFileInfo, Marshal.SizeOf(shFileInfo), flags);
			if (res == 0) {
				throw new FileNotFoundException();
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

			var bs = (ImageSource)Icon2BitmapSource(hIcon);
			DestroyIcon(hIcon);
			return bs;
		});
	}
}