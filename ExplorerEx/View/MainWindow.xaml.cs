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
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using hc = HandyControl.Controls;
using HandyControl.Data;

namespace ExplorerEx.View;

public sealed partial class MainWindow {
	public event Action<uint, EverythingInterop.QueryReply> EverythingQueryReplied;

	private static readonly List<MainWindow> MainWindows = new();

	/// <summary>
	/// 窗口打开时，每100ms触发一次
	/// </summary>
	public static event Action FrequentTimerElapsed;

	private static DispatcherTimer frequentTimer;

	public SimpleCommand SideBarItemPreviewMouseUpCommand { get; }
	public SimpleCommand SideBarItemClickCommand { get; }
	public FileItemCommand BookmarkItemCommand { get; }
	public FileItemCommand SideBarPcItemCommand { get; }
	public SimpleCommand EditBookmarkCommand { get; }
	public SplitGrid SplitGrid { get; }
	public HwndSource Hwnd { get; }

	private IntPtr nextClipboardViewer;
	private bool isClipboardRegistered;

	/// <summary>
	/// 每注册一个EverythingQuery就+1
	/// </summary>
	private static volatile uint globalEverythingQueryId;
	private readonly HashSet<uint> everythingQueryIds = new();

	private readonly string startupPath;

	private readonly FileSystemItemContextMenuConverter bookmarkItemContextMenuConverter;
	private readonly ContextMenu sideBarPcItemContextMenu;

	public MainWindow(string startupPath, bool startUpLoad = true) {
		this.startupPath = startupPath;
		MainWindows.Add(this);

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
		if (MainWindows.Count > 1 || isInvalidPos) {
			var rand = new Random((int)DateTime.Now.Ticks);
			left += rand.Next(-100, 100);
			top += rand.Next(-100, 100);
		}
		Left = Math.Min(Math.Max(300 - Width, left), screenWidth - 100);
		Top = Math.Min(Math.Max(0, top), screenHeight - 100);

		DataContext = this;
		InitializeComponent();

		bookmarkItemContextMenuConverter = (FileSystemItemContextMenuConverter)Resources["BookmarkItemContextMenuConverter"];
		sideBarPcItemContextMenu = (ContextMenu)Resources["SideBarPcItemContextMenu"];
		sideBarPcItemContextMenu.DataContext = this;

		SideBarItemPreviewMouseUpCommand = new SimpleCommand(SideBarItem_OnPreviewMouseUp);
		SideBarItemClickCommand = new SimpleCommand(SideBarItem_OnClick);

		BookmarkItemCommand = new FileItemCommand {
			SelectedItemsProvider = () => SideBarBookmarksTreeView.SelectedItem is BookmarkItem selectedItem ? new[] { selectedItem } : Array.Empty<FileListViewItem>(),
			TabControlProvider = () => FileTabControl.MouseOverTabControl
		};
		SideBarPcItemCommand = new FileItemCommand {
			SelectedItemsProvider = () => SideBarThisPcTreeView.SelectedItem is FolderOnlyItem selectedItem ? new[] { selectedItem } : Array.Empty<FileListViewItem>(),
			TabControlProvider = () => FileTabControl.MouseOverTabControl
		};
		EditBookmarkCommand = new SimpleCommand(_ => {
			if (SideBarBookmarksTreeView.SelectedItem is BookmarkItem selectedItem) {
				AddToBookmarks(selectedItem.FullPath);
			}
		});
		RenameTextBox.VerifyFunc = (fileName) => new OperationResult<bool> { Data = !FileUtils.IsProhibitedFileName(fileName) };

		if (ConfigHelper.LoadBoolean("WindowMaximized")) {
			WindowState = WindowState.Maximized;
			BorderThickness = new Thickness(8);
		}

		Hwnd = HwndSource.FromHwnd(new WindowInteropHelper(this).EnsureHandle())!;
		if (MainWindows.Count == 1) {  // 只在一个窗口上检测剪贴板变化事件
			RegisterClipboard();
			RecycleBinItem.RegisterWatcher();

			if (frequentTimer == null) {
				frequentTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Input, (_, _) => FrequentTimerElapsed?.Invoke(), Dispatcher);
			} else {
				frequentTimer.Start();
			}
		}
		Hwnd.AddHook(WndProc);

