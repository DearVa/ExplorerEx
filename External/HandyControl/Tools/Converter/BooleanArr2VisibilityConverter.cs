using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace HandyControl.Tools.Converter {
	public class BooleanArr2VisibilityConverter : IMultiValueConverter {
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
			if (values == null) {
				return Visibility.Collapsed;
			}
			if (values.Any(item => item is false)) {
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
			throw new NotSupportedException();
		}
	}
}
