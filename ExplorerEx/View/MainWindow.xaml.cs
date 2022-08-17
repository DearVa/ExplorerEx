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
using HandyControl.Tools;
using static ExplorerEx.Win32.Win32Interop;
using static ExplorerEx.Shell32.Shell32Interop;
using ConfigHelper = ExplorerEx.Utils.ConfigHelper;
using TextBox = HandyControl.Controls.TextBox;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Threading;
using ExplorerEx.Database.Interface;
using hc = HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Tools.Interop;

namespace ExplorerEx.View;

public sealed partial class MainWindow {
	/// <summary>
	/// 获取聚焦的窗口，可能为null
	/// </summary>
	public static MainWindow? FocusedWindow => All.Count > 0 ? All.FirstOrDefault(mw => mw.IsFocused) ?? All[0] : null;

	public static readonly IBookmarkDbContext BookmarkDbContext = ConfigHelper.Container.Resolve<IBookmarkDbContext>();


	public event Action<uint, EverythingInterop.QueryReply>? EverythingQueryReplied;

	public static IReadOnlyList<MainWindow> AllWindows => All;

	/// <summary>
	/// 所有打开的窗口
	/// </summary>
	private static readonly List<MainWindow> All = new();

	/// <summary>
	/// 窗口打开时，每100ms触发一次
	/// </summary>
	public static event Action? FrequentTimerElapsed;

	private static readonly DispatcherTimer FrequentTimer = new(TimeSpan.FromMilliseconds(100), DispatcherPriority.Input, (_, _) => FrequentTimerElapsed?.Invoke(), Application.Current.Dispatcher);

	public SimpleCommand SideBarItemPreviewMouseUpCommand { get; }
	public SimpleCommand SideBarItemClickCommand { get; }
	public FileItemCommand BookmarkItemCommand { get; }
	public FileItemCommand SideBarPcItemCommand { get; }
	public SimpleCommand EditBookmarkCommand { get; }
	public SplitGrid SplitGrid { get; }
	public HwndSource Hwnd { get; }

	/// <summary>
	/// 每注册一个EverythingQuery就+1
	/// </summary>
	private static volatile uint globalEverythingQueryId;
	private readonly HashSet<uint> everythingQueryIds = new();

	private readonly string? startupPath;

	private readonly FileSystemItemContextMenuConverter bookmarkItemContextMenuConverter;
	/// <summary>
	/// 由于侧边栏ThisPC不可选中，所以右键按下时用这个代表选中的Item
	/// </summary>
	private FolderOnlyItem? selectedSideBarPcItem;
	/// <summary>
	/// 侧边栏“此电脑”项目的右键菜单
	/// </summary>
	private readonly ContextMenu sideBarPcItemContextMenu;

	private readonly ContextMenu bookmarkCategoryItemContextMenu;

