using System;
using ExplorerEx.Utils;
using ExplorerEx.ViewModel;
using HandyControl.Data;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Windows.Input;
using ExplorerEx.Model;
using ExplorerEx.Win32;
using ConfigHelper = ExplorerEx.Utils.ConfigHelper;
using MessageBox = HandyControl.Controls.MessageBox;
using System.IO;
using System.Windows.Controls;
using System.Collections.Specialized;
using System.Collections;
using HandyControl.Interactivity;

namespace ExplorerEx.View.Controls;

[TemplatePart(Name = HeaderPanelKey, Type = typeof(FileTabPanel))]
[TemplatePart(Name = HeaderBorderKey, Type = typeof(Border))]
[TemplatePart(Name = TabBorderRootKey, Type = typeof(Border))]
[TemplatePart(Name = TabBorderKey, Type = typeof(Border))]
[TemplatePart(Name = NewTabButtonKey, Type = typeof(Button))]
[TemplatePart(Name = ContentPanelKey, Type = typeof(Border))]
public partial class FileTabControl {
	#region HandyControl

	private const string HeaderPanelKey = "PART_HeaderPanel";

	private const string HeaderBorderKey = "PART_HeaderBorder";

	/// <summary>
	/// 停靠标签页以及新建标签页按钮的区域，如果是最右上角的，应当把Margin设为0,0,160,0，避免遮挡窗口右上角的控制按钮
	/// </summary>
	private const string TabBorderRootKey = "PART_TabBorderRoot";

	/// <summary>
	/// 停靠标签页的区域
	/// </summary>
	private const string TabBorderKey = "PART_TabBorder";

	private const string NewTabButtonKey = "NewTabButton";

	private const string ContentPanelKey = "contentPanel";

	/// <summary>
	///     标签宽度
	/// </summary>
	public static readonly DependencyProperty TabItemWidthProperty = DependencyProperty.Register(
		nameof(TabItemWidth), typeof(double), typeof(FileTabControl), new PropertyMetadata(200d));

	/// <summary>
	///     标签高度
	/// </summary>
	public static readonly DependencyProperty TabItemHeightProperty = DependencyProperty.Register(
		nameof(TabItemHeight), typeof(double), typeof(FileTabControl), new PropertyMetadata(35d));

	public static readonly DependencyProperty TabBorderRootMarginProperty = DependencyProperty.Register(
		nameof(TabBorderRootMargin), typeof(Thickness), typeof(FileTabControl), new PropertyMetadata(default(Thickness)));

	public Thickness TabBorderRootMargin {
		get => (Thickness)GetValue(TabBorderRootMarginProperty);
		set => SetValue(TabBorderRootMarginProperty, value);
	}

	private Border? headerBorder;

	public Border TabBorder { get; private set; }

	public Border TabBorderRoot { get; private set; }

	public Button NewTabButton { get; private set; }

	public Border ContentPanel { get; private set; }

	/// <summary>
	///     是否为内部操作
	/// </summary>
	internal bool IsInternalAction;

	public FileTabPanel? HeaderPanel { get; private set; }

	/// <summary>
	///     标签宽度
	/// </summary>
	public double TabItemWidth {
		get => (double)GetValue(TabItemWidthProperty);
		set => SetValue(TabItemWidthProperty, value);
	}

	/// <summary>
	///     标签高度
	/// </summary>
	public double TabItemHeight {
		get => (double)GetValue(TabItemHeightProperty);
		set => SetValue(TabItemHeightProperty, value);
	}

	#endregion

	/// <summary>
	/// 获取当前被聚焦的TabControl，只要窗口存在就不为null
	/// </summary>
	public static FileTabControl? FocusedTabControl { get; private set; }

	/// <summary>
	/// 当前鼠标正在其上的FileTabControl，如果没有就返回<see cref="FocusedTabControl"/>
	/// </summary>
	public static FileTabControl? MouseOverTabControl { get; private set; }

	/// <summary>
	/// 所有的TabControl，一个分屏一个
	/// </summary>
	private static readonly List<FileTabControl> TabControls = new();

	/// <summary>
	/// 标签页
	/// </summary>
	public ObservableCollection<FileTabViewModel> TabItems { get; } = new();

	private static readonly Dictionary<FileTabViewModel, FileViewGrid> CachedViews = new();

