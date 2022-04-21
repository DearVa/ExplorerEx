using System;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using ExplorerEx.Model;
using ExplorerEx.Utils;

namespace ExplorerEx.Converter.Grouping;

internal class NameGroupingConverter : IValueConverter {
	public static Lazy<NameGroupingConverter> Instance { get; } = new(new NameGroupingConverter());

	private NameGroupingConverter() { }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		var name = (string)value;
		if (name == null) {
			return "Empty".L();
		}
		var first = name[0];
		return first switch {
			>= 'a' and <= 'h' or >= 'A' and <= 'H' => "A - H",
			>= 'i' and <= 'p' or >= 'I' and <= 'P' => "I - P",
			>= 'q' and <= 'z' or >= 'Q' and <= 'Z' => "Q - Z",
			>= '0' and <= '9' => "0 - 9",
			< (char)33 or > (char)126 => GetPinYinChar(name[..1]),  // TODO: 多语言
			_ => "*"
		};
	}

	private static readonly Encoding Gbk = Encoding.GetEncoding("GBK");

	public static string GetPinYinChar(string c) {
		var buf = Gbk.GetBytes(c);
		if (buf.Length != 2) {
			return "*";
		}
		var index = buf[0] * 256 + buf[1];
		return index switch {
			>= 45217 and <= 48118 => "拼音 A - H",
			>= 48119 and <= 50905 => "拼音 J - P",
			>= 50906 and <= 55289 => "拼音 Q - Z",
			_ => "*"
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new InvalidOperationException();
	}
}