	public MainWindow(string? startupPath, bool startUpLoad = true) {
		this.startupPath = startupPath;
		All.Add(this);
        
		var screenWidth = SystemParameters.PrimaryScreenWidth;
		var screenHeight = SystemParameters.PrimaryScreenHeight;
		Width = Math.Min(ConfigHelper.LoadInt("WindowWidth", 1200), screenWidth);
		Height = ConfigHelper.LoadInt("WindowHeight", 800);

		var left = ConfigHelper.LoadInt("WindowLeft");
		var top = ConfigHelper.LoadInt("WindowTop");
		var isInvalidPos = false;
		if (left < 300 - Width || left > screenWidth - 100) {
			left = (int)(screenWidth - Width) / 2;
			isInvalidPos = true;
		}
		if (top < 0 || left > screenHeight - 100) {
			left = (int)(screenHeight - Height) / 2;
			isInvalidPos = true;
		}
		if (All.Count > 1 || isInvalidPos) {
			var rand = new Random((int)DateTime.Now.Ticks);
			left += rand.Next(-100, 100);
			top += rand.Next(-100, 100);
		}
		Left = Math.Min(Math.Max(300 - Width, left), screenWidth - 100);
		Top = Math.Min(Math.Max(0, top), screenHeight - 100);

		DataContext = this;
		InitializeComponent();
        SideBarBookmarksTreeView.ItemsSource = BookmarkCategoryComboBox.ItemsSource = BookmarkDbContext.GetBindable();

		bookmarkItemContextMenuConverter = (FileSystemItemContextMenuConverter)Resources["BookmarkItemContextMenuConverter"];
		sideBarPcItemContextMenu = (ContextMenu)Resources["SideBarPcItemContextMenu"];
		sideBarPcItemContextMenu.DataContext = this;
		bookmarkCategoryItemContextMenu = (ContextMenu)Resources["BookmarkCategoryItemContextMenu"];
		bookmarkCategoryItemContextMenu.DataContext = this;

		SideBarItemPreviewMouseUpCommand = new SimpleCommand(SideBarItem_OnPreviewMouseUp);
		SideBarItemClickCommand = new SimpleCommand(SideBarItem_OnClick);

		BookmarkItemCommand = new FileItemCommand {
			SelectedItemsProvider = () => SideBarBookmarksTreeView.SelectedItem is BookmarkItem selectedItem ? new[] { selectedItem } : Array.Empty<FileListViewItem>(),
			TabControlProvider = () => FileTabControl.MouseOverTabControl
		};
		SideBarPcItemCommand = new FileItemCommand {
			SelectedItemsProvider = () => selectedSideBarPcItem != null ? new[] { selectedSideBarPcItem } : Array.Empty<FileListViewItem>(),
			TabControlProvider = () => FileTabControl.MouseOverTabControl
		};
		EditBookmarkCommand = new SimpleCommand(e => {
			var contextMenu = e.FindParent<ContextMenu>()!;
			if (contextMenu.GetValue(FileItemAttach.FileItemProperty) is FileListViewItem item) {
				AddToBookmarks(item.FullPath);
			} else if (contextMenu.GetValue(CustomDataAttach.DataProperty) is BookmarkCategory category) {

			}
		});

		var renameDialogContent = new RenameDialogContent {
			RenameTextBox = {
				VerifyFunc = fileName => new OperationResult<bool>(!FileUtils.IsProhibitedFileName(fileName))
			}
		};
		renameContentDialog = new ContentDialog {
			Content = renameDialogContent,
			PrimaryButtonText = "Ok".L(),
			CancelButtonText = "Cancel".L()
		};
		renameContentDialog.SetBinding(ContentDialog.IsPrimaryButtonEnabledProperty, new Binding {
			Source = renameDialogContent.RenameTextBox,
			Path = new PropertyPath(TextBox.IsErrorProperty),
			Mode = BindingMode.OneWay,
			Converter = Application.Current.Resources["Boolean2BooleanReConverter"] as IValueConverter
		});
		renameTextBox = renameDialogContent.RenameTextBox;
		renameContentDialog.Shown += () => {
			if (renameSelectLength != -1) {
				renameTextBox.Select(0, renameSelectLength);
			} else {
				renameTextBox.SelectAll();
			}
			renameTextBox.Focus();
		};

		if (ConfigHelper.LoadBoolean("WindowMaximized")) {
			WindowState = WindowState.Maximized;
			BorderThickness = new Thickness(8);
		}

		FrequentTimer.Start();
		if (All.Count == 1) {  // 有窗口的时候才监视回收站
			RecycleBinItem.RegisterAllWatchers();
		}

		Hwnd = HwndSource.FromHwnd(new WindowInteropHelper(this).EnsureHandle())!;
		Hwnd.AddHook(WndProc);

		SetWindowLong(Hwnd.Handle, GWL_STYLE, GetWindowLong(Hwnd.Handle, GWL_STYLE) & ~WS_SYSMENU);

		SplitGrid = new SplitGrid(this, null);
		ContentGrid.Children.Add(SplitGrid);

		Settings.ThemeChanged += ChangeTheme;
		ChangeTheme();

		if (startUpLoad) {
			StartupLoad();
		}
	}

