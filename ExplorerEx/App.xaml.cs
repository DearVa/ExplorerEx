using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using ExplorerEx.View;
using ExplorerEx.Win32;
using Microsoft.Win32;

namespace ExplorerEx;

public partial class App {
	public static App Instance { get; private set; }
	public static Arguments Args;

	private App() {
		Instance = this;
	}

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

	private bool running, windowShown;
	private Mutex mutex;
	private NotifyIconWindow notifyIconWindow;

	protected override async void OnStartup(StartupEventArgs e) {
		Logger.Initialize();
		Win32Interop.AttachConsole(-1);
		await Console.Out.FlushAsync();
		try {
			Args = new Arguments(e.Args);
		} catch (ArgumentException ae) {
			Console.WriteLine("Unknown_argument".L() + ae.Message);
			Current.Shutdown();
			return;
		}
		if (Args.ShowHelp) {
			Console.WriteLine("#App_help".L());
			Current.Shutdown();
			return;
		}
		if (Args.RequireDebugger && Debugger.Launch()) {
			Debugger.Break();
		}
		Trace.WriteLine("Startup: " + DateTime.Now);
		mutex = new Mutex(true, "ExplorerEx", out var createdNew);
		if (createdNew) {  // 说明没有开启ExplorerEx
			running = true;
			new Thread(IPCWork) {
				IsBackground = true
			}.Start();
		} else {
			var nmmf = new NotifyMemoryMappedFile("ExplorerExIPC", 1024, false);
			var command = e.Args.Length > 1 ? "Open|" + e.Args[1] : "Open";
			nmmf.Write(Encoding.UTF8.GetBytes(command));
			Current.Shutdown();
			return;
		}
		IconHelper.InitializeDefaultIcons(Resources);
		await BookmarkDbContext.Instance.LoadOrMigrateAsync();
		await FileViewDbContext.Instance.LoadOrMigrateAsync();
		if (!Args.RunInBackground) {
			new MainWindow().Show();
			windowShown = true;
		}
		notifyIconWindow = new NotifyIconWindow();
	}

	/// <summary>
	/// 进程间消息传递线程
	/// </summary>
	private void IPCWork() {
		using var nmmf = new NotifyMemoryMappedFile("ExplorerExIPC", 1024, true);
		while (running) {
			nmmf.WaitForModified();
			var data = nmmf.Read();
			if (data != null) {
				var msg = Encoding.UTF8.GetString(data).Split('|');
				switch (msg[0]) {
				case "Open":
					OpenWindow(msg.Length == 2 ? msg[1] : null);
					break;
				}
			}
		}
	}

	public void OpenWindow(string path) {
		if (windowShown) {
			View.MainWindow.OpenPath(path);
		} else {
			Dispatcher.Invoke(() => new MainWindow(path).Show());
			windowShown = true;
		}
	}

	protected override void OnExit(ExitEventArgs e) {
		if (running) {
			running = false;
			BookmarkDbContext.Instance.SaveChanges();
			notifyIconWindow?.NotifyIconContextContent.Dispose();
			mutex.Dispose();
		}
		base.OnExit(e);
	}
}