		SplitGrid = new SplitGrid(this, null);
		ContentGrid.Children.Add(SplitGrid);

		EnableMica(App.IsDarkTheme);

		if (FolderOnlyItem.Home.Children.Count == 0) {
			foreach (var driveInfo in DriveInfo.GetDrives()) {
				FolderOnlyItem.Home.Children.Add(new FolderOnlyItem(driveInfo));
			}
		}

		if (startUpLoad) {
			StartupLoad();
		}
	}

	private void SideBarItem_OnPreviewMouseUp(object args) {
		var e = (MouseButtonEventArgs)args;
		if (e.ChangedButton == MouseButton.Right && e.OriginalSource is FrameworkElement { DataContext: FileListViewItem fileItem }) {
			fileItem.IsSelected = true;
			switch (fileItem) {
			case BookmarkItem bookmarkItem:
				var menu = (ContextMenu)bookmarkItemContextMenuConverter.Convert(bookmarkItem, null, null, null)!;
				menu.DataContext = this;
				menu.SetValue(FileItemAttach.FileItemProperty, bookmarkItem);
				menu.IsOpen = true;
				e.Handled = true;
				break;
			case FolderOnlyItem pcItem:
				sideBarPcItemContextMenu.SetValue(FileItemAttach.FileItemProperty, pcItem);
				sideBarPcItemContextMenu.IsOpen = true;
				e.Handled = true;
				break;
			}
		}
	}

	private static async void SideBarItem_OnClick(object args) {
		var e = (RoutedEventArgs)args;
		if (e.OriginalSource is FrameworkElement element) {
			switch (element.DataContext) {
			case FileListViewItem fileItem:
				fileItem.IsSelected = true;
				if (fileItem.IsFolder) {
					await FileTabControl.FocusedTabControl.SelectedTab.LoadDirectoryAsync(fileItem.FullPath);
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

	/// <summary>
	/// 注册事件监视剪贴板变化
	/// </summary>
	private void RegisterClipboard() {
		nextClipboardViewer = SetClipboardViewer(Hwnd.Handle);
		Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
		isClipboardRegistered = true;
		DataObjectContent.HandleClipboardChanged();
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
							case PathType.LocalFolder:
							case PathType.Zip: {
								if (tabItem.FullPath[0] == drive) {
									_ = tabItem.LoadDirectoryAsync(null);  // 驱动器移除，返回主页
								}
								break;
							}
							}
						}
					}
					var home = FolderOnlyItem.Home;
					for (var i = 0; i < home.Children.Count; i++) {
						if (home.Children[i].FullPath[0] == drive) {
							home.Children.RemoveAt(i);
							break;
						}
					}
					if (param == 0x8000) {
						home.Children.Add(new FolderOnlyItem(new DriveInfo(drive.ToString())));
					}
					RecycleBinItem.RegisterWatcher();
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
			App.ChangeTheme(App.IsDarkTheme, ((SolidColorBrush)SystemParameters.WindowGlassBrush).Color);
			EnableMica(App.IsDarkTheme);
			break;
		}
		return IntPtr.Zero;
	}

	/// <summary>
	/// 打开给定的路径，如果没有窗口就打开一个新的，如果有窗口就会在FocusedTabControl打开新标签页，需要在UI线程调用
	/// </summary>
	/// <param name="path"></param>
	public static async Task OpenPath(string path) {
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
			new MainWindow(null).Show();
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
						item.LoadIcon(FileListViewItem.LoadDetailsOptions.Default);
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

	#region 消息框

	private DispatcherFrame messageBoxFrame;
	private MessageBoxResult messageBoxResult;

	/// <summary>
	/// 还未封装完，目前只用来重命名
	/// </summary>
	/// <returns></returns>
	public MessageBoxResult ShowMessageBox() {
		if (messageBoxFrame != null) {
			Logger.Error("Error");
		}
		MessageBoxMaskBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(1d, TimeSpan.FromMilliseconds(300d)) {
			EasingFunction = new CubicEase {
				EasingMode = EasingMode.EaseOut
			}
		});
		var scaleInAnimation = new DoubleAnimation(0.8d, 1d, TimeSpan.FromMilliseconds(300d)) {
			EasingFunction = new BackEase {
				EasingMode = EasingMode.EaseOut,
				Amplitude = 1.2d
			}
		};
		scaleInAnimation.Completed += (_, _) => RenameTextBox.Focus();
		MessageBoxBorderScaleTf.BeginAnimation(ScaleTransform.ScaleXProperty, scaleInAnimation);
		MessageBoxBorderScaleTf.BeginAnimation(ScaleTransform.ScaleYProperty, scaleInAnimation);
		MessageBoxMaskBorder.Visibility = Visibility.Visible;
		messageBoxFrame = new DispatcherFrame();
		Dispatcher.PushFrame(messageBoxFrame);
		return messageBoxResult;
	}

	private void CloseMessageBox() {
		var easingFunction = new CubicEase {
			EasingMode = EasingMode.EaseOut
		};
		MessageBoxMaskBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(0d, TimeSpan.FromMilliseconds(300d)) {
			EasingFunction = easingFunction
		});
		var scaleXAnimation = new DoubleAnimation(1d, 1.2d, TimeSpan.FromMilliseconds(300d)) {
			EasingFunction = easingFunction
		};
		var scaleYAnimation = new DoubleAnimation(1d, 1.2d, TimeSpan.FromMilliseconds(300d)) {
			EasingFunction = easingFunction
		};
		scaleYAnimation.Completed += (_, _) => {
			messageBoxFrame.Continue = false;
			messageBoxFrame = null;
			MessageBoxMaskBorder.Visibility = Visibility.Collapsed;
		};
		MessageBoxBorderScaleTf.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
		MessageBoxBorderScaleTf.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
	}

	private void MessageBoxOk_OnClick(object sender, RoutedEventArgs e) {
		messageBoxResult = MessageBoxResult.OK;
		CloseMessageBox();
	}

	private void MessageBoxCancel_OnClick(object sender, RoutedEventArgs e) {
		messageBoxResult = MessageBoxResult.Cancel;
		CloseMessageBox();
	}

	public string StartRename(string originalName) {
		RenameTextBox.Text = originalName;
		var lastDot = originalName.LastIndexOf('.');
		if (lastDot != -1) {
			RenameTextBox.Select(0, lastDot);
		} else {
			RenameTextBox.SelectAll();
		}
		if (ShowMessageBox() == MessageBoxResult.OK && !RenameTextBox.IsError) {
			return RenameTextBox.Text;
		}
		return null;
	}
	#endregion


	protected override void OnClosing(CancelEventArgs e) {
		if (SplitGrid.FileTabControl.TabItems.Count > 1 || SplitGrid.AnySplitScreen) {
			if (!MessageBoxHelper.AskWithDefault("CloseMultiTabs", "#YouHaveOpenedMoreThanOneTab".L())) {
				e.Cancel = true;
			}
		}
		base.OnClosing(e);
	}

	protected override void OnClosed(EventArgs e) {
		MainWindows.Remove(this);
		SplitGrid.Close();
		if (isClipboardRegistered) {
			if (nextClipboardViewer != IntPtr.Zero) {
				ChangeClipboardChain(Hwnd.Handle, nextClipboardViewer);
			}
			if (MainWindows.Count > 0) { // 通知下一个Window进行Hook
				MainWindows[0].RegisterClipboard();
			}
		}
		if (MainWindows.Count == 0) {
			frequentTimer.Stop();
			RecycleBinItem.UnregisterWatcher();
		}
		base.OnClosed(e);
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
					FileTabControl.MouseOverTabControl.CloseTab(mouseOverTab);
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
				}
			} else {
				switch (e.Key) {
				case Key.Enter:
					mouseOverTab.FileItemCommand.Execute("Open");
					break;
				case Key.Delete:
					mouseOverTab.FileItemCommand.Execute("Delete");
					break;
				case Key.Back:
					mouseOverTab.GoBackAsync();
					break;
				}
			}
		}
		base.OnKeyDown(e);
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
		DragFilesPreview.Instance.DragDropEffect = DragDropEffects.None;
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
		if (MainWindows.Any(mw => mw.Hwnd.Handle == cursorHwnd)) {
			return;  // 只有当真正离开了窗口，才取消显示
		}
		DataObjectContent.HandleDragLeave();
	}

}