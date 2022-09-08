using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExplorerEx.Shell32;
using SharpSvgImage.Svg;
using static ExplorerEx.Shell32.Shell32Interop;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.Utils;

internal static class IconHelper {
	public static DrawingImage FolderDrawingImage { get; }
	public static DrawingImage EmptyFolderDrawingImage { get; }
	public static DrawingImage UnknownFileDrawingImage { get; }
	public static DrawingImage MissingFileDrawingImage { get; }
	public static BitmapImage ComputerBitmapImage { get; } = new(new Uri("pack://application:,,,/ExplorerEx;component/Assets/Picture/Computer.png"));

	/// <summary>
	/// 可以获取缩略图的文件格式
	/// </summary>
	private static readonly HashSet<string> ExtensionsWithThumbnail = new() {
		".lnk",
		".jpg",
		".jpeg",
		".png",
		".bmp",
		".tif",
		".tiff",
		".gif",
		".svg",
		".mp3",
		".flac",
		".avi",
		".wmv",
		".mpeg",
		".mp4",
		".m4v",
		".mov",
		".asf",
		".flv",
		".f4v",
		".rmvb",
		".rm",
		".3gp",
		".vob",
		".docx",
		".pptx",
		".pdf"
	};

	/// <summary>
	/// 如dll等文件，其图标都一样，就存拓展名下来，直接取，不用每次都生成
	/// </summary>
	private static readonly Dictionary<string, ImageSource> CachedIcons = new();
	private static readonly Dictionary<string, ImageSource> CachedLargeIcons = new();
	private static readonly Dictionary<string, ImageSource> CachedDriveIcons = new();

	static IconHelper() {
		var resources = Application.Current.Resources;
		FolderDrawingImage = (DrawingImage)resources["FolderDrawingImage"];
		EmptyFolderDrawingImage = (DrawingImage)resources["EmptyFolderDrawingImage"];
		UnknownFileDrawingImage = (DrawingImage)resources["UnknownFileDrawingImage"];
		MissingFileDrawingImage = (DrawingImage)resources["MissingFileDrawingImage"];
	}

	/// <summary>
	/// 空函数，目的只是为了确保在UI线程调用静态构造方法
	/// </summary>
	public static void Initialize() { }

	public static BitmapSource HIcon2BitmapSource(IntPtr hIcon) {
		var bitmap = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
		bitmap.Freeze();
		return bitmap;
	}

	public static BitmapSource HBitmap2BitmapSource(IntPtr hBitmap) {
		var bitmap = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
		bitmap.Freeze();
		return bitmap;
	}

	public static ImageSource Svg2ImageSource(string svgPath) {
		var svg = new SvgRender();
		return new DrawingImage(svg.LoadDrawing(svgPath));
	}

	/// <summary>
	/// 获取文件的小图标
	/// </summary>
	/// <param name="path">文件路径</param>
	/// <param name="useFileAttr">如果为false，那就去找文件本身并生成缩略图（比如exe这种）；如果为true，那就只根据拓展名生成缩略图</param>
	/// <returns></returns>
	public static ImageSource GetSmallIcon(string path, bool useFileAttr) {
		var extension = Path.GetExtension(path);
		if (string.IsNullOrEmpty(extension)) {
			return UnknownFileDrawingImage;
		}
		extension = extension.ToLower();
		var isLnk = extension == ".lnk";
		var useCache = extension is not ".exe" and not ".ico" && !isLnk;
		if (useCache) {
			lock (CachedIcons) {
				if (CachedIcons.TryGetValue(extension, out var icon)) {
					return icon;
				}
			}
		}

		if (extension == ".svg") {
			return Application.Current.Dispatcher.Invoke(() => Svg2ImageSource(path));
		}

		var dwFa = useFileAttr ? FileAttribute.Normal : 0;
		var flags = SHGFI.Icon | SHGFI.LargeIcon;
		if (useFileAttr) {
			flags |= SHGFI.UseFileAttributes;
		} else if (isLnk) {
			flags |= SHGFI.LinkOverlay;
		}

		var shFileInfo = new ShFileInfo();
		var res = SHGetFileInfo(path, dwFa, ref shFileInfo, Marshal.SizeOf(shFileInfo), flags);
		if (res == 0 || shFileInfo.hIcon == IntPtr.Zero) {
#if DEBUG
			Trace.WriteLine($"无法获取 {path} 的图标，Res: {res}");
#endif
			return UnknownFileDrawingImage;
		}

		var result = (ImageSource)HIcon2BitmapSource(shFileInfo.hIcon);
		DestroyIcon(shFileInfo.hIcon);

		if (useCache) {
			lock (CachedIcons) {
				CachedIcons[extension] = result;
			}
		}
		return result;
	}

