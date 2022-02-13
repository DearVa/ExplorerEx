using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ExplorerEx.Utils;
using ExplorerEx.View.Controls;
using ExplorerEx.Win32;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.View;

public sealed partial class MainWindow {
	/// <summary>
	/// 当剪切板变化时触发，数据会放在<see cref="DataObjectContent"/>中
	/// </summary>
	public static event Action ClipboardChanged;
	public static DataObjectContent DataObjectContent { get; private set; }
	
	public event Action<uint, EverythingInterop.QueryReply> EverythingQueryReplied;

	private static readonly List<MainWindow> MainWindows = new();

	private readonly SplitGrid splitGrid;
	private readonly HwndSource hwnd;
	private IntPtr nextClipboardViewer;

	private readonly string startupPath;

	/// <summary>
	/// 每注册一个EverythingQuery就+1
	/// </summary>
	private static volatile uint globalEverythingQueryId;

	private readonly HashSet<uint> everythingQueryIds = new();

	public MainWindow() : this(null) { }

	public MainWindow(string path) {
		startupPath = path;
		MainWindows.Add(this);
		Width = ConfigHelper.LoadInt("WindowWidth", 1200);
		Height = ConfigHelper.LoadInt("WindowHeight", 800);
		var left = ConfigHelper.LoadInt("WindowLeft");
		var top = ConfigHelper.LoadInt("WindowTop");
		if (MainWindows.Count > 1) {
			left += Random.Shared.Next(-100, 100);
			top += Random.Shared.Next(-100, 100);
		}
		Left = Math.Min(Math.Max(100 - Width, left), SystemParameters.PrimaryScreenWidth - 100);
		Top = Math.Min(Math.Max(0, top), SystemParameters.PrimaryScreenHeight - 100);

		DataContext = this;
		InitializeComponent();

		if (ConfigHelper.LoadBoolean("WindowMaximized")) {
			WindowState = WindowState.Maximized;
			RootGrid.Margin = new Thickness(8);
		}

		hwnd = HwndSource.FromHwnd(new WindowInteropHelper(this).EnsureHandle())!;
		if (MainWindows.Count == 1) {  // 只在一个窗口上检测剪贴板变化事件
			RegisterClipboard();
		}
		hwnd.AddHook(WndProc);

		splitGrid = new SplitGrid(this, null);
		RootGrid.Children.Add(splitGrid);
		ChangeThemeWithSystem();
	}

	protected override async void OnContentRendered(EventArgs e) {
		base.OnContentRendered(e);
		await splitGrid.FileTabControl.StartUpLoad(startupPath);
	}

	private void RegisterClipboard() {
		nextClipboardViewer = SetClipboardViewer(hwnd.Handle);
		var error = Marshal.GetLastWin32Error();
		if (error != 0) {
			Marshal.ThrowExceptionForHR(error);
		}
		OnClipboardChanged();
	}

	private void EnableAcrylic() {
		var accent = new AccentPolicy {
			AccentState = AccentState.EnableAcrylicBlurBehind,
			// 20: 透明度 第一个0xFFFFFF：背景色
			GradientColor = (40 << 24) | (0xCCCCCC & 0xFFFFFF)
		};

		var sizeOfAccent = Marshal.SizeOf<AccentPolicy>();
		var pAccent = Marshal.AllocHGlobal(sizeOfAccent);
		Marshal.StructureToPtr(accent, pAccent, true);

		var data = new WindowCompositionAttributeData {
			Attribute = WindowCompositionAttribute.AccentPolicy,
			SizeOfData = sizeOfAccent,
			Data = pAccent
		};

		SetWindowCompositionAttribute(hwnd.Handle, ref data);

		Marshal.FreeHGlobal(pAccent);
	}

	private void EnableMica(bool isDarkTheme) {
		if (Environment.OSVersion.Version >= Version.Parse("10.0.22000.0")) {
			var isDark = isDarkTheme ? 1 : 0;
			DwmSetWindowAttribute(hwnd.Handle, DwmWindowAttribute.UseImmersiveDarkMode, ref isDark, sizeof(uint));
			var trueValue = 1;
			DwmSetWindowAttribute(hwnd.Handle, DwmWindowAttribute.MicaEffect, ref trueValue, sizeof(uint));
		}
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
		switch ((WinMessage)msg) {
		case WinMessage.CopyData:
			if (EverythingQueryReplied != null) {
				var cd = Marshal.PtrToStructure<EverythingInterop.CopyDataStruct>(lParam);
				var id = (uint)cd.dwData;
				lock (everythingQueryIds) {
					if (!everythingQueryIds.Contains(id)) {
						break;
					}
				}
				EverythingQueryReplied.Invoke(id, EverythingInterop.ParseEverythingIpcResult(cd.lpData, cd.cbData));
			}
			break;
		case WinMessage.DrawClipboard:
			OnClipboardChanged();
			if (nextClipboardViewer != IntPtr.Zero) {
				SendMessage(nextClipboardViewer, msg, wParam, lParam);
			}
			break;
		case WinMessage.ChangeCbChain:
			if (wParam == nextClipboardViewer) {
				nextClipboardViewer = lParam;
			} else if (nextClipboardViewer != IntPtr.Zero) {
				SendMessage(nextClipboardViewer, msg, wParam, lParam);
			}
			break;
		case WinMessage.DwmColorizationCOlorChanged:
			ChangeThemeWithSystem();
			break;
		case WinMessage.NewInstance: // 启动了另一个实例
			var pid = (int)wParam;
			try {
				var other = Process.GetProcessById(pid);
				var args = other.GetCommandLine().Split(' ');
				var path = args.Length > 1 ? args[1] : null;
				new MainWindow(path).Show();
			} catch (Exception e) {
				Logger.Exception(e, false);
			}
			handled = true;
			break;
		}
		return IntPtr.Zero;
	}

