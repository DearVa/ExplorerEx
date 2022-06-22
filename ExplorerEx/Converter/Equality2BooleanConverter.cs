using System;
using System.Globalization;
using System.Windows.Data;

namespace ExplorerEx.Converter; 

internal class Equality2BooleanConverter : IValueConverter {
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		return value switch {
			int i => i == System.Convert.ToInt32(parameter),
			string s => s.Equals(parameter as string),
			_ => value?.Equals(parameter) ?? parameter == null
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}