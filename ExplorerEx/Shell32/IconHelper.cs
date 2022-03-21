﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExplorerEx.Win32;
using SvgConverter;
using static ExplorerEx.Shell32.Shell32Interop;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.Shell32;

internal static class IconHelper {
	public static DrawingImage FolderDrawingImage { get; private set; }
	public static DrawingImage EmptyFolderDrawingImage { get; private set; }
	public static DrawingImage UnknownFileDrawingImage { get; private set; }
	public static DrawingImage MissingFileDrawingImage { get; private set; }
	public static BitmapImage ComputerBitmapImage { get; } = new(new Uri("pack://application:,,,/ExplorerEx;component/Assets/Picture/Computer.png"));

	/// <summary>
	/// 可以获取缩略图的文件格式
	/// </summary>
	private static readonly HashSet<string> ExtensionsWithThumbnail = new() {
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

	public static void InitializeDefaultIcons(ResourceDictionary resources) {
		FolderDrawingImage = (DrawingImage)resources["FolderDrawingImage"];
		EmptyFolderDrawingImage = (DrawingImage)resources["EmptyFolderDrawingImage"];
		UnknownFileDrawingImage = (DrawingImage)resources["UnknownFileDrawingImage"];
		MissingFileDrawingImage = (DrawingImage)resources["MissingFileDrawingImage"];
	}

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

	/// <summary>
	/// 获取文件的小图标
	/// </summary>
	/// <param name="path">文件路径</param>
	/// <param name="useFileAttr">如果为true，那就去找文件本身并生成缩略图（比如exe这种）；如果为false，那就只根据拓展名生成缩略图</param>
	/// <returns></returns>
	public static ImageSource GetPathIcon(string path, bool useFileAttr) {
		var extension = Path.GetExtension(path);
		if (string.IsNullOrEmpty(extension)) {
			return UnknownFileDrawingImage;
		}
		extension = extension.ToLower();
		var isLnk = extension == ".lnk";
		var useCache = extension != ".exe" && !isLnk;
		if (useCache) {
			lock (CachedIcons) {
				if (CachedIcons.TryGetValue(extension, out var icon)) {
					return icon;
				}
			}
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
			Trace.WriteLine($"无法获取 {path} 的图标，Res: {res}");
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

	public static ImageSource GetDiskDriveThumbnail(DriveInfo drive) {
		if (!drive.IsReady) {
			return UnknownFileDrawingImage;
		}
		var name = drive.Name;
		lock (CachedDriveIcons) {
			if (CachedDriveIcons.TryGetValue(name, out var cache)) {
				return cache;
			}
		}
		var icon = LoadLargeIcon(name, null);
		if (icon == null) {
			return UnknownFileDrawingImage;
		}
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
				return (DrawingImage)Application.Current.Dispatcher.Invoke(() => ConverterLogic.ConvertSvgToObject(path, ResultMode.DrawingImage, null, out _, new ResKeyInfo()));
			}
			var retCode = SHCreateItemFromParsingName(path, IntPtr.Zero, ref GUID_IShellItem2, out var nativeShellItem);
			if (retCode != 0) {
				// 发生错误，fallback to加载大图标
				Trace.WriteLine($"加载缩略图出错：{path}");
				return LoadLargeIcon(path, extension);
			}
			var size = new Win32Interop.Size {
				width = 128,
				height = 128
			};
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
				return image;
			}
		}
		return LoadLargeIcon(path, null);
	}

	private static ImageSource LoadLargeIcon(string path, string extension) {
		var shFileInfo = new ShFileInfo();
		var res = SHGetFileInfo(path, FileAttribute.Normal, ref shFileInfo, Marshal.SizeOf(shFileInfo), SHGFI.SysIconIndex);
		if (res == 0) {
			return UnknownFileDrawingImage;
		}

		var iconIndex = shFileInfo.iIcon;
		var hIcon = IntPtr.Zero;
		// 只能使用STA调用，否则会失败
		var hr = Application.Current.Dispatcher.Invoke(() => JumboImageList.GetIcon(iconIndex, ILD.Transparent, ref hIcon));
		if (hr != 0 || hIcon == IntPtr.Zero) {
			return UnknownFileDrawingImage;
		}

		var bs = (ImageSource)HIcon2BitmapSource(hIcon);
		DestroyIcon(hIcon);

		if (extension != null) {
			lock (CachedLargeIcons) {
				CachedLargeIcons[extension] = bs;
			}
		}
		return bs;
	}
}