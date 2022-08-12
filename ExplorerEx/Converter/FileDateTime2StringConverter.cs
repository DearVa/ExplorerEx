using System;
using System.Globalization;
using System.Windows.Data;

namespace ExplorerEx.Converter; 

internal class FileDateTime2StringConverter : IValueConverter {
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		var dt = (DateTime)value;
		if (dt == DateTime.MaxValue) {
			return string.Empty;
		}
		return dt.ToString(CultureInfo.CurrentUICulture);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}