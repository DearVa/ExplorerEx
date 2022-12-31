using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ExplorerEx.Utils;

namespace ExplorerEx.Converter; 

internal class FileName2IconConverter : IValueConverter {
	/// <summary>
	/// 返回给定的文件名（如果不含路径会查找Path）的小图标，<see cref="parameter"/>是如果没有找到，就使用fallback的资源
	/// </summary>
	/// <param name="value"></param>
	/// <param name="targetType"></param>
	/// <param name="parameter"></param>
	/// <param name="culture"></param>
	/// <returns></returns>
	public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		if (value is string fileName) {
            var location = FileUtils.FindFileLocation(fileName);
			if (location != null) {
				return IconHelper.GetSmallIcon(location, false);
			}
		}
		if (parameter is string fallback) {
			return Application.Current.Resources[fallback];
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotSupportedException();
	}
}