using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using ExplorerEx.Database.Interface;
using ExplorerEx.Database.SqlSugar;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.View;
using ExplorerEx.Win32;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx;

public partial class App {
	public static Arguments Args { get; private set; } = null!;
	public static int ProcessorCount { get; private set; }

	/// <summary>
	/// 全局实例的注册容器
	/// </summary>
	public static IWindsorContainer Container { get; private set; } = null!;

	public static IBookmarkDbContext BookmarkDbContext { get; private set; } = null!;
	public static IFileViewDbContext FileViewDbContext { get; private set; } = null!;

	private App() { }

	private static bool isRunning;
	private static Mutex? mutex;
	private static NotifyIconWindow? notifyIconWindow;
	private static NotifyMemoryMappedFile? notifyMmf;
	// private static DispatcherTimer dispatcherTimer;

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
			Console.WriteLine("#AppHelp".L());
			Current.Shutdown();
			return;
		}
		if (Args.RequireDebugger && Debugger.Launch()) {
			Debugger.Break();
		}

		mutex = new Mutex(true, "ExplorerExMut", out var createdNew);
		if (!createdNew) {  // 说明已开启ExplorerEx
			notifyMmf = new NotifyMemoryMappedFile("ExplorerExIPC", 1024, false);
			var command = e.Args.Length > 1 ? "Open|" + e.Args[1] : "Open";
			notifyMmf.Write(Encoding.UTF8.GetBytes(command));
			notifyMmf.Dispose();
			Current.Shutdown();
			return;
		}

		Container = new WindsorContainer();
		Container.Register(Component.For<IBookmarkDbContext>().Instance(
			new BookmarkSugarContext()
			// new BookmarkEfContext()
			));
		BookmarkDbContext = Container.Resolve<IBookmarkDbContext>();
		Container.Register(Component.For<IFileViewDbContext>().Instance(
			new FileViewSugarContext()
			// new FileViewEfContext()
			));
		FileViewDbContext = Container.Resolve<IFileViewDbContext>();
		var loadDataBaseTasks = new[] {
			BookmarkDbContext.LoadAsync(),
			FileViewDbContext.LoadAsync()
		};

		isRunning = true;
		notifyMmf = new NotifyMemoryMappedFile("ExplorerExIPC", 1024, true);
		new Thread(IPCWork) {
			IsBackground = true
		}.Start();

		ProcessorCount = Environment.ProcessorCount;
		IconHelper.Initialize();
		Shell32Interop.Initialize();
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		Settings.Current.LoadSettings();
		ChangeTheme(((SolidColorBrush)SystemParameters.WindowGlassBrush).Color, false);

		await Task.WhenAll(loadDataBaseTasks);

		notifyIconWindow = new NotifyIconWindow();
		//dispatcherTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, LowFrequencyWork, Dispatcher);
		//dispatcherTimer.Start();

		if (!Args.RunInBackground) {
			new MainWindow(null).Show();
		}

#if DEBUG
		EventManager.RegisterClassHandler(typeof(UIElement), UIElement.PreviewKeyDownEvent, new KeyEventHandler((_, args) => {
			if (args.Key == Key.Pause) {
				Debugger.Break();
			}
		}));
#endif
	}

	public static void ChangeTheme(Color primaryColor, bool useAnimation = true) {
		var brushes = new ResourceDictionary {
			Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Basic/Brushes.xaml", UriKind.Absolute)
		};
		var primaryHsvColor = primaryColor.ToHSV();
		var lightPrimaryColor = new HSVColor(primaryHsvColor.hue, primaryHsvColor.saturation * 0.8, primaryHsvColor.value).ToRGB();
		var darkPrimaryColor = new HSVColor(primaryHsvColor.hue, primaryHsvColor.saturation * 1.25, primaryHsvColor.value).ToRGB();
		var newColors = new ResourceDictionary {
			Source = new Uri(Settings.Current.IsDarkMode ? "pack://application:,,,/HandyControl;component/Themes/Basic/Colors/ColorsDark.xaml" : "pack://application:,,,/HandyControl;component/Themes/Basic/Colors/Colors.xaml", UriKind.Absolute),
			["LightPrimaryColor"] = lightPrimaryColor,
			["PrimaryColor"] = primaryColor,
			["DarkPrimaryColor"] = darkPrimaryColor,
			["LightSelectColor"] = Color.FromArgb(0x66, lightPrimaryColor.R, lightPrimaryColor.G, lightPrimaryColor.B),
			["SelectColor"] = Color.FromArgb(0x99, primaryColor.R, primaryColor.G, primaryColor.B),
			["DarkSelectColor"] = Color.FromArgb(0xCC, darkPrimaryColor.R, darkPrimaryColor.G, darkPrimaryColor.B),
			["TitleColor"] = primaryColor,
			["SecondaryTitleColor"] = lightPrimaryColor
		};
		if (Settings.Current.WindowBackdrop != WindowBackdrop.SolidColor) {
			newColors["WindowBackgroundColor"] = Color.FromArgb(0, 127, 127, 127);
		}
		var resources = Current.Resources;
		foreach (string brushName in brushes.Keys) {
			if (resources[brushName] is SolidColorBrush sc) {
				var newColor = (Color)newColors[brushName[..^5] + "Color"];
				if (sc.Color == newColor) {
					continue;
				}
				if (sc.IsFrozen) {
					sc = sc.Clone();
					if (useAnimation) {
						sc.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(newColor, new Duration(TimeSpan.FromMilliseconds(300))));
					} else {
						sc.SetValue(SolidColorBrush.ColorProperty, newColor);
					}
					resources[brushName] = sc;
				} else {
					if (useAnimation) {
						sc.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(newColor, new Duration(TimeSpan.FromMilliseconds(300))));
					} else {
						sc.SetValue(SolidColorBrush.ColorProperty, newColor);
					}
				}
			}
		}
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
			notifyMmf!.WaitForModified();
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
			BookmarkDbContext.Save();
			FileViewDbContext.Save();
			notifyIconWindow?.NotifyIcon.Dispose();
			mutex!.Dispose();
		}
		base.OnExit(e);
	}

	/// <summary>
	/// 严重错误，需要立即退出。会记录错误Log，并弹框警告
	/// </summary>
	/// <param name="e"></param>
	public static void Fatal(Exception e) {
		Logger.Exception(e, false);
		MessageBox(IntPtr.Zero, string.Format("#FatalError".L(), e), "Fatal", MessageBoxType.IconStop);
		Environment.Exit(-1);
	}
}