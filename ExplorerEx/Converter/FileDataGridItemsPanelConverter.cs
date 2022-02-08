using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using ExplorerEx.View.Controls;

namespace ExplorerEx.Converter;

/// <summary>
/// 根据<see cref="FileDataGrid.ViewTypes"/>来转换ItemsPanel
/// </summary>
internal class FileDataGridItemsPanelConverter : IValueConverter {
	public ItemsPanelTemplate StackPanelTemplate { get; set; }
	public ItemsPanelTemplate WrapPanelTemplate { get; set; }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		return (FileDataGrid.ViewTypes)value! switch {
			FileDataGrid.ViewTypes.Detail => StackPanelTemplate,
			FileDataGrid.ViewTypes.Content => StackPanelTemplate,
			_ => WrapPanelTemplate
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}