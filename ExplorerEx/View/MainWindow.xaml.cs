using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ExplorerEx.Command;
using ExplorerEx.Converter;
using ExplorerEx.Model;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.View.Controls;
using ExplorerEx.Win32;
using Microsoft.EntityFrameworkCore;
using HandyControl.Tools;
using static ExplorerEx.Win32.Win32Interop;
using static ExplorerEx.Shell32.Shell32Interop;
using ConfigHelper = ExplorerEx.Utils.ConfigHelper;
using TextBox = HandyControl.Controls.TextBox;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Diagnostics;
using hc = HandyControl.Controls;

namespace ExplorerEx.View;

public sealed partial class MainWindow {
	public event Action<uint, EverythingInterop.QueryReply> EverythingQueryReplied;

	public static List<MainWindow> MainWindows { get; } = new();

	public SimpleCommand SideBarItemPreviewMouseUpCommand { get; }
	public SimpleCommand SideBarItemClickCommand { get; }
	public FileItemCommand BookmarkItemCommand { get; }
	public FileItemCommand SideBarPcItemCommand { get; }
	public SimpleCommand EditBookmarkCommand { get; }
	public SplitGrid SplitGrid { get; }
	public HwndSource Hwnd { get; }

	private IntPtr nextClipboardViewer;
	private bool isClipboardViewerSet;

	/// <summary>
	/// 每注册一个EverythingQuery就+1
	/// </summary>
	private static volatile uint globalEverythingQueryId;
	private readonly HashSet<uint> everythingQueryIds = new();

	private readonly string startupPath;

	private readonly FileSystemItemContextMenuConverter bookmarkItemContextMenuConverter;
	private readonly ContextMenu sideBarPcItemContextMenu;

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

		bookmarkItemContextMenuConverter = (FileSystemItemContextMenuConverter)Resources["BookmarkItemContextMenuConverter"];
		sideBarPcItemContextMenu = (ContextMenu)Resources["SideBarPcItemContextMenu"];
		sideBarPcItemContextMenu.DataContext = this;

		SideBarItemPreviewMouseUpCommand = new SimpleCommand(SideBarItem_OnPreviewMouseUp);
		SideBarItemClickCommand = new SimpleCommand(SideBarItem_OnClick);

		BookmarkItemCommand = new FileItemCommand {
			SelectedItemsProvider = () => SideBarBookmarksTreeView.SelectedItem is BookmarkItem selectedItem ? new[] { selectedItem } : Array.Empty<FileItem>(),
			TabControlProvider = () => FileTabControl.MouseOverTabControl
		};
		SideBarPcItemCommand = new FileItemCommand {
			SelectedItemsProvider = () => SideBarThisPcTreeView.SelectedItem is SideBarPcItem selectedItem ? new[] { selectedItem } : Array.Empty<FileItem>(),
			TabControlProvider = () => FileTabControl.MouseOverTabControl
		};
		EditBookmarkCommand = new SimpleCommand(_ => {
			if (SideBarBookmarksTreeView.SelectedItem is BookmarkItem selectedItem) {
				AddToBookmarks(selectedItem.FullPath);
			}
		});

		if (ConfigHelper.LoadBoolean("WindowMaximized")) {
			WindowState = WindowState.Maximized;
			BorderThickness = new Thickness(8);
		}

		Hwnd = HwndSource.FromHwnd(new WindowInteropHelper(this).EnsureHandle())!;
		if (MainWindows.Count == 1) {  // 只在一个窗口上检测剪贴板变化事件
			RegisterClipboard();
			RecycleBinItem.Update();
		}
		Hwnd.AddHook(WndProc);

		SplitGrid = new SplitGrid(this, null);
		ContentGrid.Children.Add(SplitGrid);
		ChangeTheme(App.IsDarkTheme, false);

		if (SideBarPcItem.RootItems.Count == 0) {
			foreach (var driveInfo in DriveInfo.GetDrives()) {
				SideBarPcItem.RootItems.Add(new SideBarPcItem(driveInfo));
			}
		}

