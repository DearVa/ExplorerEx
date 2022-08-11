using System;
using System.Globalization;
using System.Windows.Data;

namespace HandyControl.Tools.Converter; 

public class Long2FileSizeConverter : IValueConverter {
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		return value switch {
			null => Properties.Langs.Lang.UnknownSize,
			long longValue => StaticConvert(longValue),
			_ => Properties.Langs.Lang.Unknown
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotSupportedException();
	}

	public static string StaticConvert(long value) {
		return value switch {
			< 0 => Properties.Langs.Lang.UnknownSize,
			< 1024 => $"{value} B",
			< 1048576 => $"{value / 1024.0:0.00} KB",
			< 1073741824 => $"{value / 1048576.0:0.00} MB",
			< 1099511627776 => $"{value / 1073741824.0:0.00} GB",
			< 1125899906842624 => $"{value / 1099511627776.0:0.00} TB",
			< 1152921504606847000 => $"{value / 1125899906842624.0:0.00} PB",
			_ => Properties.Langs.Lang.TooLarge
		};
	}
}