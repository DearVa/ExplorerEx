using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace ExplorerEx.Converter; 

/// <summary>
/// 将完整路径转成文件名
/// </summary>
internal class FullPath2FileNameConverter : IValueConverter {
	public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		if (value is string path) {
			return Path.GetFileName(path);
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotSupportedException();
	}
}