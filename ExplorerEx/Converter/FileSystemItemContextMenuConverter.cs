using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using ExplorerEx.Model;

namespace ExplorerEx.Converter; 

/// <summary>
/// 根据所选项的类型决定是显示文件夹右键菜单还是文件右键菜单
/// </summary>
internal class FileSystemItemContextMenuConverter : IValueConverter {
	public ContextMenu FileContextMenu { get; set; }

	public ContextMenu FolderContextMenu { get; set; }

	public ContextMenu DiskDriveContextMenu { get; set; }

	public object Convert(object item, Type targetType, object parameter, CultureInfo culture) {
		return item switch {
			DiskDriveItem => DiskDriveContextMenu,
			FileSystemItem fs => fs.IsFolder ? FolderContextMenu : FileContextMenu,
			BookmarkItem bm => bm.IsFolder ? FolderContextMenu : FileContextMenu, 
			_ => null
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}