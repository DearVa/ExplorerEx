using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using ExplorerEx.Models;

namespace ExplorerEx.Converter;

/// <summary>
/// 根据<see cref="FileViewType"/>来转换ItemsPanel
/// </summary>
internal class FileDataGridItemsPanelConverter : IValueConverter {
	public ItemsPanelTemplate StackPanelTemplate { get; set; } = null!;
	public ItemsPanelTemplate WrapPanelTemplate { get; set; } = null!;

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		return (FileViewType)value switch {
			FileViewType.Details => StackPanelTemplate,
			FileViewType.Content => StackPanelTemplate,
			_ => WrapPanelTemplate
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}