	public new static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
		nameof(SelectedIndex), typeof(int), typeof(FileTabControl), new PropertyMetadata(0, SelectedIndexProperty_OnChanged));

	/// <summary>
	/// 在这里关闭虚拟化
	/// </summary>
	/// <param name="d"></param>
	/// <param name="e"></param>
	/// <exception cref="NotImplementedException"></exception>
	private static void SelectedIndexProperty_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var tabControl = (FileTabControl)d;
		var index = (int)e.NewValue;
		if (index == -1) {
			tabControl.ContentPanel.Child = null;
			return;
		}
		var tabViewModel = tabControl.TabItems[index];
		if (!CachedViews.TryGetValue(tabViewModel, out var fileViewGrid)) {
			CachedViews.Add(tabViewModel, new FileViewGrid(tabViewModel, tabControl.ContentPanel));
		} else {
			tabControl.ContentPanel.Child = fileViewGrid;
		}
	}

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

	public FileTabViewModel SelectedTab {
		get {
			if (TabItems.Count == 0) {
				throw new IndexOutOfRangeException();
			}
			var index = SelectedIndex;
			if (index < 0 || index > TabItems.Count) {
				return TabItems[0];
			}
			return TabItems[index];
		}
	}

	public MainWindow MainWindow { get; }

	public SplitGrid OwnerSplitGrid { get; set; }

	public static readonly DependencyProperty CanMove2NewWindowProperty = DependencyProperty.Register(
		nameof(CanMove2NewWindow), typeof(bool), typeof(FileTabControl), new PropertyMetadata(default(bool)));

	public bool CanMove2NewWindow {
		get => (bool)GetValue(CanMove2NewWindowProperty);
		set => SetValue(CanMove2NewWindowProperty, value);
	}

	public static readonly DependencyProperty CanSplitScreenProperty = DependencyProperty.Register(
		nameof(CanSplitScreen), typeof(bool), typeof(FileTabControl), new PropertyMetadata(default(bool)));

	public bool CanSplitScreen {
		get => (bool)GetValue(CanSplitScreenProperty);
		set => SetValue(CanSplitScreenProperty, value);
	}

