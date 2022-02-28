using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace ExplorerEx.Converter; 

/// <summary>
/// 根据所选项的类型决定是显示文件夹右键菜单还是文件右键菜单
/// </summary>
internal class FileSystemItemContextMenuConverter : IValueConverter {
	public ContextMenu FileContextMenu { get; set; }

	public ContextMenu FolderContextMenu { get; set; }

	public object Convert(object isDirectory, Type targetType, object parameter, CultureInfo culture) {
		return (bool)isDirectory! ? FolderContextMenu : FileContextMenu;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}