	private void SideBarItem_OnPreviewMouseUp(object? args) {
		var e = (MouseButtonEventArgs)args!;
		if (e.ChangedButton == MouseButton.Right && e.OriginalSource is FrameworkElement frameworkElement) {
			if (frameworkElement.DataContext is FileListViewItem fileListViewItem) {
				switch (fileListViewItem) {
				case BookmarkItem bookmarkItem:
					bookmarkItem.IsSelected = true;
					if (!File.Exists(bookmarkItem.FullPath) && !Directory.Exists(bookmarkItem.FullPath)) {

					} else {
						var menu = (ContextMenu)bookmarkItemContextMenuConverter.Convert(bookmarkItem, null, null, null)!;
						menu.DataContext = this;
						menu.SetValue(FileItemAttach.FileItemProperty, bookmarkItem);
						menu.IsOpen = true;
					}
					break;
				case FolderOnlyItem folderOnlyItem:
					selectedSideBarPcItem = folderOnlyItem;
					sideBarPcItemContextMenu.SetValue(FileItemAttach.FileItemProperty, folderOnlyItem);
					sideBarPcItemContextMenu.IsOpen = true;
					break;
				}
			} else {
				var bookmarkCategory = (BookmarkCategory)frameworkElement.DataContext;
				bookmarkCategoryItemContextMenu.SetValue(CustomDataAttach.DataProperty, bookmarkCategory);
				bookmarkCategoryItemContextMenu.IsOpen = true;
			}
			e.Handled = true;
		}
	}

