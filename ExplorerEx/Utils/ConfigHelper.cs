using System;
using System.Configuration;

namespace ExplorerEx.Utils; 

/// <summary>
/// 用于处理配置文件
/// </summary>
public static class ConfigHelper {
	public static void Save(string key, object value) {
		if (value == null) {
			Delete(key);
			return;
		}
		try {
			var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
			var settings = configFile.AppSettings.Settings;
			if (settings[key] == null) {
				settings.Add(key, value.ToString());
			} else {
				settings[key].Value = value.ToString();
			}
			configFile.Save(ConfigurationSaveMode.Modified);
			ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
		} catch (ConfigurationErrorsException e) {
			//Logger.Log(e);
		}
	}

	public static string Load(string key) {
		try {
			var appSettings = ConfigurationManager.AppSettings;
			return appSettings[key];
		} catch (ConfigurationErrorsException e) {
			//Logger.Log(e);
			return null;
		}
	}

	public static bool LoadBoolean(string key) {
		try {
			return Convert.ToBoolean(ConfigurationManager.AppSettings[key]);
		} catch (ConfigurationErrorsException e) {
			//Logger.Log(e);
			return default;
		}
	}

	public static int LoadInt(string key) {
		try {
			return Convert.ToInt32(ConfigurationManager.AppSettings[key]);
		} catch (ConfigurationErrorsException e) {
			//Logger.Log(e);
			return default;
		}
	}

	public static bool Delete(string key) {
		try {
			var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
			var settings = configFile.AppSettings.Settings;
			if (settings[key] == null) {
				return false;
			}
			settings.Remove(key);
			configFile.Save(ConfigurationSaveMode.Modified);
			ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
			return true;
		} catch (ConfigurationErrorsException) {
			return false;
		}
	}
}