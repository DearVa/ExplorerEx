using System;
using System.Windows;
using static ExplorerEx.Win32.Win32Interop;
using ExplorerEx.Model;
using ExplorerEx.Win32;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Globalization;
using System.Threading;

namespace ExplorerEx.View; 

/// <summary>
/// 显示一个托盘图标
/// </summary>
public partial class NotifyIconWindow {
	public NotifyIconWindow() {
		Thread.CurrentThread.CurrentUICulture = new CultureInfo(Settings.Current[Settings.CommonSettings.Language].GetInt32());

		DataContext = this;
		
		InitializeComponent();
		NotifyIcon.Init();
		NotifyIcon.MouseDoubleClick += ShowWindow;
		NotifyIcon.WndProc += NotifyIcon_OnWndProc;

		// 注册事件监视剪贴板变化
		nextClipboardViewer = SetClipboardViewer(NotifyIcon.Hwnd);
		Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
		DataObjectContent.HandleClipboardChanged();
	}


	private IntPtr nextClipboardViewer;

	private void NotifyIcon_OnWndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam) {
		switch ((WinMessage)msg) {
		case WinMessage.DeviceChange:
			if (lParam == IntPtr.Zero) {
				break;
			}
			var vol = Marshal.PtrToStructure<DevBroadcastVolume>(lParam);
			if (vol.deviceType == 0x2) {  // DBT_DEVTYPVOLUME
				var param = wParam.ToInt32();
				switch (param) {
				case 0x8000:  // DBT_DEVICEARRIVAL
				case 0x8004:  // DBT_DEVICEREMOVECOMPLETE
					var driveLetter = DriveMaskToLetter(vol.unitMask);
					foreach (var mainWindow in MainWindow.AllWindows) {
						foreach (var fileTabControl in mainWindow.SplitGrid) {
							foreach (var tabItem in fileTabControl.TabItems) {
								switch (tabItem.PathType) {
								case PathType.Home:
									tabItem.Refresh();
									break;
								case PathType.LocalFolder:
								case PathType.Zip: {
									if (tabItem.FullPath[0] == driveLetter) {
										_ = tabItem.LoadDirectoryAsync(null);  // 驱动器移除，返回主页
									}
									break;
								}
								}
							}
						}
					}
					FolderOnlyItem.Home.UpdateDriveChildren();
					RecycleBinItem.RegisterWater(driveLetter);
					break;
				}
			}
			break;
		case WinMessage.DrawClipboard:
			if (nextClipboardViewer != IntPtr.Zero && nextClipboardViewer != hWnd) {
				SendMessage(nextClipboardViewer, msg, wParam, lParam);
			}
			DataObjectContent.HandleClipboardChanged();
			break;
		case WinMessage.ChangeCbChain:
			if (wParam == nextClipboardViewer) {
				nextClipboardViewer = lParam == hWnd ? IntPtr.Zero : lParam;
			} else if (nextClipboardViewer != IntPtr.Zero && nextClipboardViewer != hWnd) {
				SendMessage(nextClipboardViewer, msg, wParam, lParam);
			}
			break;
		case WinMessage.DwmColorizationColorChanged:
			App.ChangeTheme(((SolidColorBrush)SystemParameters.WindowGlassBrush).Color);
			foreach (var mainWindow in MainWindow.AllWindows) {
				mainWindow.ChangeTheme();
			}
			break;
		}
	}

	protected override void OnClosed(EventArgs e) {
		if (nextClipboardViewer != IntPtr.Zero) {
			ChangeClipboardChain(NotifyIcon.Hwnd, nextClipboardViewer);
		}
		base.OnClosed(e);
	}

	private void ShowWindow(object sender, RoutedEventArgs e) {
		MainWindow.ShowWindow();
		NotifyIcon.CloseContextControl();
	}

	private void ExitButton_OnClick(object sender, RoutedEventArgs e) {
		NotifyIcon.Dispose();
		Application.Current.Shutdown();
	}
}