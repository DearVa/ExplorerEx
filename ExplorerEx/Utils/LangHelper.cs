using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using ExplorerEx.Strings;

namespace ExplorerEx.Utils; 

internal static class LangHelper {
	/// <summary>
	/// 本地化字符串
	/// </summary>
	/// <param name="key"></param>
	/// <returns></returns>
	public static string L(this string key) {
		try {
			return Resources.ResourceManager.GetString(key, Settings.CurrentCulture) ?? key;
		} catch {
			return key;
		}
	}
}

internal class LangExtension : MarkupExtension {
	[ConstructorArgument("path")]
	public string Key { get; }
	
	public LangExtension(string key) {
		Key = key;
	}

	public override object ProvideValue(IServiceProvider serviceProvider) {
		return Key.L();
	}
}

public class LangConverter : IValueConverter {
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		if (value is string s) {
			return s.L();
		}
		return value;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new InvalidOperationException();
	}
}