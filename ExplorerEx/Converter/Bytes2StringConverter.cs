using System;
using System.Globalization;
using System.Windows.Data;
using ExplorerEx.Utils;

namespace ExplorerEx.Converter; 

/// <summary>
/// 将字节数转换成易读的形式
/// </summary>
internal class Bytes2StringConverter : IValueConverter {
	public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture) {
		return value switch {
			long l => FileUtils.FormatByteSize(l),
			int i => FileUtils.FormatByteSize(i),
			_ => null
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}