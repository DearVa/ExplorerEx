using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ExplorerEx.Annotations;
using ExplorerEx.Command;
using ExplorerEx.Converter;
using ExplorerEx.Converter.Grouping;
using ExplorerEx.Definitions.Interfaces;
using ExplorerEx.Models;
using ExplorerEx.Models.Enums;
using ExplorerEx.Services;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.Utils.Collections.Internals;
using ExplorerEx.ViewModels;
using ExplorerEx.Win32;
using HandyControl.Controls;
using HandyControl.Tools;
using GridView = System.Windows.Controls.GridView;
using ScrollViewer = System.Windows.Controls.ScrollViewer;

namespace ExplorerEx.Views.Controls; 

/// <summary>
/// 要能够响应鼠标事件，处理点选、框选、拖放、重命名和双击
/// </summary>
public partial class FileListView : INotifyPropertyChanged {
	/// <summary>
	/// 正在拖放的路径列表
	/// </summary>
	private static string[]? draggingPaths;

	public new static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
		nameof(ItemsSource), typeof(DispatchedObservableCollection<FileListViewItem>), typeof(FileListView), new PropertyMetadata(ItemsSource_OnChanged));

	private static void ItemsSource_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var fileGrid = (FileListView)d;
		if (e.OldValue is DispatchedObservableCollection<FileListViewItem> oldList) {
			oldList.CollectionChanged -= fileGrid.OnItemsChanged;
		}
		if (e.NewValue is DispatchedObservableCollection<FileListViewItem> newList) {
			newList.CollectionChanged += fileGrid.OnItemsChanged;
			((ItemsControl)fileGrid).ItemsSource = newList;
			fileGrid.listCollectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(newList);
		} else {
			((ItemsControl)fileGrid).ItemsSource = null;
			fileGrid.listCollectionView = null;
		}
	}

	public new DispatchedObservableCollection<FileListViewItem> ItemsSource {
		get => (DispatchedObservableCollection<FileListViewItem>)GetValue(ItemsSourceProperty);
		set => SetValue(ItemsSourceProperty, value);
	}

	public FileTabViewModel ViewModel { get; private set; } = null!;

	public static readonly DependencyProperty OwnerWindowProperty = DependencyProperty.Register(
		nameof(OwnerWindow), typeof(MainWindow), typeof(FileListView), new PropertyMetadata(default(MainWindow)));

	public MainWindow OwnerWindow {
		get => (MainWindow)GetValue(OwnerWindowProperty);
		set => SetValue(OwnerWindowProperty, value);
	}

	public delegate void ItemDoubleClickEventHandler(object sender, ItemClickEventArgs e);

	public static readonly RoutedEvent ItemDoubleClickedEvent = EventManager.RegisterRoutedEvent(
		nameof(ItemDoubleClicked), RoutingStrategy.Bubble, typeof(ItemDoubleClickEventHandler), typeof(FileListView));

	public event ItemDoubleClickEventHandler ItemDoubleClicked {
		add => AddHandler(ItemDoubleClickedEvent, value);
		remove => RemoveHandler(ItemDoubleClickedEvent, value);
	}

	public static readonly DependencyProperty FileViewProperty = DependencyProperty.Register(
		nameof(FileView), typeof(FileView), typeof(FileListView), new PropertyMetadata(default(FileView), OnFileViewChanged));

	public FileView FileView {
		get => (FileView)GetValue(FileViewProperty);
		set => SetValue(FileViewProperty, value);
	}

	private ListCollectionView? listCollectionView;

	public double ItemWidth => FileView.ItemWidth <= 0 ? double.NaN : FileView.ItemWidth;

	public double ItemHeight => FileView.ItemHeight <= 0 ? double.NaN : FileView.ItemHeight;

	public Size ActualItemSize => new(double.IsNaN(ItemWidth) ? ActualWidth : ItemWidth + 2d, ItemHeight + 6d);

	public static readonly DependencyProperty FullPathProperty = DependencyProperty.Register(
		nameof(FullPath), typeof(string), typeof(FileListView), new PropertyMetadata(null, OnFullPathChanged));

	private static void OnFullPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var fileDataGrid = (FileListView)d;
		fileDataGrid.isMouseDown = false;
		fileDataGrid.isRectSelecting = false;
		fileDataGrid.isDoubleClicked = false;
		fileDataGrid.isPreparedForRenaming = false;
	}

	public string? FullPath {
		get => (string)GetValue(FullPathProperty);
		set => SetValue(FullPathProperty, value);
	}

	public static readonly DependencyProperty FolderProperty = DependencyProperty.Register(
		nameof(Folder), typeof(FileListViewItem), typeof(FileListView), new PropertyMetadata(default(FileListViewItem)));

	/// <summary>
	/// 当前文件夹，用于空白右键ContextMenu的DataContext
	/// </summary>
	public FileListViewItem Folder {
		get => (FileListViewItem)GetValue(FolderProperty);
		set => SetValue(FolderProperty, value);
	}

	/// <summary>
	/// 文件打开方式列表
	/// </summary>
	// ReSharper disable once CollectionNeverQueried.Global
	public static ObservableCollection<FileAssocItem> FileAssocList { get; } = new();

	public static readonly DependencyProperty MouseItemProperty = DependencyProperty.Register(
		nameof(MouseItem), typeof(FileListViewItem), typeof(FileListView), new PropertyMetadata(default(FileListViewItem)));

	/// <summary>
	/// 鼠标所在的那一项，随着MouseMove事件更新
	/// </summary>
	public FileListViewItem? MouseItem {
		get => (FileListViewItem)GetValue(MouseItemProperty);
		set => SetValue(MouseItemProperty, value);
	}

	/// <summary>
	/// 选择一个文件，参数为string文件名，不含路径
	/// </summary>
	public SimpleCommand SelectCommand { get; }

	public SimpleCommand SwitchViewCommand { get; }

	private readonly FileGridDataGridColumnsConverter columnsConverter;
	private readonly FileGridListBoxTemplateConverter listBoxTemplateConverter;
	private readonly FileListViewItemContextMenuConverter fileListViewItemContextMenuConverter;

	private readonly ItemsPanelTemplate virtualizingWrapPanel, virtualizingStackPanel;

	private ContextMenu? openedContextMenu;
	private ScrollViewer? scrollViewer;
	private SimplePanel contentPanel = null!;
	private Border selectionRect = null!;
	private ShortcutPopup shortcutPopup = null!;

	public FileListView() {
		DataContextChanged += (_, e) => shortcutPopup.DataContext = ContextMenu!.DataContext = ViewModel = (FileTabViewModel)e.NewValue;
		InitializeComponent();
		((FileListViewBindingContext)Resources["BindingContext"]).FileListView = this;
		ApplyTemplate();
		SelectCommand = new SimpleCommand(Select);
		SwitchViewCommand = new SimpleCommand(OnSwitchView);
		columnsConverter = (FileGridDataGridColumnsConverter)Resources["ColumnsConverter"];
		listBoxTemplateConverter = (FileGridListBoxTemplateConverter)Resources["ListBoxTemplateConverter"];
		fileListViewItemContextMenuConverter = (FileListViewItemContextMenuConverter)Resources["FileListViewItemContextMenuConverter"];
		((FileGridListBoxTemplateConverter)Resources["ListBoxTemplateConverter"]).FileListView = this;

		virtualizingWrapPanel = (ItemsPanelTemplate)Resources["VirtualizingWrapPanel"];
		virtualizingStackPanel = (ItemsPanelTemplate)Resources["VirtualizingStackPanel"];

		showShortcutPopupDelayAction = new DelayAction(TimeSpan.FromMilliseconds(300), () => {
			Win32Interop.GetCursorPos(out shortcutShowMousePoint);
			Dispatcher.Invoke(() => shortcutPopup.IsOpen = true);
		});

		if (!isPreviewTimerInitialized) {
			MainWindow.FrequentTimerElapsed += MouseHoverTimerWork;
			isPreviewTimerInitialized = true;
		}
	}

	private async void OnSwitchView(object? e) {
		if (e is string param && int.TryParse(param, out var type)) {
			await ViewModel.SwitchViewType((ViewSortGroup)type);
		}
	}

	/// <summary>
	/// 用于让SelectionChanged方法判断是不是切换标签页了，如果是就不通知ViewModel改变
	/// </summary>
	private bool isDataContextChanging;

	private static void OnFileViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var fileGrid = (FileListView)d;
		var fileView = (FileView)e.NewValue;
		fileView.PropertyChanged += fileGrid.OnFileViewPropertyChanged;
	}

	private void OnFileViewPropertyChanged(object? sender, PropertyChangedEventArgs e) {
		var fileView = FileView;
		switch (e.PropertyName) {
			case nameof(fileView.FileViewType):
				if (fileView.FileViewType == FileViewType.Details) {
					var view = new GridView();
					columnsConverter.Convert(view.Columns, fileView);
					View = view;
					var padding = Padding;
					contentPanel.Margin = new Thickness(padding.Left, 30d + padding.Top, padding.Right, padding.Bottom);
				} else {
					ItemTemplate = listBoxTemplateConverter.Convert();
					View = null;
					contentPanel.Margin = Padding;
				}
				break;
			case nameof(fileView.PathType):
			case nameof(fileView.DetailLists):
				if (View is GridView gridView) {
					columnsConverter.Convert(gridView.Columns, fileView);
				} else {
					ItemTemplate = listBoxTemplateConverter.Convert();
				}
				break;
			case nameof(fileView.SortBy):
			case nameof(fileView.IsAscending):
				listCollectionView!.IsLiveSorting = true;
				listCollectionView.CustomSort = new FileView.SortByComparer(fileView.SortBy, fileView.IsAscending);
				break;
			case nameof(fileView.GroupBy):
				var groups = listCollectionView!.GroupDescriptions!;
				groups.Clear();
				if (fileView.GroupBy.HasValue) {
					GroupStyle.Panel = virtualizingStackPanel;
					listCollectionView.IsLiveGrouping = true;
					IValueConverter? converter = null;
					switch (fileView.GroupBy) {
						case DetailListType.DateCreated:
						case DetailListType.DateDeleted:
						case DetailListType.DateModified:
							converter = DateTimeGroupingConverter.Instance.Value;
							break;
						case DetailListType.FileSize:
						case DetailListType.TotalSpace:
							converter = FileSizeGroupingConverter.Instance.Value;
							break;
						case DetailListType.Name:
							converter = NameGroupingConverter.Instance.Value;
							break;
					}
					groups.Add(new PropertyGroupDescription(fileView.GroupBy.ToString(), converter));
				} else {
					GroupStyle.Panel = virtualizingWrapPanel;
					listCollectionView.IsLiveGrouping = false;
				}
				break;
			case nameof(fileView.ItemSize):
				OnPropertyChanged(nameof(ItemWidth));
				OnPropertyChanged(nameof(ItemHeight));
				OnPropertyChanged(nameof(ActualItemSize));
				break;
		}
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		scrollViewer = (ScrollViewer)GetTemplateChild("ViewScrollViewer")!;
		contentPanel = (SimplePanel)GetTemplateChild("ContentPanel")!;
		selectionRect = (Border)GetTemplateChild("SelectionRect")!;
		shortcutPopup = (ShortcutPopup)GetTemplateChild("ShortcutPopup")!;
		shortcutPopup.MouseLeave += ShortcutPopup_OnMouseLeave;
	}

	/// <summary>
	/// 选择某一项
	/// </summary>
	/// <param name="fileName"></param>
	public void Select(object? fileName) {
		if (fileName == null) {
			return;
		}
		var item = ItemsSource.FirstOrDefault(item => item.Name == (string)fileName);
		if (item != null) {
			ScrollIntoView(item);
			item.IsSelected = true;
		}
	}

	/// <summary>
	/// 是否鼠标点击了，不加这个可能会从外部拖进来依旧是框选状态
	/// </summary>
	private bool isMouseDown;

	private bool isMouseOnFileNameLabel;

	private bool isPreparedForRenaming;
	/// <summary>
	/// 鼠标按下如果在Row上，就是对应的项；如果不在，就是-1。每次鼠标抬起都重置为-1
	/// </summary>
	private int mouseDownItemIndex;
	/// <summary>
	/// <see cref="mouseDownItemIndex"/>重置为-1之前的值，主要用于shift多选
	/// </summary>
	private int prevMouseDownItemIndex;

	private Point mouseDownPoint;

	/// <summary>
	/// 是否正在框选
	/// </summary>
	private bool isRectSelecting;
	/// <summary>
	/// 是否正在拖放
	/// </summary>
	private bool isDragDropping;

	private Point startSelectionPoint;
	private DispatcherTimer? timer;
	private bool shouldRename;

	private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) {
		if (e.OldStartingIndex == prevMouseDownItemIndex) {
			prevMouseDownItemIndex = -1;
		}
		if (e.OldStartingIndex == mouseDownItemIndex) {
			mouseDownItemIndex = -1;
		}
	}

	public void InverseSelection() {
		foreach (var item in ItemsSource) {
			item.IsSelected = !item.IsSelected;
		}
	}

	/// <summary>
	/// 按照文件名选中一批文件并滚动到
	/// </summary>
	/// <param name="fileNames">不是完整路径</param>
	public void SelectItems(IEnumerable<string>? fileNames) {
		if (fileNames == null) {
			UnselectAll();
			return;
		}
		foreach (var fileName in fileNames) {
			var newItem = ViewModel.AddSingleItem(fileName);
			if (newItem != null) {
				ScrollIntoView(newItem);
				newItem.IsSelected = true;
			}
		}
	}

	/// <summary>
	/// 用于处理双击事件
	/// </summary>
	private bool isDoubleClicked;

	private bool isCtrlPressedWhenMouseDown;
	private bool isShiftPressedWhenMouseDown;

	private FileListViewItem? prevMouseUpItem;
	private Point prevMouseUpPoint;
	private DateTimeOffset prevMouseUpTime;

	/// <summary>
	/// 认真观察了自带文件管理器的交互方式。
	/// 当鼠标左键或右键按下时，分以下几种情况：
	///   如果当前正在重命名，那就判断点击的位置是不是重命名的TextBox，如果是就不处理，否则就立即应用重命名并退出重命名状态
	/// 
	///   如果点击在了项目上
	///	    如果之前没有选中项，那就立即选中该项（“立即”指不等鼠标键抬起）
	///     如果之前选中了其他项目（单选或者多选），那就立即清除其他项的选择，并选中该项
	///     如果之前有且只有当前项目选中，那么就什么也不做，但是要在鼠标键左键（右键不计时）松开之后开始计时，计时结束前若没有其他操作就开始该项的重命名
	///     如果之前选中了多个项目，且该项也是选择状态，就什么也不做。此时如果松开按键，那就取消选择其他项，只选中当前项
	/// 
	///   没有点击在项目上
	///		如果按下了ctrl键，那就是反选
	///		如果没按下ctrl键，那就清空选择，同时记录坐标，为框选做准备
	/// </summary>
	/// <param name="e"></param>
	protected override void OnPreviewMouseDown(MouseButtonEventArgs e) {
		isDoubleClicked = false;

		if (e.OriginalSource.FindParent<Popup>() != null) {  // 如果点在了Popup内
			return;
		}
		if (e.OriginalSource.FindParent<VirtualizingPanel, FileListView>() == null) {  // 如果没有点击在VirtualizingPanel的范围内
			return;
		}
		if (e.OriginalSource.FindParent<Expander, VirtualizingPanel>() != null) {  // 如果点击在了Expander上，也直接返回
			return;
		}

		Focus();
		HideShortcutPopup();

		if (e.ChangedButton is MouseButton.Left or MouseButton.Right) {
			isMouseDown = true;
			shouldRename = false;
			mouseDownPoint = e.GetPosition(contentPanel);
			var item = MouseItem;
			var keyboard = Keyboard.PrimaryDevice;
			isCtrlPressedWhenMouseDown = keyboard.IsKeyDown(Key.LeftCtrl) || keyboard.IsKeyDown(Key.RightCtrl);
			isShiftPressedWhenMouseDown = keyboard.IsKeyDown(Key.LeftShift) || keyboard.IsKeyDown(Key.RightShift);
			if (item != null) {
				mouseDownItemIndex = Items.IndexOf(item);
				if (e.ChangedButton == MouseButton.Left) {
					if (item == prevMouseUpItem &&
					    Math.Abs(mouseDownPoint.X - prevMouseUpPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
					    Math.Abs(mouseDownPoint.Y - prevMouseUpPoint.Y) < SystemParameters.MinimumVerticalDragDistance &&
					    DateTimeOffset.Now <= prevMouseUpTime.AddMilliseconds(Win32Interop.GetDoubleClickTime())) {
						isDoubleClicked = true;
						if (ViewModel.SelectedItems.Count > 1) {  // 如果双击就取消其他项的选择，只选择当前项
							foreach (var fileItem in ViewModel.SelectedItems.Where(i => i != item)) {
								fileItem.IsSelected = false;
							}
						}
						RaiseEvent(new ItemClickEventArgs(ItemDoubleClickedEvent, item));
					} else {
						if (isCtrlPressedWhenMouseDown) {
							prevMouseDownItemIndex = mouseDownItemIndex;
							item.IsSelected = !item.IsSelected;
						} else if (isShiftPressedWhenMouseDown) {
							if (prevMouseDownItemIndex == -1) {
								prevMouseDownItemIndex = mouseDownItemIndex;
								item.IsSelected = true;
							} else {
								if (mouseDownItemIndex != prevMouseDownItemIndex) {
									int startIndex, endIndex;
									if (mouseDownItemIndex < prevMouseDownItemIndex) {
										startIndex = mouseDownItemIndex;
										endIndex = prevMouseDownItemIndex;
									} else {
										startIndex = prevMouseDownItemIndex;
										endIndex = mouseDownItemIndex;
									}
									UnselectAll();
									for (var i = startIndex; i <= endIndex; i++) {
										((FileListViewItem)Items[i]).IsSelected = true;
									}
								}
							}
						} else {
							prevMouseDownItemIndex = mouseDownItemIndex;
							var selectedItems = SelectedItems;
							switch (selectedItems.Count) {
								case 0:
									item.IsSelected = true;
									break;
								case 1 when item.IsSelected && isMouseOnFileNameLabel:
									isPreparedForRenaming = true;
									break;
								case 1:
									UnselectAll();
									item.IsSelected = true;
									break;
								default:
									if (!item.IsSelected) {
										UnselectAll();
										item.IsSelected = true;
									}
									break;
							}
						}
					}
				} else {
					prevMouseDownItemIndex = mouseDownItemIndex;
					var selectedItems = SelectedItems;
					if (selectedItems.Count == 0) {
						item.IsSelected = true;
					} else if (!item.IsSelected) {
						UnselectAll();
						item.IsSelected = true;
					}
				}
			} else {
				if (!isCtrlPressedWhenMouseDown && !isShiftPressedWhenMouseDown) {
					UnselectAll();
				}
				mouseDownItemIndex = -1;
				if (Settings.Current[Settings.CommonSettings.DoubleClickGoUpperLevel].AsBoolean() && prevMouseUpItem == null && e.ChangedButton == MouseButton.Left) {
					if (Math.Abs(mouseDownPoint.X - prevMouseUpPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
					    Math.Abs(mouseDownPoint.Y - prevMouseUpPoint.Y) < SystemParameters.MinimumVerticalDragDistance &&
					    DateTimeOffset.Now <= prevMouseUpTime.AddMilliseconds(Win32Interop.GetDoubleClickTime())) {
						ViewModel.GoToUpperLevelAsync();
					}
				}
			}
			var x = Math.Min(Math.Max(mouseDownPoint.X, 0), contentPanel.ActualWidth);
			var y = Math.Min(Math.Max(mouseDownPoint.Y, 0), contentPanel.ActualHeight);
			startSelectionPoint = new Point(x + scrollViewer!.HorizontalOffset, y + scrollViewer.VerticalOffset);
		}
		e.Handled = true;
	}

	/// <summary>
	/// 框选或者拖放时，自动滚动的速度
	/// </summary>
	private Vector scrollSpeed;

	/// <summary>
	/// 鼠标移动时，分为以下几种情况
	///    如果鼠标点击在项目上，那就进行拖放
	///    如果不在项目上，那就进行框选
	/// </summary>
	/// <param name="e"></param>
	protected override void OnPreviewMouseMove(MouseEventArgs e) {
		if (e.OriginalSource is DependencyObject o) {
			var mouseItem = MouseItem = ContainerFromElement(o) switch {
				ListBoxItem i => (FileListViewItem)i.Content,
				DataGridRow r => (FileListViewItem)r.Item,
				_ => null
			};
			isMouseOnFileNameLabel = mouseItem != null && o is TextBlock;
		} else {
			MouseItem = null;
			isMouseOnFileNameLabel = false;
		}
		if (!isMouseDown || isDoubleClicked || isDragDropping) {
			return;
		}
		// 只有isMouseDown（即OnPreviewMouseDown触发过）为true，这个才有用
		if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) {
			var point = e.GetPosition(contentPanel);
			if (Math.Abs(point.X - mouseDownPoint.X) >= SystemParameters.MinimumHorizontalDragDistance ||
			    Math.Abs(point.Y - mouseDownPoint.Y) >= SystemParameters.MinimumVerticalDragDistance) {
				if (mouseDownItemIndex != -1) {
					draggingPaths = ViewModel.SelectedItems.Select(static i => i.FullPath).ToArray();
					var selectedItems = ViewModel.SelectedItems;
					var data = new DataObject(DataFormats.FileDrop, selectedItems.Select(static item => item.FullPath).ToArray(), true);
					var allowedEffects = selectedItems.Any(static item => item is DiskDriveItem) ? DragDropEffects.Link : DragDropEffects.All;
					isDragDropping = true;
					DragFilesPreview.IsInternalDrag = true;
					DragDrop.DoDragDrop(this, data, allowedEffects);
					DragFilesPreview.IsInternalDrag = false;
					draggingPaths = null;
					isDragDropping = false;
					DragFilesPreview.HidePreview();
				} else {
					if (!isRectSelecting) {
						UnselectAll();
						selectionRect.Visibility = Visibility.Visible;
						Mouse.Capture(this);
						scrollSpeed = new Vector();
						timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Input, RectSelectScroll, Dispatcher);
						timer.Start();
						isRectSelecting = true;
					}
					UpdateRectSelection();

					if (point.X < 0) {
						scrollSpeed.X = point.X / 5d;
					} else if (point.Y > ActualWidth) {
						scrollSpeed.X = (point.X - ActualWidth) / 5d;
					} else {
						scrollSpeed.X = 0;
					}
					if (point.Y < 0) {
						scrollSpeed.Y = point.Y / 5d;
					} else if (point.Y > ActualHeight) {
						scrollSpeed.Y = (point.Y - ActualHeight) / 5d;
					} else {
						scrollSpeed.Y = 0;
					}

					e.Handled = true;
				}
			}
		}
	}

	private Rect prevSelectRect;

	/// <summary>
	/// 计算框选的元素
	/// </summary>
	private void UpdateRectSelection() {
		var point = Mouse.GetPosition(contentPanel);
		var actualWidth = contentPanel.ActualWidth;
		var x = Math.Min(Math.Max(point.X, 0), actualWidth) + scrollViewer!.HorizontalOffset;
		var y = Math.Min(Math.Max(point.Y, 0), contentPanel.ActualHeight) + scrollViewer.VerticalOffset;
		double l, t, w, h;
		if (x < startSelectionPoint.X) {
			l = x;
			w = startSelectionPoint.X - x;
		} else {
			l = startSelectionPoint.X;
			w = x - startSelectionPoint.X;
		}
		if (y < startSelectionPoint.Y) {
			t = y;
			h = startSelectionPoint.Y - y;
		} else {
			t = startSelectionPoint.Y;
			h = y - startSelectionPoint.Y;
		}
		var margin = new Thickness(l - scrollViewer.HorizontalOffset, t - scrollViewer.VerticalOffset, 0, 0);
		selectionRect.Margin = margin;
		selectionRect.Width = w;
		selectionRect.Height = h;

		var selectRect = new Rect(margin.Left, margin.Top, w, h);
		for (var i = 0; i < ItemsSource.Count; i++) {
			if (ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement element) {
				var topLeft = element.TranslatePoint(new Point(0, 0), contentPanel);
				var itemBounds = new Rect(topLeft.X, topLeft.Y, element.DesiredSize.Width, ItemHeight);
				if (itemBounds.IntersectsWith(selectRect)) {
					((FileListViewItem)element.DataContext).IsSelected = true;
				} else if (itemBounds.IntersectsWith(prevSelectRect)) {
					((FileListViewItem)element.DataContext).IsSelected = false;
				}
			}
		}
		prevSelectRect = selectRect;
	}

	protected override void OnPreviewMouseUp(MouseButtonEventArgs e) {
		do {
			if (!isMouseDown) {  // 只有isMouseDown（即OnPreviewMouseDown触发过）为true，这个才有用
				break;
			}
			isMouseDown = false;
			if (isDoubleClicked) {  // 如果双击了就不处理
				break;
			}
			if (e.ChangedButton is MouseButton.Left or MouseButton.Right) {
				if (isRectSelecting) {  // 如果正在框选，那就取消框选
					isRectSelecting = false;
					selectionRect.Visibility = Visibility.Collapsed;
					Mouse.Capture(null);
					timer?.Stop();
					if (e.ChangedButton == MouseButton.Left && ViewModel.SelectedItems.Count > 0) {
						ShowShortcutPopup();
					} else {
						HideShortcutPopup();
					}
					break;
				}

				if (e.OriginalSource.FindParent<Popup>() != null) {  // 如果点在了Popup内
					break;
				}
				if (e.OriginalSource.FindParent<VirtualizingPanel, FileListView>() == null) {  // 如果没有点击在VirtualizingPanel的范围内
					break;
				}
				if (e.OriginalSource.FindParent<Expander, VirtualizingPanel>() != null) {  // 如果点击在了Expander上，也直接返回
					break;
				}

				var isClickOnItem = mouseDownItemIndex >= 0 && mouseDownItemIndex < ItemsSource.Count;
				if (isClickOnItem) {
					prevMouseUpItem = (FileListViewItem)Items[mouseDownItemIndex];
				} else {
					prevMouseUpItem = null;
				}
				if (isPreparedForRenaming) {
					isPreparedForRenaming = false;
					if (mouseDownItemIndex >= 0 && mouseDownItemIndex < ItemsSource.Count && DateTimeOffset.Now > prevMouseUpTime.AddMilliseconds(Win32Interop.GetDoubleClickTime() * 1.5)) {
						var item = (FileListViewItem)Items[mouseDownItemIndex];
						if (!shouldRename) {
							shouldRename = true;
							Task.Run(async () => {
								await Task.Delay(Win32Interop.GetDoubleClickTime());
								if (shouldRename) {
									await Dispatcher.BeginInvoke(() => ViewModel.StartRename(item));
								}
								shouldRename = false;
							});
						} else {
							shouldRename = false;
						}
					}
				} else {
					switch (e.ChangedButton) {
						case MouseButton.Left when isClickOnItem:
							if (!isCtrlPressedWhenMouseDown && !isShiftPressedWhenMouseDown) {
								foreach (var selectedItem in ViewModel.SelectedItems.Where(selectedItem => selectedItem != prevMouseUpItem).ToList()) {
									selectedItem.IsSelected = false;
								}
							}
							if (ViewModel.SelectedItems.Count > 0) {  // 有可能是反选
								ShowShortcutPopup();
							} else {
								HideShortcutPopup();
							}
							break;
						case MouseButton.Right when isClickOnItem: {
							ShowItemContextMenu((FileListViewItem)Items[mouseDownItemIndex]);
							break;
						}
						case MouseButton.Right:
							UnselectAll();
							openedContextMenu = ContextMenu!;
							openedContextMenu.IsOpen = true;
							break;
					}
				}
				prevMouseUpTime = DateTimeOffset.Now;
				prevMouseUpPoint = e.GetPosition(contentPanel);

				e.Handled = true;
			}
		} while (false);
		mouseDownItemIndex = -1;
	}

	private void ShowItemContextMenu(FileListViewItem item) {
		openedContextMenu = fileListViewItemContextMenuConverter.Convert(item);
		openedContextMenu.SetValue(FileItemAttach.FileItemProperty, item);
		openedContextMenu.DataContext = this;
		var ext = Path.GetExtension(item.FullPath);
		FileAssocList.Clear();
		if (!string.IsNullOrWhiteSpace(ext) && ViewModel.SelectedItems.Count == 1) {
			foreach (var fileAssocItem in FileAssocItem.GetAssocList(ext)) {
				FileAssocList.Add(fileAssocItem);
			}
		}
		item.OnPropertyChanged(nameof(item.IsBookmarked));
		openedContextMenu.IsOpen = true;
	}

	protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
		base.OnSelectionChanged(e);

		if (isDataContextChanging) {
			isDataContextChanging = false;
			return;
		}

		ViewModel.ChangeSelection(e);
	}

	protected override void OnPreviewMouseWheel(MouseWheelEventArgs e) {
		if (previewPopup is { IsOpen: true }) {
			previewPopup.HandleMouseScroll(e);
			e.Handled = true;
		} else {
			base.OnPreviewMouseWheel(e);
		}
	}

	/// <summary>
	/// 屏蔽原有的AutoScroll
	/// </summary>
	/// <param name="e"></param>
	protected override void OnIsMouseCapturedChanged(DependencyPropertyChangedEventArgs e) { }

	private static bool isPreviewTimerInitialized;
	private static PreviewPopup? previewPopup;

	/// <summary>
	/// 用于处理鼠标事件
	/// </summary>
	private void MouseHoverTimerWork() {
		var item = MouseItem;
		// 如果鼠标处没有项目或者没有按下Space
		if (item == null || Keyboard.IsKeyUp(Key.Space)) {
			if (previewPopup is { IsOpen: true }) {  // 如果popup是打开状态，那就关闭
				previewPopup.Close();
				previewPopup = null;
			}
		} else if (OwnerWindow.IsActive && (previewPopup == null || previewPopup.FilePath != item.FullPath)) {  // 有项目且按下了Alt
			var newPopup = PreviewPopup.ChoosePopup(item.FullPath);
			if (previewPopup is { IsOpen: true } && newPopup != previewPopup) {
				previewPopup.Close();
				previewPopup = null;
			}
			if (newPopup != null) {
				previewPopup = newPopup;
				previewPopup.Placement = PlacementMode.Mouse;
				previewPopup.HorizontalOffset = 20d;
				previewPopup.VerticalOffset = 20d;
				previewPopup.Load(item.FullPath);
			}
		}

		if (shortcutPopup.IsOpen) {
			if (!shortcutPopup.IsMouseOver) {
				Win32Interop.GetCursorPos(out var mousePoint);
				int dx = mousePoint.x - shortcutShowMousePoint.x, dy = mousePoint.y - shortcutShowMousePoint.y;
				var dis = dx * dx + dy * dy;
				switch (dis) {
					case > 41000:
						HideShortcutPopup();
						break;
					case > 1000:
						//shortcutPopup.SetValue(BlurPopup.BlurOpacityProperty, (byte)MathF.Round((1 - (dis - 1000f) / 40000) * 255));
						shortcutPopup.Opacity = 1d - (dis - 1000d) / 40000d;
						break;
					default:
						shortcutPopup.Opacity = 1d;
						break;
				}
			}
		}
	}

	public void ScrollIntoView(FileListViewItem? item) {
		if (!isRectSelecting && !isDragDropping) {
			if (item == null) {
				scrollViewer!.ScrollToTop();
			} else {
				ScrollIntoView((object)item);
			}
		}
	}

	/// <summary>
	/// 根据一个字符串来快速选中一项
	/// </summary>
	/// <param name="s"></param>
	public void SelectByText(string s) {
		s = s.ToLower();
		var startIndex = SelectedItems.Count > 1 ? 0 : SelectedIndex + 1;
		for (var i = startIndex; i < Items.Count; i++) {
			var item = (FileListViewItem)Items[i];
			if (item.DisplayText.Length >= s.Length && item.DisplayText[..s.Length].ToLower() == s) {
				UnselectAll();
				ScrollIntoView(item);
				item.IsSelected = true;
				return;
			}
		}
		for (var i = 0; i < startIndex; i++) {
			var item = (FileListViewItem)Items[i];
			if (item.DisplayText.Length >= s.Length && item.DisplayText[..s.Length].ToLower() == s) {
				UnselectAll();
				ScrollIntoView(item);
				item.IsSelected = true;
				return;
			}
		}
		for (var i = startIndex; i < Items.Count; i++) {
			var item = (FileListViewItem)Items[i];
			if (item.DisplayText.Length >= s.Length && item.DisplayText.ToLower().Contains(s)) {
				UnselectAll();
				ScrollIntoView(item);
				item.IsSelected = true;
				return;
			}
		}
		for (var i = 0; i < startIndex; i++) {
			var item = (FileListViewItem)Items[i];
			if (item.DisplayText.ToLower().Contains(s)) {
				UnselectAll();
				ScrollIntoView(item);
				item.IsSelected = true;
				return;
			}
		}
	}

	private void RectSelectScroll(object? sender, EventArgs e) {
		scrollViewer!.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollSpeed.X);
		scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollSpeed.Y);
		UpdateRectSelection();
	}

	protected override void OnDragEnter(DragEventArgs e) {
		HideShortcutPopup();
		isDragDropping = true;
		var data = DataObjectContent.Drag;
		if (data is { Type: DataObjectType.FileDrop }) {  // TODO: 更多格式
			draggingPaths = data.Data as string[];
		} else {
			draggingPaths = null;
		}
		e.Handled = true;
	}

	protected override void OnDragLeave(DragEventArgs e) {
		var mousePos = e.GetPosition(this);
		if (mousePos.X > 0 && mousePos.Y > 0 && mousePos.X < ActualWidth && mousePos.Y < ActualHeight) {
			return;
		}
		isDragDropping = false;
		DragFilesPreview.Singleton.Destination = null;
		if (prevDragOnItem != null) {
			prevDragOnItem.IsSelected = false;
			prevDragOnItem = null;
		}
	}

	/// <summary>
	/// 上一个拖放到的item
	/// </summary>
	private FileListViewItem? prevDragOnItem;

	protected override void OnDragOver(DragEventArgs e) {
		e.Handled = true;
		isDragDropping = true;
		if (FileTabItem.DraggingFileTab != null || draggingPaths == null) {
			return;
		}
		FileListViewItem? mouseItem;
		if (e.OriginalSource is DependencyObject o) {
			mouseItem = ContainerFromElement(o) switch {
				ListBoxItem i => (FileListViewItem)i.Content,
				DataGridRow r => (FileListViewItem)r.Item,
				_ => null
			};
		} else {
			mouseItem = null;
		}
		bool contains;
		string? destination;  // 拖放的目的地
		if (mouseItem != null) {
			destination = mouseItem.DisplayText;
			contains = draggingPaths.Any(path => path == mouseItem.FullPath);
		} else {
			if (FileView.PathType == PathType.Home) {
				e.Effects = DragDropEffects.None;
				if (prevDragOnItem != null) {
					prevDragOnItem.IsSelected = false;
					prevDragOnItem = null;
				}
				DragFilesPreview.Singleton.DragDropEffect = DragDropEffects.None;
				return;
			}
			destination = FullPath;
			contains = draggingPaths.Any(path => path == destination);
		}

		if (prevDragOnItem != mouseItem) {
			if (prevDragOnItem != null) {
				prevDragOnItem.IsSelected = false;
			}
		}

		var dragFilesPreview = DragFilesPreview.Singleton;
		if (contains || Path.GetDirectoryName(draggingPaths[0]) == destination) { // 自己不能往自己身上拖放，相同文件夹禁止移动
			dragFilesPreview.DragDropEffect = e.Effects = DragDropEffects.None;
			return;
		}

		if (mouseItem != null) {
			if (mouseItem is {IsFolder: false} and not FileItem {IsExecutable: true}) { // 不是可执行文件就禁止拖放
				e.Effects = DragDropEffects.None;
			} else {
				prevDragOnItem = mouseItem;
				mouseItem.IsSelected = true; // 让拖放到的item高亮
			}
		}

		dragFilesPreview.Destination = destination;
		if (mouseItem is FileItem { IsExecutable: true }) {
			dragFilesPreview.OperationText = "DragOpenWith";
			dragFilesPreview.Icon = DragDropEffects.Move;
			dragFilesPreview.DragDropEffect = DragDropEffects.All;
		} else {
			dragFilesPreview.DragDropEffect = GetEffectWithKeyboard(e.Effects);
		}
	}

	protected override async void OnDrop(DragEventArgs e) {
		isDragDropping = false;
		if (DataObjectContent.Drag == null) {
			return;
		}
		var path = e.OriginalSource is DependencyObject d ? ContainerFromElement(d) switch {
			ListBoxItem i => ((FileListViewItem)i.Content).FullPath,
			DataGridRow r => ((FileListViewItem)r.Item).FullPath,
			_ => FullPath
		} : FullPath;
		if (path == null) {
			return;
		}
		try {
			var affectedItems = await FileUtils.HandleDrop(DataObjectContent.Drag, path, GetEffectWithKeyboard(e.Effects));
			SelectItems(affectedItems?.Select(Path.GetFileName).Where(static s => !string.IsNullOrWhiteSpace(s))!);
		} catch (Exception ex) {
			Service.Resolve<ILoggerService>().Exception(ex);
		}
	}

	protected override void OnPreviewGiveFeedback(GiveFeedbackEventArgs e) {
		DragFilesPreview.MoveWithCursor();
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
		base.OnRenderSizeChanged(sizeInfo);

		if (sizeInfo.WidthChanged && double.IsNaN(ItemWidth)) {
			OnPropertyChanged(nameof(ActualItemSize));
		}
	}

	protected override void OnTextInput(TextCompositionEventArgs e) { }

	#region 快捷操作菜单

	/// <summary>
	/// 显示快捷操作菜单的时候，鼠标光标所在的位置
	/// </summary>
	private Win32Interop.PointW shortcutShowMousePoint;

	private readonly DelayAction showShortcutPopupDelayAction;

	/// <summary>
	/// 显示快捷操作菜单，会延迟200ms
	/// </summary>
	private void ShowShortcutPopup() {
		if (Settings.Current[Settings.CommonSettings.ShowShortcutPopup].AsBoolean()) {
			if (shortcutPopup.IsOpen) {
				return;
			}
			showShortcutPopupDelayAction.Start();
		}
	}

	private void HideShortcutPopup() {
		showShortcutPopupDelayAction.Stop();
		shortcutPopup.IsOpen = false;
	}

	/// <summary>
	/// 鼠标离开时，更新位置
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	/// <exception cref="NotImplementedException"></exception>
	private void ShortcutPopup_OnMouseLeave(object sender, MouseEventArgs e) {
		Win32Interop.GetCursorPos(out shortcutShowMousePoint);
	}

	private void ShortcutPopup_OnShowMore() {
		Dispatcher.Invoke(() => {
			shortcutPopup.IsOpen = false;
			var item = ViewModel.SelectedItems.FirstOrDefault();
			if (item != null) {
				ShowItemContextMenu(item);
			}
		});
	}

	#endregion

	//protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) {
	//	base.OnLostKeyboardFocus(e);
	//	if (!shortcutPopup.IsKeyboardFocusWithin) {
	//		shortcutPopup.IsOpen = false;
	//	}
	//}

	/// <summary>
	/// 根据键盘按键决定要执行什么操作（Shift移动，Ctrl复制，Alt链接）
	/// </summary>
	/// <param name="effects"></param>
	/// <returns></returns>
	private static DragDropEffects GetEffectWithKeyboard(DragDropEffects effects) {
		var keyboard = Keyboard.PrimaryDevice;
		if (effects.HasFlag(DragDropEffects.Move)) {
			if (keyboard.IsKeyDown(Key.LeftShift) || keyboard.IsKeyDown(Key.RightShift)) {
				return DragDropEffects.Move;
			}
		} else if (effects.HasFlag(DragDropEffects.Copy)) {
			if (keyboard.IsKeyDown(Key.LeftCtrl) || keyboard.IsKeyDown(Key.RightCtrl)) {
				return DragDropEffects.Copy;
			}
		} else if (effects.HasFlag(DragDropEffects.Link)) {
			if (keyboard.IsKeyDown(Key.LeftAlt) || keyboard.IsKeyDown(Key.RightAlt)) {
				return DragDropEffects.Link;
			}
		}
		return effects.GetActualEffect();
	}

	public class ItemClickEventArgs : RoutedEventArgs {
		public FileListViewItem Item { get; }

		public ItemClickEventArgs(RoutedEvent e, FileListViewItem item) {
			RoutedEvent = e;
			Item = item;
		}
	}

	#region MenuItem点击事件，用Binding的话太浪费资源了
	private void Refresh_OnClick(object sender, RoutedEventArgs e) {
		ViewModel.Refresh();
	}

	private void NewFolder_OnClick(object sender, RoutedEventArgs e) {
		ViewModel.CreateCommand.Execute(CreateFolderItem.Singleton);
	}

	private void FormatDiskDrive_OnClick(object sender, RoutedEventArgs e) {
		foreach (var item in ViewModel.SelectedItems.Where(static i => i is DiskDriveItem).Cast<DiskDriveItem>().ToImmutableList()) {
			Shell32Interop.ShowFormatDriveDialog(item.Drive);
		}
	}

	private void ChooseAnotherAppMenuItem_OnClick(object sender, RoutedEventArgs e) {
		if (openedContextMenu == null) {
			return;
		}
		openedContextMenu.IsOpen = false;
		var fullPath = ((FileListViewItem)openedContextMenu.GetValue(FileItemAttach.FileItemProperty)).FullPath;
		Dispatcher.BeginInvoke(DispatcherPriority.Background, () => Shell32Interop.ShowOpenAsDialog(fullPath));
	}
	#endregion

	public event PropertyChangedEventHandler? PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}