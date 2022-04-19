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
			>= 'a' and <= 'z' or >= 'A' and <= 'Z' => first.ToString().ToLower(),
			< (char)33 or > (char)126 => GetPinYinChar(name[..1]),  // TODO: 多语言
			_ => "*"
		};
	}

	private static readonly Encoding Gbk = Encoding.GetEncoding("GBK");

	public static string GetPinYinChar(string c) {
		var buf = Gbk.GetBytes(c);
		if (buf.Length < 2) {
			return "*";
		}
		var index = buf[0] * 256 + buf[1];
		return index switch {
			>= 45217 and <= 45252 => "拼音 a",
			>= 45253 and <= 45760 => "拼音 b",
			>= 45761 and <= 46317 => "拼音 c",
			>= 46318 and <= 46825 => "拼音 d",
			>= 46826 and <= 47009 => "拼音 e",
			>= 47010 and <= 47296 => "拼音 f",
			>= 47297 and <= 47613 => "拼音 g",
			>= 47614 and <= 48118 => "拼音 h",
			>= 48119 and <= 49061 => "拼音 j",
			>= 49062 and <= 49323 => "拼音 k",
			>= 49324 and <= 49895 => "拼音 l",
			>= 49896 and <= 50370 => "拼音 m",
			>= 50371 and <= 50613 => "拼音 n",
			>= 50614 and <= 50621 => "拼音 o",
			>= 50622 and <= 50905 => "拼音 p",
			>= 50906 and <= 51386 => "拼音 q",
			>= 51387 and <= 51445 => "拼音 r",
			>= 51446 and <= 52217 => "拼音 s",
			>= 52218 and <= 52697 => "拼音 t",
			>= 52698 and <= 52979 => "拼音 w",
			>= 52980 and <= 53688 => "拼音 x",
			>= 53689 and <= 54480 => "拼音 y",
			>= 54481 and <= 55289 => "拼音 z",
			_ => "*"
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new InvalidOperationException();
	}
}