	/// <summary>
	/// 获取文件的大图标
	/// </summary>
	/// <param name="path">文件路径</param>
	/// <param name="useFileAttr">如果为false，那就去找文件本身并生成缩略图（比如exe这种）；如果为true，那就只根据拓展名生成缩略图</param>
	/// <returns></returns>
	public static ImageSource GetLargeIcon(string path, bool useFileAttr) {
		var extension = Path.GetExtension(path);
		var useCache = useFileAttr && path.Length != 3;  // 是硬盘根路径

		if (path.Length != 3 && string.IsNullOrEmpty(extension)) {
			return UnknownFileDrawingImage;
		}

		extension = extension.ToLower();
		if (useCache) {
			lock (CachedLargeIcons) {
				if (CachedLargeIcons.TryGetValue(extension, out var icon)) {
					return icon;
				}
			}
		}
		
		var dwFa = useFileAttr ? FileAttribute.Normal : 0;
		var flags = SHGFI.SysIconIndex;
		if (useFileAttr) {
			flags |= SHGFI.UseFileAttributes;
		} else if (extension == ".lnk") {
			flags |= SHGFI.LinkOverlay;
		}

		var shFileInfo = new ShFileInfo();
		
		var hr = SHGetFileInfo(path, dwFa, ref shFileInfo, Marshal.SizeOf(shFileInfo), flags);
		// Trace.WriteLine(path + ' ' + hr);
		if (hr == 0) {
			return UnknownFileDrawingImage;
		}

		var iconIndex = shFileInfo.iIcon;
		var hIcon = IntPtr.Zero;
		// Trace.WriteLine(path + " GetLargeIcon " + iconIndex);
		hr = Shell32Interop.GetLargeIcon(iconIndex, ILD.Transparent, ref hIcon);
		if (hr != 0 || hIcon == IntPtr.Zero) {
#if DEBUG
			Trace.WriteLine(Marshal.GetExceptionForHR(hr)!.Message);
#endif
			return UnknownFileDrawingImage;
		}

		var bs = (ImageSource)HIcon2BitmapSource(hIcon);
		DestroyIcon(hIcon);

		if (useCache) {
			lock (CachedLargeIcons) {
				CachedLargeIcons[extension] = bs;
			}
		}
		return bs;
	}

	public static ImageSource GetDriveThumbnail(string name) {
		if (!Directory.Exists(name)) {
			return UnknownFileDrawingImage;
		}
		lock (CachedDriveIcons) {
			if (CachedDriveIcons.TryGetValue(name, out var cache)) {
				return cache;
			}
		}
		var icon = GetLargeIcon(name, true);
		lock (CachedDriveIcons) {
			CachedDriveIcons[name] = icon;
		}
		return icon;
	}

	/// <summary>
	/// 获取文件的缩略图，如果文件没有缩略图，就获取高清图标
	/// </summary>
	/// <param name="path"></param>
	/// <returns></returns>
	/// <exception cref="FileNotFoundException"></exception>
	/// <exception cref="Exception"></exception>
	public static ImageSource GetPathThumbnail(string path) {
		// Trace.WriteLine($"加载缩略图：{path}");
		var extension = Path.GetExtension(path);
		if (string.IsNullOrEmpty(extension)) {
			return UnknownFileDrawingImage;
		}
		extension = extension.ToLower();
		if (ExtensionsWithThumbnail.Contains(extension)) {
			if (extension == ".svg") {
				return Application.Current.Dispatcher.Invoke(() => Svg2ImageSource(path));
			}
			var retCode = SHCreateItemFromParsingName(path, null, GUID_IShellItem2, out var nativeShellItem);
			if (retCode != 0) {
				// 发生错误，fallback to加载大图标
#if DEBUG
				Trace.WriteLine($"加载缩略图出错：{path} fallback to GetLargeIcon");
#endif
				return GetLargeIcon(path, false);
			}
			var size = new SizeW {
				width = 128,
				height = 128
			};
			var hr = ((IShellItemImageFactory)nativeShellItem).GetImage(size, ThumbnailOptions.ThumbnailOnly, out var hBitmap);
			Marshal.ReleaseComObject(nativeShellItem);
			if (hr != 0 || hBitmap == IntPtr.Zero) {
				// 发生错误，fallback to加载大图标
#if DEBUG
				Trace.WriteLine($"加载缩略图出错：{path} fallback to GetLargeIcon");
#endif
				return GetLargeIcon(path, false);
			}
			var bs = (ImageSource)HBitmap2BitmapSource(hBitmap);
			DeleteObject(hBitmap);
			return bs;
		}
		return GetLargeIcon(path, false);
	}
}