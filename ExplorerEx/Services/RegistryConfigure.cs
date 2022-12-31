using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using ExplorerEx.Definitions.Interfaces;
using ExplorerEx.Utils;
using Microsoft.Win32;

namespace ExplorerEx.Services;

/// <summary>
/// 用于处理配置，基于注册表
/// </summary>
[SupportedOSPlatform("windows")]
public class RegistryConfigure : IConfigureService {
	private readonly RegistryKey registryRootKey;
	private readonly DelayAction saveToBufferDelayAction;
	private readonly Dictionary<string, object> saveBuffer = new();

	public RegistryConfigure() {
		registryRootKey = Registry.CurrentUser.OpenSubKey(@"Software\Dear.Va\ExplorerEx", true) ??
		                  Registry.CurrentUser.CreateSubKey(@"Software\Dear.Va\ExplorerEx", true);

		saveToBufferDelayAction = new DelayAction(TimeSpan.FromSeconds(1), SaveBuffer);
	} 

	public void Save(string key, object value) {
		registryRootKey.SetValue(key, value);
	}

    public void SaveToBuffer(string key, object value) {
	    lock (saveBuffer) {
			saveBuffer[key] = value;
	    }

		saveToBufferDelayAction.Start();
    }

    private void SaveBuffer() {
	    lock (saveBuffer) {
		    foreach (var (key, value) in saveBuffer) {
			    Save(key, value);
		    }

		    saveBuffer.Clear();
	    }
    }

    public object? Load(string key, object? defaultValue = default) {
		return registryRootKey.GetValue(key) ?? defaultValue;
	}

	public bool LoadBoolean(string key, bool defaultValue = default) {
		return bool.TryParse(registryRootKey.GetValue(key)?.ToString(), out var value) ? value : defaultValue;
	}

	public int LoadInt(string key, int defaultValue = default) {
		return int.TryParse(registryRootKey.GetValue(key)?.ToString(), out var value) ? value : defaultValue;
	}

	public double LoadDouble(string key, int defaultValue = default) {
		return double.TryParse(registryRootKey.GetValue(key)?.ToString(), out var value) ? value : defaultValue;
	}

	public string? LoadString(string key, string? defaultValue = default) {
		return registryRootKey.GetValue(key)?.ToString() ?? defaultValue;
	}

	public bool Delete(string key) {
		try {
			registryRootKey.DeleteValue(key, true);
		} catch {
			return false;
		}
		return true;
	}
}
