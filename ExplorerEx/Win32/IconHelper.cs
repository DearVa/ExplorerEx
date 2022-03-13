using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
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
	public static BitmapImage ComputerBitmapImage { get; } = new(new Uri("pack://application:,,,/ExplorerEx;component/Assets/Picture/Computer.png"));

	/// <summary>
	/// 可以获取缩略图的文件格式
	/// </summary>
	private static readonly HashSet<string> ExtensionsWithThumbnail = new() {
		".exe", ".lnk",
		".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".gif", ".svg",
		".mp3", ".flac", 
		".mp4", ".wmv", ".avi", 
		".docx", ".pptx", ".pdf"
	};

	/// <summary>
	/// 如dll等文件，其图标都一样，就存拓展名下来，直接取，不用每次都生成
	/// </summary>
	private static readonly Dictionary<string, ImageSource> CachedIcons = new();
	private static readonly Dictionary<string, ImageSource> CachedLargeIcons = new();

	private static readonly Dictionary<string, string> CachedDescriptions = new();

	private static readonly object ShellLock = new();

	public static void InitializeDefaultIcons(ResourceDictionary resources) {
		FolderDrawingImage = (DrawingImage)resources["FolderDrawingImage"];
		EmptyFolderDrawingImage = (DrawingImage)resources["EmptyFolderDrawingImage"];
		UnknownTypeFileDrawingImage = (DrawingImage)resources["UnknownTypeFileDrawingImage"];
	}

	private static BitmapSource HIcon2BitmapSource(IntPtr hIcon) {
		var bitmap = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
		bitmap.Freeze();
		return bitmap;
	}

	private static BitmapSource HBitmap2BitmapSource(IntPtr hBitmap) {
		var bitmap = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
		bitmap.Freeze();
		return bitmap;
	}

	/// <summary>
	/// 获取文件的小图标
	/// </summary>
	/// <param name="path">文件路径</param>
	/// <param name="useFileAttr">如果为true，那就去找文件本身并生成缩略图（比如exe这种）；如果为false，那就只根据拓展名生成缩略图</param>
	/// <returns></returns>
	public static ImageSource GetPathIcon(string path, bool useFileAttr) {
		var extension = Path.GetExtension(path);
		if (string.IsNullOrEmpty(extension)) {
			return UnknownTypeFileDrawingImage;
		}
		var isLnk = extension == ".lnk";
		var useCache = extension != ".exe" && !isLnk;
		lock (CachedIcons) {
			if (useCache && CachedIcons.TryGetValue(extension, out var icon)) {
				return icon;
			}
		}

		var dwFa = useFileAttr ? FILE_ATTRIBUTE_NORMAL : 0;
		var flags = SHGFI_ICON | SHGFI_LARGEICON;
		if (useFileAttr) {
			flags |= SHGFI_USEFILEATTRIBUTES;
		} else if (isLnk) {
			flags |= SHGFI_LINKOVERLAY;
		}

		var shFileInfo = new ShFileInfo();
		Monitor.Enter(ShellLock);
		var res = SHGetFileInfo(path, dwFa, ref shFileInfo, Marshal.SizeOf(shFileInfo), flags);
		Monitor.Exit(ShellLock);
		if (res == 0 || shFileInfo.hIcon == IntPtr.Zero) {
			Trace.WriteLine($"无法获取 {path} 的图标，Res: {res}");
			return UnknownTypeFileDrawingImage;
		}

		var result = (ImageSource)HIcon2BitmapSource(shFileInfo.hIcon);
		DestroyIcon(shFileInfo.hIcon);

		if (useCache) {
			lock (CachedIcons) {
				CachedIcons.TryAdd(extension, result);
			}
		}
		return result;
	}

	public static string GetFileTypeDescription(string extension) {
		if (CachedDescriptions.TryGetValue(extension, out var desc)) {
			return desc;
		}

		var shFileInfo = new ShFileInfo();
		Monitor.Enter(ShellLock);
		var res = SHGetFileInfo(extension, FILE_ATTRIBUTE_NORMAL, ref shFileInfo, Marshal.SizeOf(shFileInfo), SHGFI_USEFILEATTRIBUTES | SHGFI_TYPENAME);
		Monitor.Exit(ShellLock);
		if (res == 0 || shFileInfo.szTypeName == null) {
			Trace.WriteLine($"无法获取 {extension} 的描述，Res: {res}");
			return string.Empty;
		}
		
		CachedDescriptions.Add(extension, shFileInfo.szTypeName);
		return shFileInfo.szTypeName;
	}

	/// <summary>
	/// 获取文件或驱动器的缩略图，如果文件没有缩略图，就获取高清图标
	/// </summary>
	/// <param name="path"></param>
	/// <returns></returns>
	/// <exception cref="FileNotFoundException"></exception>
	/// <exception cref="Exception"></exception>
	public static ImageSource GetPathThumbnail(string path) {
		// Trace.WriteLine($"加载缩略图：{path}");
		string extension = null;
		if (path.Length > 3) {  // 说明不是驱动器
			extension = Path.GetExtension(path);
			if (string.IsNullOrEmpty(extension)) {
				return UnknownTypeFileDrawingImage;
			}
			if (ExtensionsWithThumbnail.Contains(extension)) {
				// Trace.WriteLine($"文件有缩略图，加载：{path}");
				var shellItem2Guid = new Guid("7E9FB0D3-919F-4307-AB2E-9B1860310C93");
				var retCode = SHCreateItemFromParsingName(path, IntPtr.Zero, ref shellItem2Guid, out var nativeShellItem);
				if (retCode != 0) {
					// 发生错误，fallback to加载大图标
					Trace.WriteLine($"加载缩略图出错：{path}");
					return LoadLargeIcon(path, extension);
				}
				var size = new Win32Interop.Size {
					width = 128,
					height = 128
				};
				// ReSharper disable once SuspiciousTypeConversion.Global
				var hr = ((IShellItemImageFactory)nativeShellItem).GetImage(size, ThumbnailOptions.ThumbnailOnly, out var hBitmap);
				Marshal.ReleaseComObject(nativeShellItem);
				if (hr != 0 || hBitmap == IntPtr.Zero) {
					// 发生错误，fallback to加载大图标
					Trace.WriteLine($"加载缩略图出错：{path}");
					return LoadLargeIcon(path, extension);
				}
				var bs = (ImageSource)HBitmap2BitmapSource(hBitmap);
				DeleteObject(hBitmap);
				return bs;
			}
			lock (CachedLargeIcons) {
				if (CachedLargeIcons.TryGetValue(extension, out var image)) {
					// Trace.WriteLine($"使用已缓存的大图标：{path}");
					return image;
				}
			}
		}
		return LoadLargeIcon(path, extension);
	}

	private static ImageSource LoadLargeIcon(string path, string extension) {
		// Trace.WriteLine($"开始加载大图标：{path}");
		var shFileInfo = new ShFileInfo();
		var res = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shFileInfo, Marshal.SizeOf(shFileInfo), SHGFI_SYSICONINDEX);
		if (res == 0) {
			return UnknownTypeFileDrawingImage;
		}

		var iconIndex = shFileInfo.iIcon;
		var iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
		var hr = SHGetImageList(SHIL_JUMBO, ref iidImageList, out var iml); // writes iml
		if (hr != 0) {
			return UnknownTypeFileDrawingImage;
		}

		var hIcon = IntPtr.Zero;
		hr = iml.GetIcon(iconIndex, ILD_TRANSPARENT, ref hIcon);
		if (hr != 0 || hIcon == IntPtr.Zero) {
			return UnknownTypeFileDrawingImage;
		}

		var bs = (ImageSource)HIcon2BitmapSource(hIcon);
		DestroyIcon(hIcon);

		if (extension != null) {
			lock (CachedLargeIcons) {
				// Trace.WriteLine($"将大图标加入缓存：{path}");
				CachedLargeIcons.TryAdd(extension, bs);
			}
		}
		return bs;
	}
}