#pragma warning disable CS8618
	public FileTabControl(MainWindow mainWindow, SplitGrid ownerSplitGrid, FileTabViewModel? tab) {
#pragma warning restore CS8618
		MainWindow = mainWindow;
		OwnerSplitGrid = ownerSplitGrid;
		DataContext = this;
		CommandBindings.Add(new CommandBinding(ControlCommands.TabCommand, OnTabCommand));
		TabControls.Add(this);
		TabItems.CollectionChanged += (_, _) => UpdateTabContextMenu();
		FocusedTabControl ??= this;
		MouseOverTabControl ??= this;

		InitializeComponent();
		if (tab != null) {
			TabItems.Add(tab);
		}
	}

	public async Task StartUpLoad(params string[]? startupPaths) {
		if (startupPaths == null || startupPaths.Length == 0) {
			await OpenPathInNewTabAsync(null);
		} else {
			foreach (var path in startupPaths) {
				await OpenPathInNewTabAsync(path);
			}
		}
		if (TabItems.Count == 0) {
			await OpenPathInNewTabAsync(null);
		}
	}

	/// <summary>
	/// 关闭整个TabControl并释放资源
	/// </summary>
	public void Close() {
		foreach (var tab in TabItems) {
			CachedViews.Remove(tab);
			tab.Dispose();
		}
		TabItems.Clear();
		TabControls.Remove(this);
		UpdateFocusedTabControl();
	}

	/// <summary>
	/// 关闭标签页
	/// </summary>
	/// <param name="tab"></param>
	public async void CloseTab(FileTabViewModel tab) {
		var index = TabItems.IndexOf(tab);
		if (index == -1) {
			return;
		}
		if (await HandleTabClosing(tab)) {
			CachedViews.Remove(tab);
			TabItems.RemoveAt(index);
		}
	}

	/// <summary>
	/// 将标签页移动到新窗口，要求自己至少有两个以上的tab或者已经分屏了
	/// </summary>
	/// <param name="tab"></param>
	public void MoveTabToNewWindow(FileTabViewModel tab) {
		if (TabItems.Count <= 1 && !OwnerSplitGrid.AnySplitScreen) {
			return;
		}
		var index = TabItems.IndexOf(tab);
		if (index == -1) {
			return;
		}
		var newWindow = new MainWindow(null, false);
		TabItems.RemoveAt(index);
		if (TabItems.Count == 0) {
			TabControls.Remove(this);
			OwnerSplitGrid.CancelSplit();
			UpdateFocusedTabControl();
			MouseOverTabControl = FocusedTabControl;
		}
		newWindow.SplitGrid.FileTabControl.TabItems.Add(tab);
		newWindow.Show();
		newWindow.BringToFront();
	}

	public async Task OpenPathInNewTabAsync(string? path, string? selectedPath = null) {
		var newTabIndex = Math.Max(Math.Min(SelectedIndex + 1, TabItems.Count), 0);
		var item = new FileTabViewModel(this);
		TabItems.Insert(newTabIndex, item);
		SelectedIndex = newTabIndex;
		if (!await SelectedTab.LoadDirectoryAsync(path, true, selectedPath)) {
			if (TabItems.Count > 1) {
				TabItems.Remove(item);
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
			MouseOverTabControl = FocusedTabControl = null;
			return;
		}
		var focused = TabControls.FirstOrDefault(tc => tc.IsFocused);
		if (focused != null) {
			MouseOverTabControl = FocusedTabControl = focused;
		} else {
			focused = TabControls.FirstOrDefault(tc => tc.MainWindow.IsFocused);
			MouseOverTabControl = FocusedTabControl = focused ?? TabControls[0];
		}
	}
	
	private async void OnTabCommand(object sender, ExecutedRoutedEventArgs e) {
		if (e.OriginalSource is not FileTabItem tabItem) {
			return;
		}
		var tab = (FileTabViewModel)tabItem.DataContext;
		switch (e.Parameter) {
		case "Duplicate":
			await OpenPathInNewTabAsync(tab.FullPath);
			break;
		case "Move2NewWindow":
			MoveTabToNewWindow(tab);
			break;
		case "SplitLeft":
			if (OwnerSplitGrid.Split(tab, SplitOrientation.Left)) {
				TabItems.Remove(tab);
			}
			break;
		case "SplitRight":
			if (OwnerSplitGrid.Split(tab, SplitOrientation.Right)) {
				TabItems.Remove(tab);
			}
			break;
		case "SplitBottom":
			if (OwnerSplitGrid.Split(tab, SplitOrientation.Bottom)) {
				TabItems.Remove(tab);
			}
			break;
		}
	}

	/// <summary>
	/// 当一个Tab被移动到别的TabControl上时触发
	/// </summary>
	/// <param name="tab"></param>
	internal void HandleTabMoved(FileTabViewModel tab) {
		if (TabItems.Count == 0) {  // 如果移走的是最后一个，那就要关闭当前的了
			TabControls.Remove(this);
			if (OwnerSplitGrid.AnySplitScreen) {
				OwnerSplitGrid.CancelSplit();
			} else {  // 说明就剩这一个Tab了
				MainWindow.Close();
			}
			UpdateFocusedTabControl();
			MouseOverTabControl = FocusedTabControl;
		}
		tab.playTabAnimation = true;
		// Trace.WriteLine(string.Join("\n", MainWindow.MainWindows.SelectMany(mw => mw.splitGrid).SelectMany(f => f.TabItems).Select(i => i.FullPath)));
	}

	private async void NewTabButton_OnClick(object sender, RoutedEventArgs e) {
		await OpenPathInNewTabAsync(null);
	}

	/// <summary>
	/// 当tab关闭的时候，根据用户选择或者设置来决定是否关闭
	/// </summary>
	/// <param name="tab"></param>
	/// <returns>是否关闭</returns>
	internal async Task<bool> HandleTabClosing(FileTabViewModel tab) {
		if (TabItems.Count <= 1) {
			if (OwnerSplitGrid.AnySplitScreen) {
				tab.Dispose();
				TabControls.Remove(this);
				OwnerSplitGrid.CancelSplit();
				UpdateFocusedTabControl();
				MouseOverTabControl = FocusedTabControl;
			} else {  // 说明就剩这一个Tab了
				switch (ConfigHelper.LoadInt("LastTabClosed")) {
				case 1:
					tab.Dispose();
					TabControls.Remove(this);
					UpdateFocusedTabControl();
					MouseOverTabControl = FocusedTabControl;
					MainWindow.Close();
					return true;
				case 2:
					await SelectedTab.LoadDirectoryAsync(null);
					return false;
				default:
					var msi = new MessageBoxInfo {
						Button = MessageBoxButton.OKCancel,
						OkButtonText = "CloseWindow".L(),
						CancelButtonText = "BackToHome".L(),
						Message = "#YouClosedTheLastTab".L(),
						CheckBoxText = "RememberMyChoiceAndDontAskAgain".L(),
						IsChecked = false,
						Image = MessageBoxImage.Question
					};
					var result = MessageBox.Show(msi);
					if (msi.IsChecked) {
						ConfigHelper.Save("LastTabClosed", result == MessageBoxResult.OK ? 1 : 2);
					}
					if (result == MessageBoxResult.OK) {
						tab.Dispose();
						TabControls.Remove(this);
						UpdateFocusedTabControl();
						MouseOverTabControl = FocusedTabControl;
						MainWindow.Close();
						return true;
					}
					await SelectedTab.LoadDirectoryAsync(null);
					return false;
				}
			}
		} else {
			tab.Dispose();
		}
		return true;
	}

	private static bool CanDragDrop(DragEventArgs e, FileTabViewModel vm) {
		if (vm.PathType == PathType.Home) {
			e.Effects = DragDropEffects.None;
			e.Handled = true;
			return false;
		}
		if (e.Effects.HasFlag(DragDropEffects.Move) && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } fileList) {
			if (Path.GetDirectoryName(fileList[0]) == vm.FullPath) {  // 相同文件夹禁止移动
				e.Effects = DragDropEffects.None;
				e.Handled = true;
				return false;
			}
		}
		return true;
	}

	private static void TabBorder_OnDragEnter(object s, DragEventArgs e) {
		e.Handled = true;
		var dragFilesPreview = DragFilesPreview.Singleton;
		if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } fileList) {
			var folderList = fileList.Where(Directory.Exists).ToImmutableList();
			if (folderList.Count > 0) {
				dragFilesPreview.OperationText = "DragOpenInNewTab".L();
				dragFilesPreview.Destination = folderList[0];
				dragFilesPreview.DragDropEffect = DragDropEffects.All;
				return;
			}
		}
		dragFilesPreview.DragDropEffect = DragDropEffects.None;
	}

	internal static void TabItem_OnDrag(FileTabItem fileTabItem, DragEventArgs args) {
		var tab = (FileTabViewModel)fileTabItem.DataContext;
		if (!CanDragDrop(args, tab)) {
			return;
		}
		DragFilesPreview.Singleton.Destination = tab.FullPath;
	}

	private async void TabBorder_OnDrop(object s, DragEventArgs e) {
		if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } fileList) {
			foreach (var folderPath in fileList.Where(Directory.Exists)) {
				await OpenPathInNewTabAsync(folderPath);
			}
		}
	}

	internal static void TabItem_OnDrop(FileTabItem fileTabItem, DragEventArgs args) {
		var tab = (FileTabViewModel)fileTabItem.DataContext;
		if (!CanDragDrop(args, tab)) {
			return;
		}
		_ = FileUtils.HandleDrop(DataObjectContent.Parse(args.Data), tab.FullPath, args.Effects.GetActualEffect());
	}

	protected override void OnMouseEnter(MouseEventArgs e) {
		MouseOverTabControl = this;
		base.OnMouseEnter(e);
	}

	/// <summary>
	/// 更新Tab的菜单：是否可分屏和是否可以移动到新窗口
	/// </summary>
	public void UpdateTabContextMenu() {
		var canSplitScreen = TabItems.Count > 1;
		CanSplitScreen = canSplitScreen;
		CanMove2NewWindow = canSplitScreen || OwnerSplitGrid.AnySplitScreen;
	}

	#region HandyControl

	protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e) {
		base.OnItemsChanged(e);

		if (HeaderPanel == null) {
			IsInternalAction = false;
			return;
		}

		if (IsInternalAction) {
			IsInternalAction = false;
			return;
		}

		switch (e.Action) {
		case NotifyCollectionChangedAction.Add: {
			for (var i = 0; i < Items.Count; i++) {
				if (ItemContainerGenerator.ContainerFromIndex(i) is not FileTabItem item) {
					return;
				}
				item.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
				item.FileTabPanel = HeaderPanel;
			}
			break;
		}
		case NotifyCollectionChangedAction.Remove:
			if (Items.Count > 0 && TabIndex >= Items.Count) {
				TabIndex = Items.Count - 1;
			}
			break;
		}

		headerBorder?.InvalidateMeasure();
		IsInternalAction = false;
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		HeaderPanel = (FileTabPanel)GetTemplateChild(HeaderPanelKey)!;
		TabBorder = (Border)GetTemplateChild(TabBorderKey)!;
		TabBorder.MouseEnter += TabBorder_OnMouseEnter;
		TabBorder.DragEnter += TabBorder_OnDragEnter;
		TabBorder.DragOver += (_, e) => e.Handled = true;
		TabBorder.Drop += TabBorder_OnDrop;
		TabBorderRoot = (Border)GetTemplateChild(TabBorderRootKey)!;
		headerBorder = (Border)GetTemplateChild(HeaderBorderKey)!;
		NewTabButton = (Button)GetTemplateChild(NewTabButtonKey)!;
		ContentPanel = (Border)GetTemplateChild(ContentPanelKey)!;
		SelectedIndexProperty_OnChanged(this, new DependencyPropertyChangedEventArgs(SelectedIndexProperty, 0, 0));
	}

	private void TabBorder_OnMouseEnter(object sender, MouseEventArgs e) {
		if (FileTabItem.DraggingFileTab != null) {
			FileTabItem.DragTabDestination = this;
			if (!Items.Contains(FileTabItem.DraggingFileTab.DataContext)) {
				var mouseDownPoint = e.GetPosition(TabBorder);
				var list = GetActualList();
				double tabWidth;
				var newCount = list.Count + 1;
				if (newCount * TabItemWidth > TabBorder.ActualWidth) {
					tabWidth = TabBorder.ActualWidth / newCount;
				} else {
					tabWidth = TabItemWidth;
				}
				var insertIndex = Math.Max(Math.Min((int)(mouseDownPoint.X / tabWidth), list.Count), 0);
				var newTab = FileTabItem.DraggingFileTab;
				list.Insert(insertIndex, newTab.DataContext);
				SelectedIndex = insertIndex;
				newTab.StartDrag(TabBorder, mouseDownPoint, insertIndex);
			}
			FileTabItem.DragFrame!.Continue = false;
		}
	}

	internal void CloseOtherItems(FileTabItem? currentItem) {
		var actualItem = currentItem != null ? ItemContainerGenerator.ItemFromContainer(currentItem) : null;

		var list = GetActualList();
		IsInternalAction = true;

		for (var i = 0; i < Items.Count; i++) {
			var item = list[i];
			if (!Equals(item, actualItem) && item != null) {
				if (ItemContainerGenerator.ContainerFromItem(item) is not FileTabItem tabItem) {
					continue;
				}
				tabItem.ViewModel.Dispose();
				list.Remove(item);
				i--;
			}
		}

		SetCurrentValue(SelectedIndexProperty, Items.Count == 0 ? -1 : 0);
	}

	internal IList GetActualList() {
		IList list;
		if (ItemsSource is IList iList) {
			list = iList;
		} else {
			list = Items;
		}

		return list;
	}

	protected override bool IsItemItsOwnContainerOverride(object item) {
		return item is FileTabItem;
	}

	protected override DependencyObject GetContainerForItemOverride() {
		return new FileTabItem();
	}


	// https://stackoverflow.com/questions/11703833/dragmove-and-maximize
	private Point? dragPoint;

	private void TabBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
		if (e.ClickCount == 2) {
			if (MainWindow.ResizeMode is not ResizeMode.CanResize and not ResizeMode.CanResizeWithGrip) {
				return;
			}

			MainWindow.WindowState = MainWindow.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
		} else {
			if (MainWindow.WindowState == WindowState.Maximized) {
				dragPoint = e.GetPosition(null);
			}
			MainWindow.DragMove();
		}
	}

	private void TabBorder_OnMouseMove(object sender, MouseEventArgs e) {
		if (dragPoint.HasValue) {
			Win32Interop.GetCursorPos(out var point);

			MainWindow.Left = (point.x - dragPoint.Value.X * MainWindow.Width / MainWindow.ActualWidth);
			MainWindow.Top = point.y - dragPoint.Value.Y;
			dragPoint = null;

			MainWindow.WindowState = WindowState.Normal;

			MainWindow.DragMove();
		}
	}

	private void TabBorder_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
		dragPoint = null;
	}
	#endregion
}