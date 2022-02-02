using System;
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
			return Resources.ResourceManager.GetString(key);
		} catch {
			return "锟斤拷" + key + "烫烫烫";
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