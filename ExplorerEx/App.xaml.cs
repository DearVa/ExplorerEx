using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using ExplorerEx.Utils;
using ExplorerEx.Win32;
using Microsoft.Win32;

namespace ExplorerEx;

public partial class App {
	/// <summary>
	/// 通过注册表获取系统是否为深色模式
	/// </summary>
	public static bool IsDarkTheme {
		get {
			try {
				using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
				return key?.GetValue("AppsUseLightTheme") is 0;
			} catch {
				return false;
			}
		}
	}

	private Mutex mutex;

	protected override void OnStartup(StartupEventArgs e) {
		mutex = new Mutex(true, "ExplorerEx", out var createdNew);
		if (!createdNew) {
			var current = Process.GetCurrentProcess();
			foreach (var process in Process.GetProcessesByName(current.ProcessName)) {
				if (process.Id != current.Id) {
					Win32Interop.SendMessage(process.MainWindowHandle, 13288, (IntPtr)current.Id, IntPtr.Zero);
					break;
				}
			}
			Current.Shutdown();
			return;
		}
		base.OnStartup(e);
		Logger.Initialize();
		IconHelper.InitializeDefaultIcons(Resources);
	}

	protected override void OnExit(ExitEventArgs e) {
		mutex.Dispose();
		base.OnExit(e);
	}
}