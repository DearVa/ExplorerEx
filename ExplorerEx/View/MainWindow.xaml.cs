using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using ExplorerEx.Converter;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using ExplorerEx.View.Controls;
using ExplorerEx.Win32;
using Microsoft.EntityFrameworkCore;
using HandyControl.Tools;
using static ExplorerEx.Win32.Win32Interop;
using ConfigHelper = ExplorerEx.Utils.ConfigHelper;
using TextBox = HandyControl.Controls.TextBox;

namespace ExplorerEx.View;

public sealed partial class MainWindow {
	/// <summary>
	/// 当剪切板变化时触发，数据会放在<see cref="DataObjectContent"/>中
	/// </summary>
	public static event Action ClipboardChanged;
	public static DataObjectContent DataObjectContent { get; private set; }

	public event Action<uint, EverythingInterop.QueryReply> EverythingQueryReplied;

	public static readonly List<MainWindow> MainWindows = new();

	public readonly SplitGrid splitGrid;
	private readonly HwndSource hwnd;
	private IntPtr nextClipboardViewer;
	private bool isClipboardViewerSet;

	/// <summary>
	/// 每注册一个EverythingQuery就+1
	/// </summary>
	private static volatile uint globalEverythingQueryId;
	private readonly HashSet<uint> everythingQueryIds = new();

	private readonly string startupPath;

	private readonly StringFilter2VisibilityConverter bookmarkFilter;

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

