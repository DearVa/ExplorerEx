using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExplorerEx.Definitions.Interfaces;
using ExplorerEx.Services;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using static ExplorerEx.Shell32.Shell32Interop;

namespace ExplorerEx.Models;

/// <summary>
/// 文件关联项，即“打开方式”
/// </summary>
public class FileAssocItem : IExecutable {
	public ImageSource? Icon { get; }

	public string Description { get; }

	private readonly IAssocHandler assocHandler;

	public FileAssocItem(IAssocHandler assocHandler) {
		this.assocHandler = assocHandler;
		assocHandler.GetUIName(out var desc);
		Description = desc;
		// @{Microsoft.Windows.Photos_2021.21120.8011.0_x64__8wekyb3d8bbwe?ms-resource://Microsoft.Windows.Photos/Files/Assets/PhotosAppList.png}
		assocHandler.GetIconLocation(out var iconPath, out _);
		if (iconPath == string.Empty) {
			return;
		}
		if (iconPath[0] != '@') {
			Icon = IconHelper.GetSmallIcon(iconPath, false);
		} else {  // 说明是UWP应用，要根据包名找到应用位置，之后获取最合适大小的icon
			var index = iconPath.IndexOf('?');
			var appxName = iconPath[2..index];
			var appxPath = AppxPackage.GetFullPathByPackageName(appxName);
			if (appxPath == null) {
				return;
			}
			index = iconPath.IndexOf("/Files", index, StringComparison.Ordinal);
			var relativeImagePath = iconPath[(index + 7)..^1];
			var imagePath = AppxPackage.FindMinimumQualifiedImagePath(appxPath, relativeImagePath, 30);
			if (imagePath == null) {
				return;
			}
			var bitmap = new BitmapImage();
			bitmap.BeginInit();
			using var fs = File.OpenRead(imagePath);
			bitmap.StreamSource = fs;
			bitmap.CacheOption = BitmapCacheOption.OnLoad;
			bitmap.EndInit();
			bitmap.Freeze();
			Icon = bitmap;
		}
	}

	public void Run(string args) {
		Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(args, null, GUID_IShellItem, out var item));
		item.BindToHandler(null, BHID_DataObject, typeof(IDataObject).GUID, out var pDataObject);
		if (pDataObject != IntPtr.Zero) {
			var dataObj = (IDataObject)Marshal.GetTypedObjectForIUnknown(pDataObject, typeof(IDataObject));
			assocHandler.Invoke(dataObj);
			Marshal.ReleaseComObject(dataObj);
		}
		Marshal.ReleaseComObject(item);
	}

	/// <summary>
	/// 所有的打开者，可以通过文件拓展名来查找
	/// </summary>
	private static readonly Dictionary<string, List<FileAssocItem>> FileAssocLists = new();

	private const int SingleFileAssocMaxItemCount = 5;

	public static List<FileAssocItem> GetAssocList(string extension) {
		Monitor.Enter(FileAssocLists);
		if (!FileAssocLists.TryGetValue(extension, out var list)) {
			Monitor.Exit(FileAssocLists);

			list = new List<FileAssocItem>();
			Marshal.ThrowExceptionForHR(SHAssocEnumHandlers(extension, AssocFilter.Recommended, out var handlers));
			var count = 0;
			while (count++ < SingleFileAssocMaxItemCount && handlers.Next(1, out var assocHandler, out var fetched) >= 0 && fetched > 0) {
				try {
					list.Add(new FileAssocItem(assocHandler));
				} catch (Exception e) {
					Service.Resolve<ILoggerService>().Exception(e, false);
				}
			}
			Marshal.ReleaseComObject(handlers);

			Monitor.Enter(FileAssocLists);
			FileAssocLists.Add(extension, list);
			Monitor.Exit(FileAssocLists);
		} else {
			Monitor.Exit(FileAssocLists);
		}
		return list;
	}
}