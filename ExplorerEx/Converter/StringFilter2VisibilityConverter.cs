using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExplorerEx.Converter;

interface IFilterable {
	/// <summary>
	/// 使用<see cref="filter"/>进行过滤（英语动名词同型真是好家伙）
	/// </summary>
	/// <param name="filter"></param>
	/// <returns></returns>
	bool Filter(string filter);
}

/// <summary>
/// 提供一个Filter，控制Visibility
/// </summary>
internal class StringFilter2VisibilityConverter : IValueConverter {
	public string Filter { get; set; }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		if (string.IsNullOrEmpty(Filter) || value is not IFilterable f) {
			return Visibility.Visible;
		}
		return f.Filter(Filter) ? Visibility.Visible : Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}