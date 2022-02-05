using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using ExplorerEx.ViewModel;
using ExplorerEx.Win32;
using Microsoft.Win32;
using static ExplorerEx.Win32.Win32Interop;

namespace ExplorerEx.View;

public sealed partial class MainWindow {
	public static MainWindow Instance => MainWindows[0];
	
	private static readonly List<MainWindow> MainWindows = new();

	public static event Action ClipboardChanged;
	public static DataObjectContent DataObjectContent { get; private set; }

	private readonly HwndSource hwnd;
	private static bool isClipboardViewerSet;
	private IntPtr nextClipboardViewer;

	private readonly MainWindowViewModel viewModel;
	private readonly string startupPath;

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

		DataContext = viewModel = new MainWindowViewModel(this);
		InitializeComponent();

		if (ConfigHelper.LoadBoolean("WindowMaximized")) {
			WindowState = WindowState.Maximized;
			RootGrid.Margin = new Thickness(8);
		}

		hwnd = HwndSource.FromHwnd(new WindowInteropHelper(this).EnsureHandle())!;
		if (!isClipboardViewerSet) {
			nextClipboardViewer = SetClipboardViewer(hwnd.Handle);
			var error = Marshal.GetLastWin32Error();
			if (error != 0) {
				Logger.Error($"监控剪贴板失败，错误代码为：{error}");
			}
			isClipboardViewerSet = true;
			OnClipboardChanged();
		}
		hwnd.AddHook(WndProc);
	}

	protected override async void OnContentRendered(EventArgs e) {
		base.OnContentRendered(e);
		await viewModel.StartUpLoad(startupPath);
		ChangeThemeWithSystem();
	}

	private void EnableAcrylic() {
		var accent = new AccentPolicy {
			AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
			// 20: 透明度 第一个0xFFFFFF：背景色
			GradientColor = (40 << 24) | (0xCCCCCC & 0xFFFFFF)
		};

		var sizeOfAccent = Marshal.SizeOf<AccentPolicy>();
		var pAccent = Marshal.AllocHGlobal(sizeOfAccent);
		Marshal.StructureToPtr(accent, pAccent, true);

		var data = new WindowCompositionAttributeData {
			Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
			SizeOfData = sizeOfAccent,
			Data = pAccent
		};

		SetWindowCompositionAttribute(hwnd.Handle, ref data);

		Marshal.FreeHGlobal(pAccent);
	}

	private void EnableMica(bool isDarkTheme) {
		if (Environment.OSVersion.Version >= Version.Parse("10.0.22000.0")) {
			var isDark = isDarkTheme ? 1 : 0;
			DwmSetWindowAttribute(hwnd.Handle, DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE, ref isDark, sizeof(uint));
			var trueValue = 1;
			DwmSetWindowAttribute(hwnd.Handle, DwmWindowAttribute.DWMWA_MICA_EFFECT, ref trueValue, sizeof(uint));
		}
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
		switch (msg) {
		case WM_DRAWCLIPBOARD:
			OnClipboardChanged();
			SendMessage(nextClipboardViewer, msg, wParam, lParam);
			break;
		case WM_CHANGECBCHAIN:
			if (wParam == nextClipboardViewer) {
				nextClipboardViewer = lParam;
			} else {
				SendMessage(nextClipboardViewer, msg, wParam, lParam);
			}
			break;
		case WM_THEMECHANGED:
		case WM_DWMCOMPOSITIONCHANGED:
		case WM_DWMCOLORIZATIONCOLORCHANGED:
			ChangeThemeWithSystem();
			break;
		case 13288:  // 启动了另一个实例
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

	private static void OnClipboardChanged() {
		try {
			DataObjectContent = new DataObjectContent(Clipboard.GetDataObject());
			ClipboardChanged?.Invoke();
		} catch (Exception e) {
			HandyControl.Controls.MessageBox.Error(e.ToString());
		}
	}

	protected override void OnClosed(EventArgs e) {
		MainWindows.Remove(this);
		base.OnClosed(e);
		if (nextClipboardViewer != IntPtr.Zero) {
			ChangeClipboardChain(hwnd.Handle, nextClipboardViewer);
		}
	}

	protected override void OnPreviewMouseUp(MouseButtonEventArgs e) {
		switch (e.ChangedButton) {
		case MouseButton.XButton1:  // 鼠标侧键返回
			viewModel.SelectedTab.GoBackAsync();
			break;
		case MouseButton.XButton2:
			viewModel.SelectedTab.GoForwardAsync();
			break;
		}
		base.OnPreviewMouseUp(e);
	}

	private void ChangeThemeWithSystem() {
		bool isDarkTheme;
		try {
			isDarkTheme = (int)Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")!.GetValue("AppsUseLightTheme")! != 1;
		} catch {
			isDarkTheme = false;
		}
		ChangeTheme(isDarkTheme);
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
				Source = new Uri($"pack://application:,,,/HandyControl;component/Themes/Skin.xaml", UriKind.Absolute)
			});
		}
		dictionaries.Add(new ResourceDictionary {
			Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Theme.xaml", UriKind.Absolute)
		});
		
		EnableMica(isDarkTheme);
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e) {
		if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
			switch (e.Key) {
			case Key.Z:
				break;
			case Key.X:
				viewModel.SelectedTab.Copy(true);
				break;
			case Key.C:
				viewModel.SelectedTab.Copy(false);
				break;
			case Key.V:
				viewModel.SelectedTab.Paste();
				break;
			}
		} else {
			switch (e.Key) {
			case Key.Delete:
				viewModel.SelectedTab.Delete((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift);
				break;
			default:
				base.OnPreviewKeyDown(e);
				return;
			}
		}
		e.Handled = true;
	}

	private async void AddressBar_OnKeyDown(object sender, KeyEventArgs e) {
		switch (e.Key) {
		case Key.Enter:
			await viewModel.SelectedTab.LoadDirectoryAsync(((TextBox)sender).Text);
			break;
		}
	}

	private async void HomeListBox_OnMouseUp(object sender, MouseButtonEventArgs e) {
		if (e.ChangedButton == MouseButton.Left) {
			if (ItemsControl.ContainerFromElement((ListBox)sender, (DependencyObject)e.OriginalSource) is ListBoxItem item) {
				await viewModel.SelectedTab.Item_OnMouseUp((FileViewBaseItem)item.Content);
			} else {
				viewModel.SelectedTab.ClearSelection();
			}
		}
	}

	private async void FileDataGrid_OnItemClicked(object item) {
		await viewModel.SelectedTab.Item_OnMouseUp((FileViewBaseItem)item);
	}

	private void FileDataGrid_OnBackgroundClicked() {
		viewModel.SelectedTab.ClearSelection();
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
}