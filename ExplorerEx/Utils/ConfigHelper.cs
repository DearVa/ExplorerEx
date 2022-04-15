using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ExplorerEx.Utils; 

/// <summary>
/// 用于处理配置文件
/// </summary>
public static class ConfigHelper {
	private static readonly RegistryKey RegRoot = Registry.CurrentUser.OpenSubKey(@"Software\Dear.Va\ExplorerEx", true) ?? Registry.CurrentUser.CreateSubKey(@"Software\Dear.Va\ExplorerEx", true);

	private static Task bufferSaveTask;
	private static readonly Dictionary<string, object> Buffer = new();
	private static bool canSave;

	/// <summary>
	/// 暂存入缓冲区，如果1s没有新的写入就批量存储
	/// </summary>
	/// <param name="key"></param>
	/// <param name="value"></param>
	public static void SaveToBuffer(string key, object value) {
		lock (Buffer) {
			Buffer[key] = value;
			canSave = false;
			if (bufferSaveTask == null || bufferSaveTask.IsCompleted) {
				bufferSaveTask = Task.Run(BufferSaveTaskWork);
			}
		}
	}

	private static void BufferSaveTaskWork() {
		while (true) {
			canSave = true;
			Thread.Sleep(1000);
			if (canSave) {
				lock (Buffer) {
					foreach (var (key, value) in Buffer) {
						Save(key, value);
					}
					Buffer.Clear();
				}
				break;
			}
		}
	}

	public static void Save(string key, object value) {
		try {
			RegRoot.SetValue(key, value);
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