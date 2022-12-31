using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExplorerEx.Converter; 

public class Boolean2ObjectConverter : DependencyObject, IValueConverter {
	public static readonly DependencyProperty TrueValueProperty = DependencyProperty.Register(
		nameof(TrueValue), typeof(object), typeof(Boolean2ObjectConverter), new PropertyMetadata(default(object)));

	public object TrueValue {
		get => GetValue(TrueValueProperty);
		set => SetValue(TrueValueProperty, value);
	}

	public static readonly DependencyProperty FalseValueProperty = DependencyProperty.Register(
		nameof(FalseValue), typeof(object), typeof(Boolean2ObjectConverter), new PropertyMetadata(default(object)));

	public object FalseValue {
		get => GetValue(FalseValueProperty);
		set => SetValue(FalseValueProperty, value);
	}

	public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) {
		return value is true ? TrueValue : FalseValue;
	}

	public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture) {
		return value?.Equals(TrueValue);
	}
}