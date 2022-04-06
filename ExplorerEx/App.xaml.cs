using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ExplorerEx.Model;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.View;
using ExplorerEx.Win32;
using Microsoft.Win32;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx;

public partial class App {
	public static App Instance { get; private set; }
	public static Arguments Args { get; private set; }
	public static int ProcessorCount { get; private set; }

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

	private static bool isRunning;
	private static Mutex mutex;
	private static NotifyIconWindow notifyIconWindow;
	private static NotifyMemoryMappedFile notifyMmf;
	private static DispatcherTimer dispatcherTimer;

	protected override async void OnStartup(StartupEventArgs e) {
		Logger.Initialize();
		AttachConsole(-1);
		await Console.Out.FlushAsync();
		try {
			Args = new Arguments(e.Args);
		} catch (ArgumentException ae) {
			Console.WriteLine("UnknownArgument".L() + ae.Message);
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
		mutex = new Mutex(true, "ExplorerEx", out var createdNew);
		if (createdNew) {  // 说明没有开启ExplorerEx
			isRunning = true;
			notifyMmf = new NotifyMemoryMappedFile("ExplorerExIPC", 1024, true);
			new Thread(IPCWork) {
				IsBackground = true
			}.Start();
		} else {
			notifyMmf = new NotifyMemoryMappedFile("ExplorerExIPC", 1024, false);
			var command = e.Args.Length > 1 ? "Open|" + e.Args[1] : "Open";
			notifyMmf.Write(Encoding.UTF8.GetBytes(command));
			Current.Shutdown();
			return;
		}
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		Shell32Interop.Initialize();
		IconHelper.InitializeDefaultIcons(Resources);
		await BookmarkDbContext.Instance.LoadDataBase();
		await FileViewDbContext.Instance.LoadDataBase();
		if (!Args.RunInBackground) {
			new MainWindow(null).Show();
		}
		notifyIconWindow = new NotifyIconWindow();
		//dispatcherTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, LowFrequencyWork, Dispatcher);
		//dispatcherTimer.Start();

#if DEBUG
		EventManager.RegisterClassHandler(typeof(UIElement), UIElement.PreviewKeyDownEvent, new KeyEventHandler((_, args) => {
			if (args.Key == Key.Pause) {
				Debugger.Break();
			}
		}));
#endif
	}

	/// <summary>
	/// 使用DispatcherTimer调用的低频工作，10s一次
	/// </summary>
	private void LowFrequencyWork(object s, EventArgs e) {
		if (DateTime.Now.TimeOfDay > new TimeSpan(20, 10, 0)) {
			var brushes = new ResourceDictionary {
				Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Basic/Brushes.xaml", UriKind.Absolute)
			};
			var newColors = new ResourceDictionary {
				Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Basic/Colors/ColorsDark.xaml", UriKind.Absolute)
			};
			foreach (string brushName in brushes.Keys) {
				if (Resources[brushName] is SolidColorBrush brush) {
					var newColorName = brushName[..^5] + "Color";
					brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation((Color)newColors[newColorName], new Duration(TimeSpan.FromMilliseconds(500))));
				}
			}
		}
	}

	/// <summary>
	/// 进程间消息传递线程
	/// </summary>
	private void IPCWork() {
		while (isRunning) {
			notifyMmf.WaitForModified();
			var data = notifyMmf.Read();
			if (data != null) {
				var msg = Encoding.UTF8.GetString(data).Split('|');
				switch (msg[0]) {
				case "Open":
					Dispatcher.Invoke(() => View.MainWindow.OpenPath(msg.Length == 2 ? msg[1] : null));
					break;
				}
			}
		}
	}

	protected override void OnExit(ExitEventArgs e) {
		if (isRunning) {
			isRunning = false;
			BookmarkDbContext.Instance.SaveChanges();
			notifyIconWindow?.NotifyIconContextContent.Dispose();
			mutex.Dispose();
		}
		base.OnExit(e);
	}

	/// <summary>
	/// 严重错误，需要立即退出。会记录错误Log，并弹框警告
	/// </summary>
	/// <param name="e"></param>
	public static void Fatal(Exception e) {
		Logger.Exception(e, false);
		MessageBox.Show(string.Format("#FatalError".L(), e), "Fatal", MessageBoxButton.OK, MessageBoxImage.Stop);
		Environment.Exit(-1);
	}
}