		StartupLoad();
	}

	private void SideBarItem_OnPreviewMouseUp(object args) {
		var e = (MouseButtonEventArgs)args;
		if (e.ChangedButton == MouseButton.Right && e.OriginalSource is FrameworkElement { DataContext: FileItem fileItem }) {
			fileItem.IsSelected = true;
			switch (fileItem) {
			case BookmarkItem bookmarkItem:
				var menu = (ContextMenu)bookmarkItemContextMenuConverter.Convert(bookmarkItem, null, null, null)!;
				menu.DataContext = this;
				menu.SetValue(FileItemAttach.FileItemProperty, bookmarkItem);
				menu.IsOpen = true;
				e.Handled = true;
				break;
			case SideBarPcItem pcItem:
				sideBarPcItemContextMenu.SetValue(FileItemAttach.FileItemProperty, pcItem);
				sideBarPcItemContextMenu.IsOpen = true;
				e.Handled = true;
				break;
			}
		}
	}

	private void SideBarItem_OnClick(object args) {
		var e = (RoutedEventArgs)args;
		if (e.OriginalSource is FrameworkElement element) {
			switch (element.DataContext) {
			case FileItem fileItem:
				fileItem.IsSelected = true;
				if (fileItem.IsFolder) {
					_ = FileTabControl.FocusedTabControl.SelectedTab.LoadDirectoryAsync(fileItem.FullPath);
				} else {
					try {
						var psi = new ProcessStartInfo {
							FileName = fileItem.FullPath,
							UseShellExecute = true
						};
						if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) && fileItem is FileSystemItem { IsExecutable: true }) {
							psi.Verb = "runas";
						}
						Process.Start(psi);
					} catch (Exception ex) {
						if (ex is Win32Exception { ErrorCode: -2147467259 }) {  // 操作被用户取消
							return;
						}
						hc.MessageBox.Error(ex.Message, "FailedToOpenFile".L());
					}
				}
				break;
			case BookmarkCategory bc:
				bc.IsExpanded = !bc.IsExpanded;
				break;
			}
		}
	}

	private void StartupLoad() {
		if (startupPath == null) {
			_ = SplitGrid.FileTabControl.StartUpLoad(App.Args.Paths.ToArray());
		} else {
			_ = SplitGrid.FileTabControl.StartUpLoad(startupPath);
		}
	}

	/// <summary>
	/// 注册事件监视剪贴板变化
	/// </summary>
	private void RegisterClipboard() {
		nextClipboardViewer = SetClipboardViewer(Hwnd.Handle);
		var error = Marshal.GetLastWin32Error();
		if (error != 0) {
			Marshal.ThrowExceptionForHR(error);
		}
		isClipboardViewerSet = true;
		DataObjectContent.HandleClipboardChanged();
	}

	/// <summary>
	/// 注册事件监视回收站变化
	/// </summary>
	private void RegisterRecycleBin() {
		var entry = new SHChangeNotifyEntry();
		SHChangeNotifyRegister(Hwnd.Handle, SHCNF.AcceptInterrupts | SHCNF.AcceptNonInterrupts, SHCNE.Rmdir | SHCNE.RenameFolder | SHCNE.Delete | SHCNE.RenameItem, (uint)WinMessage.ShellNotifyRBinDir, 1, ref entry);
		RecycleBinItem.Update();
	}

	private void EnableMica(bool isDarkTheme) {
		if (Environment.OSVersion.Version >= Version.Parse("10.0.22000.0")) {
			var isDark = isDarkTheme ? 1 : 0;
			DwmSetWindowAttribute(Hwnd.Handle, DwmWindowAttribute.UseImmersiveDarkMode, ref isDark, sizeof(uint));
			var trueValue = 1;
			DwmSetWindowAttribute(Hwnd.Handle, DwmWindowAttribute.MicaEffect, ref trueValue, sizeof(uint));
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
				var param = wParam.ToInt32();
				switch (param) {
				case 0x8000:  // DBT_DEVICEARRIVAL
				case 0x8004:  // DBT_DEVICEREMOVECOMPLETE
					var drive = DriveMaskToLetter(vol.unitMask);
					foreach (var fileTabControl in SplitGrid) {
						foreach (var tabItem in fileTabControl.TabItems) {
							switch (tabItem.PathType) {
							case PathType.Home:
								tabItem.Refresh();
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
					for (var i = 0; i < SideBarPcItem.RootItems.Count; i++) {
						if (SideBarPcItem.RootItems[i].FullPath[0] == drive) {
							SideBarPcItem.RootItems.RemoveAt(i);
							break;
						}
					}
					if (param == 0x8000) {
						SideBarPcItem.RootItems.Add(new SideBarPcItem(new DriveInfo(drive.ToString())));
					}
					break;
				}
			}
			break;
		case WinMessage.DrawClipboard:
			if (nextClipboardViewer != IntPtr.Zero && nextClipboardViewer != hwnd) {
				SendMessage(nextClipboardViewer, msg, wParam, lParam);
			}
			DataObjectContent.HandleClipboardChanged();
			break;
		case WinMessage.ChangeCbChain:
			if (wParam == nextClipboardViewer) {
				nextClipboardViewer = lParam == hwnd ? IntPtr.Zero : lParam;
			} else if (nextClipboardViewer != IntPtr.Zero && nextClipboardViewer != hwnd) {
				SendMessage(nextClipboardViewer, msg, wParam, lParam);
			}
			break;
		case WinMessage.DwmColorizationColorChanged:
			ChangeThemeWithSystem();
			break;
		}
		return IntPtr.Zero;
	}

	/// <summary>
	/// 打开给定的路径，如果没有窗口就打开一个新的，如果有窗口就会在FocusedTabControl打开新标签页，需要在UI线程调用
	/// </summary>
	/// <param name="path"></param>
	public static void OpenPath(string path) {
		if (MainWindows.Count == 0) {  // 窗口都关闭了
			new MainWindow(path).Show();
		} else {
			var tabControl = FileTabControl.FocusedTabControl;
			tabControl.MainWindow.BringToFront();
			_ = tabControl.OpenPathInNewTabAsync(path);
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
			EverythingInterop.SetReplyWindow(Hwnd.Handle);
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
		"IsAddToBookmarkShow", typeof(bool), typeof(MainWindow), new PropertyMetadata(false, OnIsAddToBookmarkShowChanged));

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
	public void AddToBookmarks(params string[] filePaths) {
		if (filePaths == null || filePaths.Length == 0) {
			return;
		}
		bookmarkPaths = filePaths;
		currentBookmarkIndex = 0;
		IsAddToBookmarkShow = true;
	}

	private void OnAddToBookmarkConfirmClick(object sender, RoutedEventArgs e) {
		isDeleteBookmark = false;
		IsAddToBookmarkShow = false;
	}

	private void OnAddToBookmarkDeleteClick(object sender, RoutedEventArgs e) {
		isDeleteBookmark = true;
		IsAddToBookmarkShow = false;
	}

	private static async void OnIsAddToBookmarkShowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var window = (MainWindow)d;
		var bookmarkItem = window.bookmarkPaths[window.currentBookmarkIndex];
		if ((bool)e.NewValue) {
			var fullPath = Path.GetFullPath(bookmarkItem);
			var dbBookmark = BookmarkDbContext.Instance.BookmarkDbSet.Local.FirstOrDefault(b => b.FullPath == fullPath);
			if (dbBookmark != null) {
				window.AddToBookmarkTipTextBlock.Text = "EditBookmark".L();
				window.BookmarkName = dbBookmark.Name;
				window.BookmarkCategoryComboBox.SelectedItem = dbBookmark.Category;
			} else {
				window.AddToBookmarkTipTextBlock.Text = "AddToBookmarks".L();
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
						dbBookmark.UpdateUI(nameof(dbBookmark.Name));
						dbBookmark.Category = categoryItem;
						await bookmarkDb.SaveChangesAsync();
					} else {
						var item = new BookmarkItem(bookmarkItem, window.BookmarkName, categoryItem);
						await bookmarkDb.BookmarkDbSet.AddAsync(item);
						await bookmarkDb.SaveChangesAsync();
						item.LoadIcon();
					}
					foreach (var updateItem in MainWindows.SelectMany(mw => mw.SplitGrid).SelectMany(f => f.TabItems).SelectMany(i => i.Items).Where(i => i.FullPath == fullPath)) {
						updateItem.UpdateUI(nameof(updateItem.IsBookmarked));
					}
				}
				window.currentBookmarkIndex++;
				if (window.currentBookmarkIndex < window.bookmarkPaths.Length) {
					window.IsAddToBookmarkShow = true;
				}
			}
		}
	}

	public static async Task RemoveFromBookmark(params string[] filePaths) {
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
				foreach (var updateItem in MainWindows.SelectMany(mw => mw.SplitGrid).SelectMany(f => f.TabItems).SelectMany(i => i.Items).Where(i => i.FullPath == fullPath)) {
					updateItem.UpdateUI(nameof(updateItem.IsBookmarked));
				}
			}
		}
	}
	#endregion

	protected override void OnClosing(CancelEventArgs e) {
		if (SplitGrid.FileTabControl.TabItems.Count > 1 || SplitGrid.AnyOtherTabs) {
			if (!MessageBoxHelper.AskWithDefault("CloseMultiTabs", "#YouHaveOpenedMoreThanOneTab".L())) {
				e.Cancel = true;
			}
		}
		base.OnClosing(e);
	}

	protected override void OnClosed(EventArgs e) {
		MainWindows.Remove(this);
		SplitGrid.Close();
		if (isClipboardViewerSet) {
			if (nextClipboardViewer != IntPtr.Zero) {
				ChangeClipboardChain(Hwnd.Handle, nextClipboardViewer);
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

	private void ChangeTheme(bool isDarkTheme, bool useAnimation = true) {
		var brushes = new ResourceDictionary {
			Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Basic/Brushes.xaml", UriKind.Absolute)
		};
		var newColors = new ResourceDictionary {
			Source = new Uri(isDarkTheme ? "pack://application:,,,/HandyControl;component/Themes/Basic/Colors/ColorsDark.xaml" : "pack://application:,,,/HandyControl;component/Themes/Basic/Colors/Colors.xaml", UriKind.Absolute)
		};
		var resources = Application.Current.Resources;
		foreach (string brushName in brushes.Keys) {
			if (resources[brushName] is SolidColorBrush sc) {
				var newColorName = brushName[..^5] + "Color";
				if (sc.IsFrozen) {
					sc = sc.Clone();
					if (useAnimation) {
						sc.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation((Color)newColors[newColorName], new Duration(TimeSpan.FromMilliseconds(300))));
					} else {
						sc.SetValue(SolidColorBrush.ColorProperty, newColors[newColorName]);
					}
					resources[brushName] = sc;
				} else {
					if (useAnimation) {
						sc.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation((Color)newColors[newColorName], new Duration(TimeSpan.FromMilliseconds(300))));
					} else {
						sc.SetValue(SolidColorBrush.ColorProperty, newColors[newColorName]);
					}
				}
			}
		}
		EnableMica(isDarkTheme);
	}

	private void OnDragAreaMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
		DragMove();
	}

	protected override void OnStateChanged(EventArgs e) {
		base.OnStateChanged(e);
		var isMaximized = WindowState == WindowState.Maximized;
		ConfigHelper.Save("WindowMaximized", isMaximized);
		if (isMaximized) {
			BorderThickness = new Thickness(8);
		} else {
			BorderThickness = new Thickness(2);
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

	protected override void OnPreviewKeyDown(KeyEventArgs e) {
		var mouseOverTab = FileTabControl.MouseOverTabControl?.SelectedTab;
		if (mouseOverTab != null) {
			if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
				switch (e.Key) {
				case Key.Z:
					break;
				case Key.X:
					mouseOverTab.FileItemCommand.Execute("Cut");
					break;
				case Key.C:
					mouseOverTab.FileItemCommand.Execute("Copy");
					break;
				case Key.V:
					mouseOverTab.FileItemCommand.Execute("Paste");
					break;
				case Key.A:
					mouseOverTab.FileListView.SelectAll();
					break;
				case Key.I:
					mouseOverTab.FileListView.InverseSelection();
					break;
				}
			} else {
				switch (e.Key) {
				case Key.Delete:
					mouseOverTab.FileItemCommand.Execute("Delete");
					break;
				case Key.Left:
					ChangeTheme(true);
					break;
				case Key.Right:
					ChangeTheme(false);
					break;
				}
			}
		}
		base.OnPreviewKeyDown(e);
	}

	private void Sidebar_OnSizeChanged(object sender, SizeChangedEventArgs e) {
		int width;
		if (e.WidthChanged && (width = (int)e.NewSize.Width) != (int)e.PreviousSize.Width && SidebarTabControl.SelectedIndex != -1) {
			ConfigHelper.Save("SidebarWidth", width);
		}
	}

	private bool loaded;
	private const double SidebarDefaultWidth = 300;

	private void OnSidebarSelectionChanged(object sender, SelectionChangedEventArgs e) {
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

	private void TabItem_OnDragEnter(object sender, DragEventArgs e) {
		var tabItem = sender.FindParent<TabItem>();
		if (tabItem != null) {
			SidebarTabControl.SelectedItem = tabItem;
		}
	}

	private void BookmarksSideBarContent_OnFileDrop(string[] filePaths) {
		AddToBookmarks(filePaths);
	}

	private void RecycleBinSideBarContent_OnFileDrop(string[] filePaths) {
		try {
			FileUtils.FileOperation(FileOpType.Delete, filePaths);
		} catch (Exception ex) {
			Logger.Exception(ex);
		}
	}

	/// <summary>
	/// 清除TextBox的焦点
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	private void MainWindow_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
		if (Keyboard.FocusedElement is TextBox && e.OriginalSource.FindParent<TextBox>() == null) {
			FocusManager.SetFocusedElement(this, null);
			Keyboard.ClearFocus();
		}
	}
}