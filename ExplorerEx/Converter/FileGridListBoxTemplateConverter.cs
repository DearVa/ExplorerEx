using ExplorerEx.Model;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ExplorerEx.View.Controls;

namespace ExplorerEx.Converter; 

internal class FileGridListBoxTemplateConverter : IValueConverter {
	public FileGrid FileGrid { get; set; }
	/// <summary>
	/// 图标模式
	/// </summary>
	public DataTemplate IconTemplate { get; set; }
	/// <summary>
	/// 列表模式
	/// </summary>
	public DataTemplate ListTemplate { get; set; }
	/// <summary>
	/// 平铺模式
	/// </summary>
	public DataTemplate TileTemplate { get; set; }
	/// <summary>
	/// 内容模式
	/// </summary>
	public DataTemplate ContentTemplate { get; set; }
	/// <summary>
	/// Home的平铺模板
	/// </summary>
	public DataTemplate TileHomeTemplate { get; set; }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		return FileGrid.FileViewType switch {
			FileViewType.Icon => IconTemplate,
			FileViewType.List => ListTemplate,
			FileViewType.Tile when FileGrid.PathType == PathType.Home => TileHomeTemplate,
			FileViewType.Tile => TileTemplate,
			FileViewType.Content => ContentTemplate,
			_ => null
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}