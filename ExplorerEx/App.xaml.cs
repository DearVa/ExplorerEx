using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using ExplorerEx.Utils;
using ExplorerEx.Win32;

namespace ExplorerEx; 

public partial class App {
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