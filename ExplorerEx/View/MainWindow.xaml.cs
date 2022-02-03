using System;
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
	public static event Action ClipboardChanged;
	public static ClipboardContent ClipboardContent { get; private set; }

	private readonly IntPtr hwnd;
	private IntPtr nextClipboardViewer;

	private readonly MainWindowViewModel viewModel;

	public MainWindow() : this(null) { }

	public MainWindow(string path) {
		Width = ConfigHelper.LoadInt("WindowWidth", 1200);
		Height = ConfigHelper.LoadInt("WindowHeight", 800);
		Left = ConfigHelper.LoadInt("WindowLeft");
		Top = ConfigHelper.LoadInt("WindowTop");
		if (ConfigHelper.LoadBoolean("WindowMaximized")) {
			WindowState = WindowState.Maximized;
		}

		DataContext = viewModel = new MainWindowViewModel(this, path);
		InitializeComponent();

		hwnd = new WindowInteropHelper(this).EnsureHandle();
		if (nextClipboardViewer == IntPtr.Zero) {  // 捕捉剪切板事件，只需要绑定一个即可
			nextClipboardViewer = (IntPtr)SetClipboardViewer((int)hwnd);
			if (nextClipboardViewer != IntPtr.Zero) {
				HwndSource.FromHwnd(hwnd)!.AddHook(WndProc);
				OnClipboardChanged();
			}
		}
	}

	protected override void OnContentRendered(EventArgs e) {
		base.OnContentRendered(e);
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

		SetWindowCompositionAttribute(hwnd, ref data);

		Marshal.FreeHGlobal(pAccent);
	}

	private void EnableMica(bool isDarkTheme) {
		if (Environment.OSVersion.Version >= Version.Parse("10.0.22000.0")) {
			var isDark = isDarkTheme ? 1 : 0;
			DwmSetWindowAttribute(hwnd, DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE, ref isDark, sizeof(uint));
			var trueValue = 1;
			DwmSetWindowAttribute(hwnd, DwmWindowAttribute.DWMWA_MICA_EFFECT, ref trueValue, sizeof(uint));
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
			ChangeThemeWithSystem();
			break;
		}

		return IntPtr.Zero;
	}

	private static void OnClipboardChanged() {
		try {
			ClipboardContent = new ClipboardContent(Clipboard.GetDataObject());
			ClipboardChanged?.Invoke();
		} catch (Exception e) {
			HandyControl.Controls.MessageBox.Error(e.ToString());
		}
	}

	protected override void OnClosed(EventArgs e) {
		base.OnClosed(e);
		if (nextClipboardViewer != IntPtr.Zero) {
			ChangeClipboardChain(hwnd, nextClipboardViewer);
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
		if (e.ChangedButton == MouseButton.Left && ItemsControl.ContainerFromElement((ListBox)sender, (DependencyObject)e.OriginalSource) is ListBoxItem item) {
			await viewModel.SelectedTab.Item_OnMouseUp((FileViewBaseItem)item.Content);
		} else {
			viewModel.SelectedTab.ClearSelection();
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
		ConfigHelper.Save("WindowMaximized", WindowState == WindowState.Maximized);
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