		bookmarkFilter = (StringFilter2VisibilityConverter)FindResource("BookmarkFilter");

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
		case WinMessage.DeviceChange:
			if (lParam == IntPtr.Zero) {
				break;
			}
			var vol = Marshal.PtrToStructure<DevBroadcastVolume>(lParam);
			if (vol.deviceType == 0x2) {  // DBT_DEVTYPVOLUME
				switch (wParam.ToInt32()) {
				case 0x8000:  // DBT_DEVICEARRIVAL
				case 0x8004:  // DBT_DEVICEREMOVECOMPLETE
					var drive = DriveMaskToLetter(vol.unitMask);
					foreach (var fileTabControl in splitGrid) {
						foreach (var tabItem in fileTabControl.TabItems) {
							switch (tabItem.PathType) {
							case PathType.Home:
#pragma warning disable CS4014
								tabItem.Refresh();
#pragma warning restore CS4014
								break;
							case PathType.Normal: {
								if (tabItem.FullPath[0] == drive) {
#pragma warning disable CS4014
									tabItem.LoadDirectoryAsync(null);  // 驱动器移除，返回主页
#pragma warning restore CS4014
								}
								break;
							}
							}
						}
					}
					break;
				}
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
	/// 打开给定的路径，如果没有窗口就打开一个新的，如果有窗口就会在FocusedTabControl打开新标签页，需要在UI线程调用
	/// </summary>
	/// <param name="path"></param>
	public static async void OpenPath(string path) {
		if (MainWindows.Count == 0) {  // 窗口都关闭了
			new MainWindow(path).Show();
		} else {
			var tabControl = FileTabControl.FocusedTabControl;
			tabControl.MainWindow.BringToFront();
			await tabControl.OpenPathInNewTabAsync(path);
		}
	}

	/// <summary>
	/// 显示主窗口
	/// </summary>
	public static void ShowWindow() {
		if (MainWindows.Count == 0) {  // 窗口都关闭了
			new MainWindow().Show();
		} else {
			MainWindows[0].BringToFront();
		}
	}

	/// <summary>
	/// 显示窗口，置顶并Focus，需要在UI线程调用
	/// </summary>
	public void BringToFront() {
		if (Visibility != Visibility.Visible) {
			Visibility = Visibility.Visible;
		}
		if (WindowState == WindowState.Minimized) {
			WindowState = WindowState.Normal;
		}
		var topmost = Topmost;
		Topmost = false;
		Topmost = true;
		Topmost = topmost;
		Focus();
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

	private string[] bookmarkPaths;
	private int currentBookmarkIndex;
	public string BookmarkCategory { get; set; }
	private bool isDeleteBookmark;

	/// <summary>
	/// 添加到书签，如果已添加，则会提示编辑
	/// </summary>
	/// <param name="filePaths"></param>
	public void AddToBookmark(params string[] filePaths) {
		if (filePaths == null || filePaths.Length == 0) {
			return;
		}
		bookmarkPaths = filePaths;
		currentBookmarkIndex = 0;
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
		var window = (MainWindow)d;
		var	bookmarkItem = window.bookmarkPaths[window.currentBookmarkIndex];
		if ((bool)e.NewValue) {
			var fullPath = Path.GetFullPath(bookmarkItem);
			var dbBookmark = BookmarkDbContext.Instance.BookmarkDbSet.Local.FirstOrDefault(b => b.FullPath == fullPath);
			if (dbBookmark != null) {
				window.AddToBookmarkTipTextBlock.Text = "Edit_bookmark".L();
				window.BookmarkName = dbBookmark.Name;
				window.BookmarkCategoryComboBox.SelectedItem = dbBookmark.Category;
			} else {
				window.AddToBookmarkTipTextBlock.Text = "Add_to_bookmarks".L();
				window.BookmarkName = fullPath.Length == 3 ? fullPath : Path.GetFileName(fullPath);
				window.BookmarkCategoryComboBox.SelectedIndex = 0;
			}
		} else {
			if (bookmarkItem != null) {
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
					var fullPath = Path.GetFullPath(bookmarkItem);
					var dbBookmark = await BookmarkDbContext.Instance.BookmarkDbSet.FirstOrDefaultAsync(b => b.FullPath == fullPath);
					if (dbBookmark != null) {
						dbBookmark.Name = window.BookmarkName;
						dbBookmark.PropertyUpdateUI(nameof(dbBookmark.Name));
						dbBookmark.Category = categoryItem;
						await bookmarkDb.SaveChangesAsync();
					} else {
						var item = new BookmarkItem(bookmarkItem, window.BookmarkName, categoryItem);
						await bookmarkDb.BookmarkDbSet.AddAsync(item);
						await bookmarkDb.SaveChangesAsync();
						item.LoadIcon();
					}
					foreach (var updateItem in MainWindows.SelectMany(mw => mw.splitGrid).SelectMany(f => f.TabItems).SelectMany(i => i.Items).Where(i => i.FullPath == fullPath)) {
						updateItem.PropertyUpdateUI(nameof(updateItem.IsBookmarked));
					}
				}
				window.currentBookmarkIndex++;
				if (window.currentBookmarkIndex < window.bookmarkPaths.Length) {
					window.IsAddToBookmarkShow = true;
				}
			}
		}
	}

	public static async void RemoveFromBookmark(params string[] filePaths) {
		if (filePaths == null || filePaths.Length == 0) {
			return;
		}
		var bookmarkDb = BookmarkDbContext.Instance;
		foreach (var filePath in filePaths) {
			var fullPath = Path.GetFullPath(filePath);
			var item = await bookmarkDb.BookmarkDbSet.FirstOrDefaultAsync(b => b.FullPath == fullPath);
			if (item != null) {
				bookmarkDb.BookmarkDbSet.Remove(item);
				await bookmarkDb.SaveChangesAsync();
				foreach (var updateItem in MainWindows.SelectMany(mw => mw.splitGrid).SelectMany(f => f.TabItems).SelectMany(i => i.Items).Where(i => i.FullPath == fullPath)) {
					updateItem.PropertyUpdateUI(nameof(updateItem.IsBookmarked));
				}
			}
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
		if (sizeInfo.WidthChanged && (int)sizeInfo.NewSize.Width != (int)sizeInfo.PreviousSize.Width) {
			ConfigHelper.Save("WindowWidth", (int)sizeInfo.NewSize.Width);
		}
		if (sizeInfo.HeightChanged && (int)sizeInfo.NewSize.Height != (int)sizeInfo.PreviousSize.Height) {
			ConfigHelper.Save("WindowHeight", (int)sizeInfo.NewSize.Height);
		}
	}

	protected override void OnKeyDown(KeyEventArgs e) {
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
				mouseOverTab?.Delete();
				break;
			}
		}
		base.OnKeyDown(e);
	}

	private void Sidebar_OnSizeChanged(object sender, SizeChangedEventArgs e) {
		int width;
		if (e.WidthChanged && (width = (int)e.NewSize.Width) != (int)e.PreviousSize.Width && SidebarTabControl.SelectedIndex != -1) {
			ConfigHelper.Save("SidebarWidth", width);
		}
	}

	private bool loaded;
	private const double SidebarDefaultWidth = 300;

	private void Sidebar_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
		void UpdateSidebarColumnDefinitionWidth() {
			var width = (double)ConfigHelper.LoadInt("SidebarWidth");
			if (width < SidebarMinWidth) {
				width = SidebarDefaultWidth;
			}
			SidebarColumnDefinition.Width = new GridLength(width);
		}

