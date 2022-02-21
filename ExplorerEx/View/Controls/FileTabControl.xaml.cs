using System;
using ExplorerEx.Utils;
using ExplorerEx.ViewModel;
using HandyControl.Data;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ExplorerEx.Model;
using ExplorerEx.Win32;
using HandyControl.Controls;
using ConfigHelper = ExplorerEx.Utils.ConfigHelper;
using MessageBox = HandyControl.Controls.MessageBox;

namespace ExplorerEx.View.Controls;

public partial class FileTabControl {
	/// <summary>
	/// 获取当前被聚焦的TabControl
	/// </summary>
	public static FileTabControl FocusedTabControl { get; private set; }
	/// <summary>
	/// 当前鼠标正在其上的FileTabControl
	/// </summary>
	public static FileTabControl MouseOverTabControl { get; private set; }
	/// <summary>
	/// 所有的TabControl
	/// </summary>
	private static readonly List<FileTabControl> TabControls = new();

	/// <summary>
	/// 标签页
	/// </summary>
	public ObservableCollection<FileViewGridViewModel> TabItems { get; } = new();

	public new static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
		"SelectedIndex", typeof(int), typeof(FileTabControl), new PropertyMetadata(default(int)));

	public new int SelectedIndex {
		get => (int)GetValue(SelectedIndexProperty);
		set {
			if (TabItems.Count == 0) {
				return;
			}
			if (value < 0) {
				value = 0;
			} else if (value >= TabItems.Count) {
				value = TabItems.Count - 1;
			}
			SetValue(SelectedIndexProperty, value);
		}
	}

	public FileViewGridViewModel SelectedTab => TabItems[SelectedIndex];

	public static readonly DependencyProperty IsFileUtilsVisibleProperty = DependencyProperty.Register(
		"IsFileUtilsVisible", typeof(bool), typeof(FileTabControl), new PropertyMetadata(default(bool)));

	public bool IsFileUtilsVisible {
		get => (bool)GetValue(IsFileUtilsVisibleProperty);
		set => SetValue(IsFileUtilsVisibleProperty, value);
	}

	public SimpleCommand TabClosingCommand { get; }
	public SimpleCommand TabMovedCommand { get; }
	public SimpleCommand CreateNewTabCommand { get; }
	/// <summary>
	/// DragEnter、Over是一样的
	/// </summary>
	public SimpleCommand DragCommand { get; }
	public SimpleCommand DropCommand { get; }

	public MainWindow MainWindow { get; }

	public SplitGrid OwnerSplitGrid { get; set; }

	public FileTabControl(MainWindow mainWindow, SplitGrid ownerSplitGrid, FileViewGridViewModel grid) {
		MainWindow = mainWindow;
		OwnerSplitGrid = ownerSplitGrid;
		DataContext = this;
		TabClosingCommand = new SimpleCommand(OnTabClosing);
		TabMovedCommand = new SimpleCommand(OnTabMoved);
		CreateNewTabCommand = new SimpleCommand(OnCreateNewTab);
		DragCommand = new SimpleCommand(OnDrag);
		DropCommand = new SimpleCommand(OnDrop);
		TabControls.Add(this);
		FocusedTabControl ??= this;

		InitializeComponent();

		if (grid != null) {
			TabItems.Add(grid);
		}
	}

	public async Task StartUpLoad(params string[] startupPaths) {
		if (startupPaths == null || startupPaths.Length == 0) {
			await OpenPathInNewTabAsync(null);
		} else {
			foreach (var path in startupPaths) {
				await OpenPathInNewTabAsync(path);
			}
		}
	}

	public void CloseAllTabs() {
		foreach (var tab in TabItems) {
			tab.Dispose();
		}
		TabItems.Clear();
	}

	public async Task OpenPathInNewTabAsync(string path) {
		var newTabIndex = Math.Max(Math.Min(SelectedIndex + 1, TabItems.Count), 0);
		var item = new FileViewGridViewModel(this);
		TabItems.Insert(newTabIndex, item);
		SelectedIndex = newTabIndex;
		if (!await SelectedTab.LoadDirectoryAsync(path)) {
			if (TabItems.Count > 1) {
				TabItems.Remove(item);
			} else {
				MainWindow.Close();
			}
		}
	}

	protected override void OnPreviewMouseUp(MouseButtonEventArgs e) {
		switch (e.ChangedButton) {
		case MouseButton.XButton1:  // 鼠标侧键返回
			SelectedTab.GoBackAsync();
			break;
		case MouseButton.XButton2:
			SelectedTab.GoForwardAsync();
			break;
		}
		base.OnPreviewMouseUp(e);
	}

	protected override void OnGotFocus(RoutedEventArgs e) {
		FocusedTabControl = this;
		base.OnGotFocus(e);
	}

	/// <summary>
	/// 当前TabControl被关闭时，寻找下一个聚焦的TabControl
	/// </summary>
	private static void UpdateFocusedTabControl() {
		if (TabControls.Count == 0) {
			FocusedTabControl = null;
			return;
		}
		var focused = TabControls.FirstOrDefault(tc => tc.IsFocused);
		if (focused != null) {
			FocusedTabControl = focused;
		} else {
			focused = TabControls.FirstOrDefault(tc => tc.MainWindow.IsFocused);
			FocusedTabControl = focused ?? TabControls[0];
		}
	}

	private void OnTabMoved(object args) {
		if (TabItems.Count == 0) {
			if (OwnerSplitGrid.AnyOtherTabs) {
				TabControls.Remove(this);
				OwnerSplitGrid.CancelSplit();
			} else {  // 说明就剩这一个Tab了
				TabControls.Remove(this);
				MainWindow.Close();
			}
			MouseOverTabControl = null;
			UpdateFocusedTabControl();
		} else {
			if (SelectedIndex == 0) {
				SelectedIndex++;
			} else {
				SelectedIndex--;
			}
		}
	}

	private async void OnTabClosing(object args) {
		var e = (CancelRoutedEventArgs)args;
		if (TabItems.Count <= 1) {
			if (OwnerSplitGrid.AnyOtherTabs) {
				TabControls.Remove(this);
				OwnerSplitGrid.CancelSplit();
				MouseOverTabControl = null;
				UpdateFocusedTabControl();
			} else {  // 说明就剩这一个Tab了
				e.Cancel = true;
				e.Handled = true;
				switch (ConfigHelper.LoadInt("LastTabClosed")) {
				case 1:
					MouseOverTabControl = null;
					MainWindow.Close();
					break;
				case 2:
					await SelectedTab.LoadDirectoryAsync(null);
					break;
				default:
					var msi = new MessageBoxInfo {
						Button = MessageBoxButton.OKCancel,
						OkButtonText = "Exit_application".L(),
						CancelButtonText = "Back_to_home".L(),
						Message = "You_closed_the_last_tab_what_do_you_want?".L(),
						CheckBoxText = "Remember_my_choice_and_dont_ask_again".L(),
						IsChecked = false,
						Image = MessageBoxImage.Question
					};
					var result = MessageBox.Show(msi);
					if (msi.IsChecked) {
						ConfigHelper.Save("LastTabClosed", result == MessageBoxResult.OK ? 1 : 2);
					}
					if (result == MessageBoxResult.OK) {
						Application.Current.Shutdown();
					} else {
						await SelectedTab.LoadDirectoryAsync(null);
					}
					break;
				}
			}
		} else {
			SelectedTab.Dispose();
			if (SelectedIndex == 0) {
				SelectedIndex++;
			} else {
				SelectedIndex--;
			}
		}
	}

	private async void OnCreateNewTab(object args) {
		await OpenPathInNewTabAsync(null);
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
		base.OnRenderSizeChanged(sizeInfo);
		if (sizeInfo.WidthChanged) {
			IsFileUtilsVisible = sizeInfo.NewSize.Width > 640d;
		}
	}

	private static void OnDrag(object args) {
		var e = (TabItemDragEventArgs)args;
		var tab = (FileViewGridViewModel)e.TabItem.DataContext;
		if (tab.PathType == PathType.Home) {
			e.DragEventArgs.Effects = DragDropEffects.None;
			return;
		}
		if (FileDataGrid.DragFilesPreview != null) {
			FileDataGrid.DragFilesPreview.Destination = tab.FullPath;
		}
	}

	private static void OnDrop(object args) {
		var e = (TabItemDragEventArgs)args;
		var tab = (FileViewGridViewModel)e.TabItem.DataContext;
		if (tab.PathType == PathType.Home) {
			return;
		}
		FileUtils.HandleDrop(new DataObjectContent(e.DragEventArgs.Data), tab.FullPath, e.DragEventArgs.Effects.GetFirstEffect());
	}

	protected override void OnMouseEnter(MouseEventArgs e) {
		MouseOverTabControl = this;
		base.OnMouseEnter(e);
	}
}