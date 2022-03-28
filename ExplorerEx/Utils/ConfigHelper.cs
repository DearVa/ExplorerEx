using System;
using Microsoft.Win32;

namespace ExplorerEx.Utils; 

/// <summary>
/// 用于处理配置文件
/// </summary>
public static class ConfigHelper {
	private static readonly RegistryKey RegRoot = Registry.CurrentUser.OpenSubKey(@"Software\Dear.Va\ExplorerEx", true) ?? Registry.CurrentUser.CreateSubKey(@"Software\Dear.Va\ExplorerEx", true);
	private static uint saveCount;

	public static void Save(string key, object value) {
		try {
			RegRoot.SetValue(key, value);
			if (++saveCount > 128) {
				RegRoot.Flush();
			}
		} catch (Exception e) {
			Logger.Exception(e);
		}
	}

	public static object Load(string key) {
		try {
			var value = RegRoot.GetValue(key);
			return value;
		} catch (Exception e) {
			Logger.Exception(e);
			return default;
		}
	}

	public static bool LoadBoolean(string key) {
		try {
			return Convert.ToBoolean(Load(key));
		} catch (Exception e) {
			Logger.Exception(e);
			return default;
		}
	}

	public static int LoadInt(string key, int defaultValue = default) {
		try {
			return Convert.ToInt32(Load(key) ?? defaultValue);
		} catch (Exception e) {
			Logger.Exception(e);
			return defaultValue;
		}
	}

	public static bool Delete(string key) {
		try {
			RegRoot.DeleteValue(key);
			RegRoot.Flush();
			return true;
		} catch (Exception) {
			return false;
		}
	}
}