		var sidebar = (TabControl)sender;
		if (loaded) {
			if (sidebar.SelectedIndex == -1) {
				ConfigHelper.Save("IsSidebarOpen", false);
				SidebarColumnDefinition.Width = new GridLength(0, GridUnitType.Auto);
			} else {
				ConfigHelper.Save("IsSidebarOpen", true);
				ConfigHelper.Save("SidebarIndex", sidebar.SelectedIndex);
				UpdateSidebarColumnDefinitionWidth();
			}
		} else {
			if (ConfigHelper.LoadBoolean("IsSidebarOpen")) {
				sidebar.SelectedIndex = ConfigHelper.LoadInt("SidebarIndex");
				UpdateSidebarColumnDefinitionWidth();
			} else {
				sidebar.SelectedIndex = -1;
				SidebarColumnDefinition.Width = new GridLength(0, GridUnitType.Auto);
			}
			loaded = true;
		}
	}

	private void Sidebar_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
		var tabItem = e.OriginalSource.FindParent<TabItem>();
		if (tabItem != null) {
			if (tabItem.IsSelected) {
				SidebarTabControl.SelectedIndex = -1;
			} else {
				SidebarTabControl.SelectedItem = tabItem;
			}
			e.Handled = true;
		}
	}

	private double sidebarStartOffset;
	private const double SidebarMinWidth = 200;

	/// <summary>
	/// 当拖动至MinWidth之后，会吸附，之后如果继续小于一半，就折叠，大于一半，就展开
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	/// <exception cref="NotImplementedException"></exception>
	private void SidebarSplitter_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
		if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) {
			var splitter = (GridSplitter)sender;
			splitter.CaptureMouse();
			sidebarStartOffset = SidebarColumnDefinition.ActualWidth - e.GetPosition(this).X;
		}
		e.Handled = true;
	}

	private void SidebarSplitter_OnPreviewMouseMove(object sender, MouseEventArgs e) {
		if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) {
			var isOpen = SidebarTabControl.SelectedIndex != -1;
			var width = sidebarStartOffset + e.GetPosition(this).X;
			if (width > SidebarMinWidth / 2) {
				if (!isOpen) {
					var index = ConfigHelper.LoadInt("SidebarIndex");
					SidebarTabControl.SelectedIndex = index <= -1 ? 0 : index;
				}
				if (width > SidebarMinWidth) {
					SidebarColumnDefinition.Width = new GridLength(width);
				} else {
					SidebarColumnDefinition.Width = new GridLength(SidebarMinWidth);
				}
			} else if (isOpen) {
				SidebarTabControl.SelectedIndex = -1;
				ConfigHelper.Save("SidebarWidth", SidebarMinWidth);
			}
		}
		e.Handled = true;
	}

	private void SidebarSplitter_OnPreviewMouseUp(object sender, MouseButtonEventArgs e) {
		var splitter = (GridSplitter)sender;
		splitter.ReleaseMouseCapture();
		e.Handled = true;
	}

	private void SearchTextBox_OnTextChanged(object sender, TextChangedEventArgs e) {
		bookmarkFilter.Filter = ((TextBox)sender).Text;
		BookmarkTreeView.Items.Refresh();
	}

	private void BookmarkDragArea_OnDragEnter(object sender, DragEventArgs e) {
		if (e.Data.GetData(DataFormats.FileDrop) == null) {
			e.Effects = DragDropEffects.None;
			return;
		}
		BookmarkDragTipGrid.BeginAnimation(OpacityProperty, new DoubleAnimation(1d, TimeSpan.FromSeconds(0.1d)));
	}

	private void BookmarkDragArea_OnDragOver(object sender, DragEventArgs e) {
		if (e.Data.GetData(DataFormats.FileDrop) == null) {
			e.Effects = DragDropEffects.None;
		}
	}

	private void BookmarkDragArea_OnDragLeave(object sender, DragEventArgs e) {
		BookmarkDragTipGrid.BeginAnimation(OpacityProperty, new DoubleAnimation(0d, TimeSpan.FromSeconds(0.1d)));
	}

	private void BookmarkDragArea_OnDrop(object sender, DragEventArgs e) {
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] fileList) {
			e.Effects = DragDropEffects.None;
			return;
		}
		BookmarkDragTipGrid.BeginAnimation(OpacityProperty, new DoubleAnimation(0d, TimeSpan.FromSeconds(0.1d)));
		AddToBookmark(fileList);
	}

	private void TabItem_OnDragEnter(object sender, DragEventArgs e) {
		var tabItem = sender.FindParent<TabItem>();
		if (tabItem != null) {
			SidebarTabControl.SelectedItem = tabItem;
		}
	}
}