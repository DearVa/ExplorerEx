using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using ExplorerEx.View.Controls;
using ExplorerEx.Win32;
using HandyControl.Controls;
using Microsoft.EntityFrameworkCore;
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
	private bool isClipboardViewerSet;

	/// <summary>
	/// 每注册一个EverythingQuery就+1
	/// </summary>
	private static volatile uint globalEverythingQueryId;
	private readonly HashSet<uint> everythingQueryIds = new();

	private readonly string startupPath;
	
	public MainWindow() : this(null) { }

	public MainWindow(string startupPath) {
		this.startupPath = startupPath;
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
			RootGrid.Margin = new Thickness(6);
		}

		hwnd = HwndSource.FromHwnd(new WindowInteropHelper(this).EnsureHandle())!;
		if (MainWindows.Count == 1) {  // 只在一个窗口上检测剪贴板变化事件
			RegisterClipboard();
		}
		hwnd.AddHook(WndProc);

		splitGrid = new SplitGrid(this, null);
		ContentGrid.Children.Add(splitGrid);
		ChangeThemeWithSystem();

		StartupLoad();
	}

	private async void StartupLoad() {
		Trace.WriteLine("StartupLoad: " + DateTime.Now);
		if (startupPath == null) {
			await splitGrid.FileTabControl.StartUpLoad(App.Args.Paths.ToArray());
		} else {
			await splitGrid.FileTabControl.StartUpLoad(startupPath);
		}
		Trace.WriteLine("Startup Finished: " + DateTime.Now);
	}

	private void RegisterClipboard() {
		nextClipboardViewer = SetClipboardViewer(hwnd.Handle);
		var error = Marshal.GetLastWin32Error();
		if (error != 0) {
			Marshal.ThrowExceptionForHR(error);
		}
		isClipboardViewerSet = true;
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
			if (nextClipboardViewer != IntPtr.Zero && nextClipboardViewer != hwnd) {
				SendMessage(nextClipboardViewer, msg, wParam, lParam);
			}
			break;
		case WinMessage.ChangeCbChain:
			if (wParam == nextClipboardViewer) {
				nextClipboardViewer = lParam == hwnd ? IntPtr.Zero : lParam;
			} else if (nextClipboardViewer != IntPtr.Zero && nextClipboardViewer != hwnd) {
				SendMessage(nextClipboardViewer, msg, wParam, lParam);
			}
			break;
		case WinMessage.DwmColorizationCOlorChanged:
			ChangeThemeWithSystem();
			break;
		}
		return IntPtr.Zero;
	}

	/// <summary>
	/// 打开给定的路径，如果窗口隐藏就会显示并打开，如果有窗口就会在FocusedTabControl打开新标签页。使用了Dispatcher，可以跨线程安全调用
	/// </summary>
	/// <param name="path"></param>
	public static void OpenPath(string path) {
		FileTabControl fileTabControl;
		if (MainWindows.Count == 1) {
			var window = MainWindows[0];
			Application.Current.Dispatcher.Invoke(() => {
				if (window.Visibility != Visibility.Visible) {
					window.Visibility = Visibility.Visible;
				}
			});
			fileTabControl = window.splitGrid.FileTabControl;
		} else {
			fileTabControl = FileTabControl.FocusedTabControl;
		}
		Application.Current.Dispatcher.Invoke(() => fileTabControl.OpenPathInNewTabAsync(path));
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

	#region 收藏夹
	public static readonly DependencyProperty IsAddToBookmarkShowProperty = DependencyProperty.Register(
		"IsAddToBookmarkShow", typeof(bool), typeof(MainWindow), new PropertyMetadata(false, IsAddToBookmarkShow_OnChanged));

	public bool IsAddToBookmarkShow {
		get => (bool)GetValue(IsAddToBookmarkShowProperty);
		set => SetValue(IsAddToBookmarkShowProperty, value);
	}

	public static readonly DependencyProperty BookmarkNameProperty = DependencyProperty.Register(
		"BookmarkName", typeof(string), typeof(MainWindow), new PropertyMetadata(default(string)));

	public string BookmarkName {
		get => (string)GetValue(BookmarkNameProperty);
		set => SetValue(BookmarkNameProperty, value);
	}

	private void BookmarkNameTextBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
		((TextBox)sender).SelectAll();
	}

	private FileViewBaseItem bookmarkItem;
	public string BookmarkCategory { get; set; }
	private bool isDeleteBookmark;

	/// <summary>
	/// 添加到书签，如果已添加，则会提示编辑
	/// </summary>
	/// <param name="bookmarkItem"></param>
	public void AddToBookmark(FileViewBaseItem bookmarkItem) {
		this.bookmarkItem = bookmarkItem;
		var fullPath = Path.GetFullPath(bookmarkItem.FullPath);
		var dbBookmark = BookmarkDbContext.Instance.BookmarkDbSet.Local.FirstOrDefault(b => b.FullPath == fullPath);
		if (dbBookmark != null) {
			AddToBookmarkTipTextBlock.Text = "Edit_bookmark".L();
			BookmarkName = dbBookmark.Name;
			BookmarkCategoryComboBox.SelectedItem = dbBookmark.Category;
		} else {
			AddToBookmarkTipTextBlock.Text = "Add_to_bookmarks".L();
			BookmarkName = fullPath.Length == 3 ? fullPath : Path.GetFileName(fullPath);
			BookmarkCategoryComboBox.SelectedIndex = 0;
		}
		IsAddToBookmarkShow = true;
	}

	private void AddToBookmarkConfirm_OnClick(object sender, RoutedEventArgs e) {
		isDeleteBookmark = false;
		IsAddToBookmarkShow = false;
	}

	private void AddToBookmarkDelete_OnClick(object sender, RoutedEventArgs e) {
		isDeleteBookmark = true;
		IsAddToBookmarkShow = false;
	}

	private static async void IsAddToBookmarkShow_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if (!(bool)e.NewValue) {
			var window = (MainWindow)d;
			if (window.bookmarkItem != null) {
				if (!window.isDeleteBookmark) {
					var category = window.BookmarkCategory.Trim();
					if (string.IsNullOrWhiteSpace(category)) {
						category = "Default_bookmark".L();
					}
					var bookmarkDb = BookmarkDbContext.Instance;
					var categoryItem = await bookmarkDb.BookmarkCategoryDbSet.FirstOrDefaultAsync(bc => bc.Name == category);
					if (categoryItem == null) {
						categoryItem = new BookmarkCategory(category);
						await bookmarkDb.BookmarkCategoryDbSet.AddAsync(categoryItem);
					}
					var fullPath = window.bookmarkItem.FullPath;
					var dbBookmark = await BookmarkDbContext.Instance.BookmarkDbSet.FirstOrDefaultAsync(b => b.FullPath == fullPath);
					if (dbBookmark != null) {
						dbBookmark.Name = window.BookmarkName;
						dbBookmark.PropertyUpdateUI(nameof(dbBookmark.Name));
						dbBookmark.Category = categoryItem;
						await bookmarkDb.SaveChangesAsync();
					} else {
						var item = new BookmarkItem(window.bookmarkItem.FullPath, window.BookmarkName, categoryItem);
						await bookmarkDb.BookmarkDbSet.AddAsync(item);
						await bookmarkDb.SaveChangesAsync();
						await item.LoadIconAsync();
					}
					window.bookmarkItem.PropertyUpdateUI(nameof(window.bookmarkItem.IsBookmarked));
				}
				window.bookmarkItem = null;
			}
		}
	}

	public static async void RemoveFromBookmark(FileViewBaseItem bookmarkItem) {
		var bookmarkDb = BookmarkDbContext.Instance;
		var fullPath = bookmarkItem.FullPath;
		var item = await bookmarkDb.BookmarkDbSet.FirstOrDefaultAsync(b => b.FullPath == fullPath);
		if (item != null) {
			bookmarkDb.BookmarkDbSet.Remove(item);
			await bookmarkDb.SaveChangesAsync();
			bookmarkItem.PropertyUpdateUI(nameof(bookmarkItem.IsBookmarked));
		}
	}
	#endregion

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
		if (MainWindows.Count == 1) {  // 如果只剩这一个窗口
			Visibility = Visibility.Collapsed;
			splitGrid.CancelSubSplit();
			splitGrid.FileTabControl.CloseAllTabs();
			e.Cancel = true;
			GC.Collect();
		}
		base.OnClosing(e);
	}

	protected override void OnClosed(EventArgs e) {
		MainWindows.Remove(this);
		splitGrid.Close();
		if (isClipboardViewerSet) {
			if (nextClipboardViewer != IntPtr.Zero) {
				ChangeClipboardChain(hwnd.Handle, nextClipboardViewer);
			}
			if (MainWindows.Count > 0) { // 通知下一个Window进行Hook
				MainWindows[0].RegisterClipboard();
			}
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

	private void DragArea_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
		DragMove();
	}

	protected override void OnStateChanged(EventArgs e) {
		base.OnStateChanged(e);
		var isMaximized = WindowState == WindowState.Maximized;
		ConfigHelper.Save("WindowMaximized", isMaximized);
		if (isMaximized) {
			RootGrid.Margin = new Thickness(6);
		} else {
			RootGrid.Margin = new Thickness(0, 3, 3, 3);
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

	protected override void OnKeyDown(KeyEventArgs e) {
#if DEBUG
		if (e.Key == Key.Pause) {
			Debugger.Break();
		}
#endif
		var mouseOverTab = FileTabControl.MouseOverTabControl.SelectedTab;
		if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
			switch (e.Key) {
			case Key.Z:
				break;
			case Key.X:
				mouseOverTab?.Copy(true);
				break;
			case Key.C:
				mouseOverTab?.Copy(false);
				break;
			case Key.V:
				mouseOverTab?.Paste();
				break;
			}
		} else {
			switch (e.Key) {
			case Key.Delete:
				mouseOverTab?.Delete((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift);
				break;
			}
		}
		base.OnKeyDown(e);
	}
}