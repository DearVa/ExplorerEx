using System;
using System.Globalization;
using System.Windows.Data;

namespace ExplorerEx.Converter; 

/// <summary>
/// 如果value为null，返回Object1，否则返回Object2
/// </summary>
internal class Object2ObjectConverter : IValueConverter {
	public object Object1 { get; set; }
	public object Object2 { get; set; }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		return value == null ? Object1 : Object2;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}