	/// <summary>
	/// 在调用Everything的Query之前，先调用这个方法注册，当查询返回，会触发<see cref="EverythingQueryReplied"/>，如果id相同，就说明查询到了
	/// </summary>
	/// <returns>分配的查询ID</returns>
	public uint RegisterEverythingQuery() {
		lock (everythingQueryIds) {
			var id = Interlocked.Increment(ref globalEverythingQueryId);
			everythingQueryIds.Add(id);
			EverythingInterop.SetReplyWindow(hwnd.Handle);
			EverythingInterop.SetReplyID(id);
			return id;
		}
	}

	public void UnRegisterEverythingQuery(uint id) {
		lock (everythingQueryIds) {
			everythingQueryIds.Remove(id);
		}
	}

	private static void OnClipboardChanged() {
		try {
			DataObjectContent = new DataObjectContent(Clipboard.GetDataObject());
			ClipboardChanged?.Invoke();
		} catch (Exception e) {
			HandyControl.Controls.MessageBox.Error(e.ToString());
		}
	}

	protected override void OnClosing(CancelEventArgs e) {
		if (splitGrid.FileTabControl.TabItems.Count > 1 || splitGrid.AnyOtherTabs) {
			if (!MessageBoxHelper.AskWithDefault("CloseMultiTabs", "You_have_opened_more_than_one_tab".L())) {
				e.Cancel = true;
			}
		}
		base.OnClosing(e);
	}

	protected override void OnClosed(EventArgs e) {
		MainWindows.Remove(this);
		splitGrid.Close();
		if (nextClipboardViewer != IntPtr.Zero) {
			ChangeClipboardChain(hwnd.Handle, nextClipboardViewer);
		}
		if (MainWindows.Count > 0) {  // 通知下一个Window进行Hook
			MainWindows[0].RegisterClipboard();
		}
		base.OnClosed(e);
	}

	private void ChangeThemeWithSystem() {
		ChangeTheme(App.IsDarkTheme);
	}

	private void ChangeTheme(bool isDarkTheme, bool useBlur = true) {
		var dictionaries = Application.Current.Resources.MergedDictionaries[0].MergedDictionaries;
		dictionaries.Clear();
		try {
			dictionaries.Add(new ResourceDictionary {
				Source = new Uri($"pack://application:,,,/HandyControl;component/Themes/Skin{(isDarkTheme ? "Dark" : string.Empty)}{(useBlur ? "Blur" : string.Empty)}.xaml", UriKind.Absolute)
			});
		} catch (Exception e) {
			Logger.Exception(e);
			dictionaries.Add(new ResourceDictionary {
				Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Skin.xaml", UriKind.Absolute)
			});
		}
		dictionaries.Add(new ResourceDictionary {
			Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Theme.xaml", UriKind.Absolute)
		});

		EnableMica(isDarkTheme);
	}

	private void DragArea_OnMouseDown(object sender, MouseButtonEventArgs e) {
		if (e.ChangedButton == MouseButton.Left) {
			DragMove();
		}
	}

	protected override void OnStateChanged(EventArgs e) {
		base.OnStateChanged(e);
		var isMaximized = WindowState == WindowState.Maximized;
		ConfigHelper.Save("WindowMaximized", isMaximized);
		if (isMaximized) {
			RootGrid.Margin = new Thickness(8);
		} else {
			RootGrid.Margin = new Thickness();
		}
	}

	protected override void OnLocationChanged(EventArgs e) {
		base.OnLocationChanged(e);
		ConfigHelper.Save("WindowLeft", (int)Left);
		ConfigHelper.Save("WindowTop", (int)Top);
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
		base.OnRenderSizeChanged(sizeInfo);
		if (sizeInfo.WidthChanged) {
			ConfigHelper.Save("WindowWidth", (int)sizeInfo.NewSize.Width);
		}
		if (sizeInfo.HeightChanged) {
			ConfigHelper.Save("WindowHeight", (int)sizeInfo.NewSize.Height);
		}
	}

#if DEBUG
	protected override void OnKeyDown(KeyEventArgs e) {
		if (e.Key == Key.Pause) {
			Debugger.Break();
		}
	}
#endif
}