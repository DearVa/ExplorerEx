using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExplorerEx.Converter; 

internal class Int2VisibilityConverter : IValueConverter {
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		if (value is int i) {
			return i != 0 ? Visibility.Visible : Visibility.Collapsed;
		}
		return Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}

internal class Int2VisibilityReConverter : IValueConverter {
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		if (value is int i) {
			return i != 0 ? Visibility.Collapsed : Visibility.Visible;
		}
		return Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}