	private static async void SideBarItem_OnClick(object? args) {
		var e = (RoutedEventArgs)args!;
		if (e.OriginalSource is FrameworkElement element) {
			switch (element.DataContext) {
			case FolderOnlyItem folderOnlyItem when e is MouseButtonEventArgs:  // Double click
				folderOnlyItem.IsExpanded = !folderOnlyItem.IsExpanded;
				break;
			case FileListViewItem fileItem:
				if (fileItem.IsFolder) {
					await FileTabControl.FocusedTabControl!.SelectedTab.LoadDirectoryAsync(fileItem.FullPath);
				} else {
					try {
						var psi = new ProcessStartInfo {
							FileName = fileItem.FullPath,
							UseShellExecute = true,
							WorkingDirectory = Path.GetDirectoryName(fileItem.FullPath) ?? ""
						};
						if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) && fileItem is FileItem { IsExecutable: true }) {
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
		try {
			if (startupPath == null) {
				_ = SplitGrid.FileTabControl.StartUpLoad(App.Args.Paths.ToArray());
			} else {
				_ = SplitGrid.FileTabControl.StartUpLoad(startupPath);
			}
		} catch (Exception e) {
			App.Fatal(e);
		}
	}

	private WindowBackdrop windowBackdrop;

	public void ChangeTheme() {
		App.ChangeTheme(((SolidColorBrush)SystemParameters.WindowGlassBrush).Color);

		var hwnd = Hwnd.Handle;
		var isWin11 = Environment.OSVersion.Version >= Version.Parse("10.0.22000.0");
		var windowBackdrop = Settings.Current.WindowBackdrop;
		var isDarkMode = Settings.Current.IsDarkMode;

		switch (this.windowBackdrop) {
		case WindowBackdrop.Acrylic:
			InteropMethods.DisableAcrylic(hwnd);
			break;
		case WindowBackdrop.Mica:
			if (isWin11) {
				var falseValue = 0;
				DwmSetWindowAttribute(Hwnd.Handle, DwmWindowAttribute.MicaEffect, ref falseValue, sizeof(uint));
			}
			break;
		}

		switch (windowBackdrop) {
		case WindowBackdrop.Acrylic:
			if (isWin11) {
				InteropMethods.EnableRoundCorner(hwnd);
			}
			InteropMethods.EnableAcrylic(hwnd, isDarkMode, 100U);
			InteropMethods.EnableShadows(hwnd);
			break;
		case WindowBackdrop.Mica:
			if (isWin11) {
				var isDark = isDarkMode ? 1 : 0;
				DwmSetWindowAttribute(Hwnd.Handle, DwmWindowAttribute.UseImmersiveDarkMode, ref isDark, sizeof(uint));
				var trueValue = 1;
				DwmSetWindowAttribute(Hwnd.Handle, DwmWindowAttribute.MicaEffect, ref trueValue, sizeof(uint));
			}
			break;
		}

		this.windowBackdrop = windowBackdrop;
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
		case WinMessage.NcHitTest:
			try {
				// for fixing #886
				// https://developercommunity.visualstudio.com/t/overflow-exception-in-windowchrome/167357
				_ = lParam.ToInt32();

				// Support Windows 11 SnapLayout
				var x = lParam.ToInt32() & 0xffff;
				var y = lParam.ToInt32() >> 16;
				var rect = new Rect(MaximizeToggleButton.PointToScreen(new Point()), new Size(MaximizeToggleButton.Width, MaximizeToggleButton.Height));
				if (rect.Contains(new Point(x, y))) {
					handled = true;
					MaximizeToggleButton.IsMouseOver = true;
					return new IntPtr(9);  // HTMAXBUTTON
				}
				MaximizeToggleButton.IsMouseOver = false;
			} catch (OverflowException) {
				handled = true;
			}
			break;
		case WinMessage.NcLButtonDown:
			try {
				// for fixing #886
				// https://developercommunity.visualstudio.com/t/overflow-exception-in-windowchrome/167357
				_ = lParam.ToInt32();

				// Support Windows 11 SnapLayout
				var x = lParam.ToInt32() & 0xffff;
				var y = lParam.ToInt32() >> 16;
				var rect = new Rect(MaximizeToggleButton.PointToScreen(new Point()), new Size(MaximizeToggleButton.Width, MaximizeToggleButton.Height));
				if (rect.Contains(new Point(x, y))) {
					handled = true;
					MaximizeToggleButton.IsMouseLeftButtonDown = true;
				}
			} catch (OverflowException) {
				handled = true;
			}
			break;
		case WinMessage.NcMouseLeave:
			MaximizeToggleButton.IsMouseOver = false;
			break;
		case WinMessage.NcLButtonUp:
			if (MaximizeToggleButton.IsMouseLeftButtonDown) {
				MaximizeToggleButton.IsMouseLeftButtonDown = false;
				try {
					// for fixing #886
					// https://developercommunity.visualstudio.com/t/overflow-exception-in-windowchrome/167357
					_ = lParam.ToInt32();

					// Support Windows 11 SnapLayout
					var x = lParam.ToInt32() & 0xffff;
					var y = lParam.ToInt32() >> 16;
					var rect = new Rect(MaximizeToggleButton.PointToScreen(new Point()), new Size(MaximizeToggleButton.Width, MaximizeToggleButton.Height));
					if (rect.Contains(new Point(x, y))) {
						handled = true;
						WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
					}
				} catch (OverflowException) {
					handled = true;
				}
			}
			break;
		}
		return IntPtr.Zero;
	}

	/// <summary>
	/// 打开给定的路径，如果没有窗口就打开一个新的，如果有窗口就会在FocusedTabControl打开新标签页，需要在UI线程调用
	/// </summary>
	/// <param name="path"></param>
	public static async Task OpenPath(string? path) {
		if (All.Count == 0) {  // 窗口都关闭了
			new MainWindow(path).Show();
		} else {
			var tabControl = FileTabControl.FocusedTabControl!;
			tabControl.MainWindow.BringToFront();
			await tabControl.OpenPathInNewTabAsync(path);
		}
	}

	/// <summary>
	/// 显示主窗口
	/// </summary>
	public static void ShowWindow() {
		if (All.Count == 0) {  // 窗口都关闭了
			new MainWindow(null).Show();
		} else {
			All[0].BringToFront();
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
	public readonly DependencyProperty IsAddToBookmarkShowProperty = DependencyProperty.Register(
		nameof(IsAddToBookmarkShow), typeof(bool), typeof(MainWindow), new PropertyMetadata(false, OnIsAddToBookmarkShowChanged));

	public bool IsAddToBookmarkShow {
		get => (bool)GetValue(IsAddToBookmarkShowProperty);
		set => SetValue(IsAddToBookmarkShowProperty, value);
	}

	public static readonly DependencyProperty BookmarkNameProperty = DependencyProperty.Register(
		nameof(BookmarkName), typeof(string), typeof(MainWindow), new PropertyMetadata(default(string)));

	public string BookmarkName {
		get => (string)GetValue(BookmarkNameProperty);
		set => SetValue(BookmarkNameProperty, value);
	}

	private void BookmarkNameTextBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
		((TextBox)sender).SelectAll();
	}

	private string[]? bookmarkPaths;
	private int currentBookmarkIndex;
	public string? BookmarkCategory { get; set; }
	private bool isDeleteBookmark;

	/// <summary>
	/// 添加到书签，如果已添加，则会提示编辑
	/// </summary>
	/// <param name="filePaths"></param>
	public void AddToBookmarks(params string[] filePaths) {
		if (filePaths.Length == 0) {
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
		if (window.bookmarkPaths == null || window.BookmarkCategory == null) {
			return;
		}
		var bookmarkItem = window.bookmarkPaths[window.currentBookmarkIndex];
		if ((bool)e.NewValue) {
			var fullPath = Path.GetFullPath(bookmarkItem);
			var dbBookmark = BookmarkDbContext.FindLocalItemFirstOrDefault(b => b.FullPath == fullPath);
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
			if (!window.isDeleteBookmark) {
				var category = window.BookmarkCategory.Trim();
				if (string.IsNullOrWhiteSpace(category)) {
					category = "DefaultBookmark".L();
				}
                var categoryItem = BookmarkDbContext.FindFirstOrDefault(bc => bc.Name == category);
				if (categoryItem == null) {
					categoryItem = new BookmarkCategory(category);
					await BookmarkDbContext.AddAsync(categoryItem);
				}
				var fullPath = Path.GetFullPath(bookmarkItem);
				var dbBookmark = BookmarkDbContext.FindLocalItemFirstOrDefault(b => b.FullPath == fullPath);
				if (dbBookmark != null) {
					dbBookmark.Name = window.BookmarkName;
					dbBookmark.OnPropertyChanged(nameof(dbBookmark.Name));
					dbBookmark.Category = categoryItem;
					await BookmarkDbContext.SaveAsync();
				} else {
					var item = new BookmarkItem(bookmarkItem, window.BookmarkName, categoryItem);
					await BookmarkDbContext.AddAsync(item);
					await BookmarkDbContext.SaveAsync();
					item.LoadIcon(FileListViewItem.LoadDetailsOptions.Default);
				}
				await BookmarkManager.BookmarkItems.SaveChangesAsync();
				await BookmarkManager.BookmarkCategories.SaveChangesAsync();
				foreach (var updateItem in All.SelectMany(mw => mw.SplitGrid).SelectMany(f => f.TabItems).SelectMany(i => i.Items).Where(i => i.FullPath == fullPath)) {
					updateItem.OnPropertyChanged(nameof(updateItem.IsBookmarked));
				}
			}
			window.currentBookmarkIndex++;
			if (window.currentBookmarkIndex < window.bookmarkPaths.Length) {
				window.IsAddToBookmarkShow = true;
			}
		}
	}

	public static async Task RemoveFromBookmark(params string[] filePaths) {
		if (filePaths.Length == 0) {
			return;
		}
        foreach (var filePath in filePaths) {
			var fullPath = Path.GetFullPath(filePath);
			var item = BookmarkDbContext.FindLocalItemFirstOrDefault(b => b.FullPath == fullPath);
			if (item != null) {
                BookmarkDbContext.Remove(item);
				await BookmarkDbContext.SaveAsync();
				foreach (var updateItem in All.SelectMany(mw => mw.SplitGrid).SelectMany(f => f.TabItems).SelectMany(i => i.Items).Where(i => i.FullPath == fullPath)) {
					updateItem.OnPropertyChanged(nameof(updateItem.IsBookmarked));
				}
			}
		}
	}
	#endregion

	#region 重命名

	private readonly ContentDialog renameContentDialog;
	private readonly TextBox renameTextBox;
	private int renameSelectLength;

	/// <summary>
	/// 弹出重命名对话框
	/// </summary>
	/// <param name="title"></param>
	/// <param name="originalName"></param>
	/// <param name="isFolder"></param>
	/// <returns></returns>
	public string? StartRename(string title, string originalName, bool isFolder) {
		renameContentDialog.Title = title;
		renameTextBox.Text = originalName;
		if (isFolder) {
			renameSelectLength = -1;
		} else {
			renameSelectLength = originalName.LastIndexOf('.');
		}
		if (renameContentDialog.Show(this) == ContentDialog.ContentDialogResult.Primary && !renameTextBox.IsError) {
			return renameTextBox.Text;
		}
		return null;
	}

	#endregion

	private void OnDragAreaMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
		DragMove();
	}

	private bool loaded;
	private const double SidebarDefaultWidth = 300;

	private void OnSidebarSelectionChanged(object sender, SelectionChangedEventArgs e) {
		void UpdateSidebarColumnDefinitionWidth() {
			var width = ConfigHelper.LoadDouble("SidebarWidth");
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

	private bool mouseDownOnSidebar;
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
			mouseDownOnSidebar = true;
			var splitter = (GridSplitter)sender;
			splitter.CaptureMouse();
			sidebarStartOffset = SidebarColumnDefinition.ActualWidth - e.GetPosition(this).X;
		}
		e.Handled = true;
	}

	private void SidebarSplitter_OnPreviewMouseMove(object sender, MouseEventArgs e) {
		if (mouseDownOnSidebar) {
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
			}

			if (isOpen) {
				ConfigHelper.SaveToBuffer("SidebarWidth", width);
			}
		}
		e.Handled = true;
	}

	private void SidebarSplitter_OnPreviewMouseUp(object sender, MouseButtonEventArgs e) {
		if (mouseDownOnSidebar && e.ChangedButton is MouseButton.Left or MouseButton.Right) {
			mouseDownOnSidebar = false;
		}
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

	private void EmptyRecycleBinButton_OnClick(object sender, RoutedEventArgs e) {
		SHEmptyRecycleBin(Hwnd.Handle, string.Empty, EmptyRecycleBinFlags.Default);
	}

	private void RestoreAllRecycleBinButton_OnClick(object sender, RoutedEventArgs e) {
		foreach (var recycleBinItem in RecycleBinItem.Items) {
			recycleBinItem.Restore();
		}
	}

	/// <summary>
	/// 清除TextBox的焦点
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	private void MainWindow_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
		if (Keyboard.FocusedElement is TextBox && e.OriginalSource.FindParent<TextBox>() == null) {
			ClearTextBoxFocus();
		}
	}

	public void ClearTextBoxFocus() {
		FocusManager.SetFocusedElement(this, null);
		Keyboard.ClearFocus();
	}

	private static readonly SettingsPanel SettingsPanel = new();

	private void SettingsButton_OnClick(object sender, RoutedEventArgs e) {
		new ContentDialog {
			Title = "Settings".L(),
			Content = SettingsPanel
		}.Show(this);
	}

	#region Overrides

	protected override void OnClosing(CancelEventArgs e) {
		if (SplitGrid.FileTabControl.TabItems.Count > 1 || SplitGrid.AnySplitScreen) {
			if (!ContentDialog.ShowWithDefault("CloseMultiTabs", "#YouHaveOpenedMoreThanOneTab".L())) {
				e.Cancel = true;
			}
		}
		base.OnClosing(e);
	}

	protected override void OnClosed(EventArgs e) {
		Settings.ThemeChanged -= ChangeTheme;

		All.Remove(this);
		SplitGrid.Close();
		if (All.Count == 0) {
			FrequentTimer.Stop();
			RecycleBinItem.UnregisterAllWatchers();
		}
		base.OnClosed(e);
	}

	protected override void OnStateChanged(EventArgs e) {
		base.OnStateChanged(e);
		IsMaximized = WindowState == WindowState.Maximized;
	}

	protected override void OnLocationChanged(EventArgs e) {
		base.OnLocationChanged(e);
		ConfigHelper.SaveToBuffer("WindowLeft", (int)Left);
		ConfigHelper.SaveToBuffer("WindowTop", (int)Top);
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
		base.OnRenderSizeChanged(sizeInfo);
		if (sizeInfo.WidthChanged && (int)sizeInfo.NewSize.Width != (int)sizeInfo.PreviousSize.Width) {
			ConfigHelper.SaveToBuffer("WindowWidth", (int)sizeInfo.NewSize.Width);
		}
		if (sizeInfo.HeightChanged && (int)sizeInfo.NewSize.Height != (int)sizeInfo.PreviousSize.Height) {
			ConfigHelper.SaveToBuffer("WindowHeight", (int)sizeInfo.NewSize.Height);
		}
	}

	protected override void OnKeyDown(KeyEventArgs e) {
		var handled = true;
		if (RootPanel.Children.Count == 2) {  // 没有打开任何对话框
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
					case Key.W:
						FileTabControl.MouseOverTabControl!.CloseTab(mouseOverTab);
						break;
					default:
						handled = false;
						break;
					}
				} else if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
					switch (e.SystemKey) {
					case Key.Left:
						mouseOverTab.GoBackAsync();
						break;
					case Key.Right:
						mouseOverTab.GoForwardAsync();
						break;
					case Key.Up:
						mouseOverTab.GoToUpperLevelAsync();
						break;
					default:
						handled = false;
						break;
					}
				} else {
					switch (e.Key) {
					case Key.Enter:
						mouseOverTab.FileItemCommand.Execute("Open");
						break;
					case Key.Delete:
						Trace.WriteLine("111");
						mouseOverTab.FileItemCommand.Execute("Delete");
						break;
					case Key.Back:
						mouseOverTab.GoBackAsync();
						break;
					default:
						handled = false;
						break;
					}
				}
			}
		} else {
			handled = false;
		}
		e.Handled = handled;
		base.OnKeyDown(e);
	}

	protected override void OnPreviewTextInput(TextCompositionEventArgs e) {
		if (RootPanel.Children.Count == 2 && !string.IsNullOrWhiteSpace(e.Text) && e.OriginalSource is not TextBox and not AddressBar) {  // 没有打开任何对话框
			var mouseOverTabControl = FileTabControl.MouseOverTabControl;
			if (mouseOverTabControl != null) {
				var fileListView = mouseOverTabControl.SelectedTab.FileListView;
				fileListView.Focus();
				fileListView.SelectByText(e.Text);
			}
		}
	}

	protected override void OnPreviewDragEnter(DragEventArgs e) {
		DataObjectContent.HandleDragEnter(e);
		base.OnPreviewDragEnter(e);
	}

	protected override void OnDragEnter(DragEventArgs e) {
		base.OnDragEnter(e);
		e.Effects = DragDropEffects.None;
		e.Handled = true;
	}

	protected override void OnPreviewDragOver(DragEventArgs e) {
		if (!DragFilesPreview.IsInternalDrag) {
			DragFilesPreview.MoveWithCursor();
		}
		base.OnPreviewDragOver(e);
	}

	protected override void OnDragOver(DragEventArgs e) {
		base.OnDragOver(e);
		DragFilesPreview.Singleton.DragDropEffect = DragDropEffects.None;
		e.Effects = DragDropEffects.None;
		e.Handled = true;
	}

	protected override void OnPreviewDrop(DragEventArgs e) {
		DragFilesPreview.HidePreview();
		base.OnPreviewDrop(e);
	}

	protected override void OnPreviewDragLeave(DragEventArgs e) {
		base.OnPreviewDragLeave(e);
		var cursorHwnd = GetCursorHwnd();
		if (All.Any(mw => mw.Hwnd.Handle == cursorHwnd)) {
			return;  // 只有当真正离开了窗口，才取消显示
		}
		DataObjectContent.HandleDragLeave();
	}

	#endregion

	#region 控制按钮

	private void MinimizeWindowButton_OnClick(object sender, RoutedEventArgs e) {
		WindowState = WindowState.Minimized;
	}

	public static readonly DependencyProperty IsMaximizedProperty = DependencyProperty.Register(
		nameof(IsMaximized), typeof(bool), typeof(MainWindow), new PropertyMetadata(default(bool), IsMaximized_OnChanged));

	private static void IsMaximized_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var window = (MainWindow)d;
		var isMaximized = (bool)e.NewValue;
		ConfigHelper.Save("WindowMaximized", isMaximized);
		if (isMaximized) {
			window.WindowState = WindowState.Maximized;
			window.BorderThickness = new Thickness(8);
		} else {
			window.WindowState = WindowState.Normal;
			window.BorderThickness = new Thickness(1);
		}
	}

	public bool IsMaximized {
		get => (bool)GetValue(IsMaximizedProperty);
		set => SetValue(IsMaximizedProperty, value);
	}

	private void CloseWindowButton_OnClick(object sender, RoutedEventArgs e) {
		Close();
	}

	#endregion
}