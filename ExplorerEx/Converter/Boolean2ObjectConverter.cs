using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExplorerEx.Converter; 

public class Boolean2ObjectConverter : DependencyObject, IValueConverter {
	public static readonly DependencyProperty TrueProperty = DependencyProperty.Register(
		"True", typeof(object), typeof(Boolean2ObjectConverter), new PropertyMetadata(default(object)));

	public object True {
		get => GetValue(TrueProperty);
		set => SetValue(TrueProperty, value);
	}

	public static readonly DependencyProperty FalseProperty = DependencyProperty.Register(
		"False", typeof(object), typeof(Boolean2ObjectConverter), new PropertyMetadata(default(object)));

	public object False {
		get => GetValue(FalseProperty);
		set => SetValue(FalseProperty, value);
	}

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		return value is true ? True : False;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		return value?.Equals(True);
	}
}