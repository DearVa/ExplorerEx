using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ExplorerEx.Converter;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using ExplorerEx.ViewModel;
using ExplorerEx.Win32;
using HandyControl.Controls;
using HandyControl.Tools;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using TabItem = HandyControl.Controls.TabItem;
using TextBox = HandyControl.Controls.TextBox;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 要能够响应鼠标事件，处理点选、框选、拖放、重命名和双击
/// </summary>
public partial class FileGrid {
	private ScrollViewer ViewScrollViewer => FileViewType == FileViewType.Detail ? dataGridScrollViewer : listBoxScrollViewer;
	private ScrollViewer dataGridScrollViewer, listBoxScrollViewer;

	/// <summary>
	/// 如果不为null，说明正在Drag，可以修改Destination或者Effect
	/// </summary>
	public static DragFilesPreview DragFilesPreview { get; private set; }

	private static DragDropWindow dragDropWindow;
	/// <summary>
	/// 正在拖放的items列表，从外部拖进来的不算
	/// </summary>
	private static FileViewBaseItem[] draggingItems;

	public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
		"ItemsSource", typeof(ObservableCollection<FileViewBaseItem>), typeof(FileGrid), new PropertyMetadata(ItemsSource_OnChanged));

	private static void ItemsSource_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var fileGrid = (FileGrid)d;
		if (e.OldValue is ObservableCollection<FileViewBaseItem> oldList) {
			oldList.CollectionChanged -= fileGrid.OnItemsChanged;
		}
		if (e.NewValue is ObservableCollection<FileViewBaseItem> newList) {
			newList.CollectionChanged += fileGrid.OnItemsChanged;
		}
		fileGrid.UpdateView();
	}

	public ObservableCollection<FileViewBaseItem> ItemsSource {
		get => (ObservableCollection<FileViewBaseItem>)GetValue(ItemsSourceProperty);
		set => SetValue(ItemsSourceProperty, value);
	}

	public FileGridViewModel ViewModel { get; private set; }

	public delegate void FileDropEventHandler(object sender, FileDropEventArgs e);

	public static readonly RoutedEvent FileDropEvent = EventManager.RegisterRoutedEvent(
		"FileDrop", RoutingStrategy.Bubble, typeof(FileDropEventHandler), typeof(FileGrid));

	public event FileDropEventHandler FileDrop {
		add => AddHandler(FileDropEvent, value);
		remove => RemoveHandler(FileDropEvent, value);
	}

	public delegate void ItemDoubleClickEventHandler(object sender, ItemClickEventArgs e);

	public static readonly RoutedEvent ItemDoubleClickedEvent = EventManager.RegisterRoutedEvent(
		"ItemDoubleClicked", RoutingStrategy.Bubble, typeof(ItemDoubleClickEventHandler), typeof(FileGrid));

	public event ItemDoubleClickEventHandler ItemDoubleClicked {
		add => AddHandler(ItemDoubleClickedEvent, value);
		remove => RemoveHandler(ItemDoubleClickedEvent, value);
	}

	public static readonly DependencyProperty FileViewTypeProperty = DependencyProperty.Register(
		"FileViewType", typeof(FileViewType), typeof(FileGrid), new PropertyMetadata(OnViewChanged));

	public FileViewType FileViewType {
		get => (FileViewType)GetValue(FileViewTypeProperty);
		set => SetValue(FileViewTypeProperty, value);
	}

	public int ViewTypeIndex => FileViewType switch {
		FileViewType.Icon when ItemSize.Width > 100d && ItemSize.Height > 130d => 0,
		FileViewType.Icon => 1,
		FileViewType.List => 2,
		FileViewType.Detail => 3,
		FileViewType.Tile => 4,
		FileViewType.Content => 5,
		_ => -1
	};

	public static readonly DependencyProperty PathTypeProperty = DependencyProperty.Register(
		"PathType", typeof(PathType), typeof(FileGrid), new PropertyMetadata(OnViewChanged));

	public PathType PathType {
		get => (PathType)GetValue(PathTypeProperty);
		set => SetValue(PathTypeProperty, value);
	}

	public static readonly DependencyProperty ItemSizeProperty = DependencyProperty.Register(
		"ItemSize", typeof(Size), typeof(FileGrid), new PropertyMetadata(OnViewChanged));

	/// <summary>
	/// 项目大小
	/// </summary>
	public Size ItemSize {
		get => (Size)GetValue(ItemSizeProperty);
		set => SetValue(ItemSizeProperty, value);
	}

	public static readonly DependencyProperty DetailListsProperty = DependencyProperty.Register(
		"DetailLists", typeof(List<DetailList>), typeof(FileGrid), new PropertyMetadata(OnViewChanged));

	public List<DetailList> DetailLists {
		get => (List<DetailList>)GetValue(DetailListsProperty);
		set => SetValue(DetailListsProperty, value);
	}

	public static readonly DependencyProperty FullPathProperty = DependencyProperty.Register(
		"FullPath", typeof(string), typeof(FileGrid), new PropertyMetadata(null, FullPath_OnChanged));

	private static void FullPath_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var fileDataGrid = (FileGrid)d;
		fileDataGrid.isMouseDown = false;
		fileDataGrid.isRectSelecting = false;
		fileDataGrid.isDoubleClicked = false;
		fileDataGrid.isPreparedForRenaming = false;
		fileDataGrid.lastBIndex = fileDataGrid.lastRIndex = -1;
		fileDataGrid.lastTIndex = fileDataGrid.lastLIndex = 0;
		fileDataGrid.UpdateView();
	}

	public string FullPath {
		get => (string)GetValue(FullPathProperty);
		set => SetValue(FullPathProperty, value);
	}

	public static readonly DependencyProperty FolderProperty = DependencyProperty.Register(
		"Folder", typeof(FileViewBaseItem), typeof(FileGrid), new PropertyMetadata(default(FileViewBaseItem)));

	/// <summary>
	/// 当前文件夹，用于空白右键ContextMenu的DataContext
	/// </summary>
	public FileViewBaseItem Folder {
		get => (FileViewBaseItem)GetValue(FolderProperty);
		set => SetValue(FolderProperty, value);
	}

	public static readonly DependencyProperty MouseItemProperty = DependencyProperty.Register(
		"MouseItem", typeof(FileViewBaseItem), typeof(FileGrid), new PropertyMetadata(default(FileViewBaseItem)));

	/// <summary>
	/// 鼠标所在的那一项，随着MouseMove事件更新
	/// </summary>
	public FileViewBaseItem MouseItem {
		get => (FileViewBaseItem)GetValue(MouseItemProperty);
		set => SetValue(MouseItemProperty, value);
	}

	/// <summary>
	/// 当前的ItemsControl
	/// </summary>
	public ItemsControl ItemsControl { get; private set; }

	public IList SelectedItems => FileViewType == FileViewType.Detail ? DataGrid.SelectedItems : ListBox.SelectedItems;

	/// <summary>
	/// 选择一个文件，参数为string文件名，不含路径
	/// </summary>
	public SimpleCommand SelectCommand { get; }

	/// <summary>
	/// 选择并重命名一个文件，参数为string文件名，不含路径
	/// </summary>
	public SimpleCommand StartRenameCommand { get; }

	public SimpleCommand SwitchViewCommand { get; }

	private readonly FileGridDataGridColumnsConverter columnsConverter;
	private readonly FileGridListBoxTemplateConverter listBoxTemplateConverter;

	private ContextMenu openedContextMenu;

	public FileGrid() {
		DataContextChanged += OnDataContextChanged;
		InitializeComponent();
		DataGrid.DataContext = ListBox.DataContext = this;
		EventManager.RegisterClassHandler(typeof(TextBox), GotFocusEvent, new RoutedEventHandler(OnRenameTextBoxGotFocus));
		SelectCommand = new SimpleCommand(Select);
		StartRenameCommand = new SimpleCommand(e => StartRename((string)e));
		SwitchViewCommand = new SimpleCommand(e => ViewModel?.OnSwitchView(e));
		columnsConverter = (FileGridDataGridColumnsConverter)FindResource("ColumnsConverter");
		listBoxTemplateConverter = (FileGridListBoxTemplateConverter)FindResource("ListBoxTemplateConverter");
		((FileGridListBoxTemplateConverter)FindResource("ListBoxTemplateConverter")).FileGrid = this;
		hoverTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Input, MouseHoverTimerWork, Dispatcher);
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
		if (e.NewValue is FileGridViewModel viewModel) {
			ViewModel = viewModel;
		}
	}

	private static void OnViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		((FileGrid)d).UpdateView();
	}

	/// <summary>
	/// 更新视图，包括<see cref="PathType"/>、<see cref="FileViewType"/>和<see cref="DetailLists"/>
	/// </summary>
	public void UpdateView() {
		if (FileViewType == FileViewType.Detail) {
			ItemsControl = DataGrid;
			columnsConverter.Convert(DataGrid.Columns, PathType, DetailLists);
			ListBox.ItemsSource = null;
			DataGrid.ItemsSource = ItemsSource;
			DataGrid.Visibility = Visibility.Visible;  // 就不用了binding了
			ListBox.Visibility = Visibility.Collapsed;
			var padding = DataGrid.Padding;
			ContentGrid.Margin = new Thickness(padding.Left, DataGrid.ColumnHeaderHeight + padding.Top, padding.Right, padding.Bottom);
		} else {
			ItemsControl = ListBox;
			DataGrid.ItemsSource = null;
			ListBox.ItemTemplate = listBoxTemplateConverter.Convert();
			ListBox.ItemsSource = ItemsSource;
			ListBox.Visibility = Visibility.Visible;
			DataGrid.Visibility = Visibility.Collapsed;
			ContentGrid.Margin = ListBox.Padding;
		}
	}

	public void Select(object fileName) {
		var item = ItemsSource.FirstOrDefault(item => item.Name == (string)fileName);
		if (item != null) {
			ScrollIntoView(item);
			item.IsSelected = true;
		}
	}

	public void StartRename(string fileName) {
		var item = ItemsSource.FirstOrDefault(item => item.Name == fileName);
		if (item == null) {
			if (ViewModel == null || (item = ViewModel.AddSingleItem(fileName)) == null) {
				return;
			}
		}
		ScrollIntoView(item);
		item.IsSelected = true;
		item.BeginRename();
	}

	private void OnRenameTextBoxGotFocus(object sender, RoutedEventArgs e) {
		var textBox = (TextBox)sender;
		if (textBox.DataContext is FileViewBaseItem item) {
			renamingItem = item;
			if (item.IsFolder) {
				textBox.SelectAll();
			} else {
				var lastIndexOfDot = textBox.Text.LastIndexOf('.');
				if (lastIndexOfDot == -1) {
					textBox.SelectAll();
				} else {
					textBox.Select(0, lastIndexOfDot);
				}
			}
			e.Handled = true;
		}
	}

	private bool rendered;

	protected override void OnRender(DrawingContext dc) {
		if (!rendered) {
			rendered = true;
			dataGridScrollViewer = (ScrollViewer)DataGrid.GetChild<Border>().Child;
			listBoxScrollViewer = (ScrollViewer)ListBox.GetChild<Border>().Child;
		}
		base.OnRender(dc);
	}

	/// <summary>
	/// 是否鼠标点击了，不加这个可能会从外部拖进来依旧是框选状态
	/// </summary>
	private bool isMouseDown;

	private bool isPreparedForRenaming;
	/// <summary>
	/// 鼠标按下如果在Row上，就是对应的项；如果不在，就是-1。每次鼠标抬起都重置为-1
	/// </summary>
	private int mouseDownRowIndex;
	/// <summary>
	/// <see cref="mouseDownRowIndex"/>重置为-1之前的值，主要用于shift多选
	/// </summary>
	private int lastMouseDownRowIndex;

	private Point startDragPosition;

	/// <summary>
	/// 是否正在框选
	/// </summary>
	private bool isRectSelecting;
	/// <summary>
	/// 是否正在拖放
	/// </summary>
	private bool isDragDropping;

	private Point startSelectionPoint;
	private DispatcherTimer timer;

	private FileViewBaseItem renamingItem;
	private CancellationTokenSource renameCts;

	private void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e) {
		if (e.OldStartingIndex == lastMouseDownRowIndex) {
			lastMouseDownRowIndex = -1;
		}
		if (e.OldStartingIndex == mouseDownRowIndex) {
			mouseDownRowIndex = -1;
		}
	}

	public void UnselectAll() {
		if (FileViewType == FileViewType.Detail) {
			DataGrid.UnselectAll();
		} else {
			ListBox.UnselectAll();
		}
	}

	/// <summary>
	/// 用于处理双击事件
	/// </summary>
	private bool isDoubleClicked;

	private FileViewBaseItem lastMouseUpItem;
	private Point lastMouseUpPoint;
	private DateTimeOffset lastMouseUpTime;

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
	///   没有点击在项目上，那就清空选择，同时记录坐标，为框选做准备
	/// </summary>
	/// <param name="e"></param>
	protected override void OnPreviewMouseDown(MouseButtonEventArgs e) {
		base.OnPreviewMouseDown(e);
		isDoubleClicked = false;
		if (e.OriginalSource is not ScrollViewer && e.OriginalSource.FindParent<ToggleBlock, FileGrid>() == null) {
			return;
		}

		if (e.ChangedButton is MouseButton.Left or MouseButton.Right) {
			isMouseDown = true;

			if (renameCts != null) {
				renameCts.Cancel();
				renameCts = null;
			}
			if (renamingItem is { EditingName: not null }) {
				if (e.OriginalSource.FindParent<TextBox, FileGrid>() == null) {
					renamingItem.StopRename();
				} else {
					return;
				}
			} else {
				renamingItem = null;
			}

			startDragPosition = e.GetPosition(ContentGrid);
			var item = MouseItem;
			if (item != null) {
				mouseDownRowIndex = ItemsSource.IndexOf(item);
				if (e.ChangedButton == MouseButton.Left) {
					if (item == lastMouseUpItem &&
						Math.Abs(startDragPosition.X - lastMouseUpPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
						Math.Abs(startDragPosition.Y - lastMouseUpPoint.Y) < SystemParameters.MinimumVerticalDragDistance &&
						DateTimeOffset.Now <= lastMouseUpTime.AddMilliseconds(Win32Interop.GetDoubleClickTime())) {
						isDoubleClicked = true;
						renameCts?.Cancel();
						renameCts = null;
						UnselectAll();
						RaiseEvent(new ItemClickEventArgs(ItemDoubleClickedEvent, item));
					} else {
						if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) {
							lastMouseDownRowIndex = mouseDownRowIndex;
							item.IsSelected = !item.IsSelected;
						} else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
							if (lastMouseDownRowIndex == -1) {
								lastMouseDownRowIndex = mouseDownRowIndex;
								item.IsSelected = true;
							} else {
								if (mouseDownRowIndex != lastMouseDownRowIndex) {
									int startIndex, endIndex;
									if (mouseDownRowIndex < lastMouseDownRowIndex) {
										startIndex = mouseDownRowIndex;
										endIndex = lastMouseDownRowIndex;
									} else {
										startIndex = lastMouseDownRowIndex;
										endIndex = mouseDownRowIndex;
									}
									UnselectAll();
									for (var i = startIndex; i <= endIndex; i++) {
										ItemsSource[i].IsSelected = true;
									}
								}
							}
						} else {
							lastMouseDownRowIndex = mouseDownRowIndex;
							var selectedItems = SelectedItems;
							if (selectedItems.Count == 0) {
								item.IsSelected = true;
							} else if (!item.IsSelected) {
								UnselectAll();
								item.IsSelected = true;
							} else if (selectedItems.Count == 1) {
								isPreparedForRenaming = true;
							}
						}
					}
				} else {
					lastMouseDownRowIndex = mouseDownRowIndex;
					var selectedItems = SelectedItems;
					if (selectedItems.Count == 0) {
						item.IsSelected = true;
					} else if (!item.IsSelected) {
						UnselectAll();
						item.IsSelected = true;
					}
				}
			} else {
				mouseDownRowIndex = -1;
			}
			var x = Math.Min(Math.Max(startDragPosition.X, 0), ContentGrid.ActualWidth);
			var y = Math.Min(Math.Max(startDragPosition.Y, 0), ContentGrid.ActualHeight);
			var scrollViewer = ViewScrollViewer;
			startSelectionPoint = new Point(x + scrollViewer.HorizontalOffset, y + scrollViewer.VerticalOffset);
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
			var mouseItem = ItemsControl.ContainerFromElement(ItemsControl, o) switch {
				ListBoxItem i => (FileViewBaseItem)i.Content,
				DataGridRow r => (FileViewBaseItem)r.Item,
				_ => null
			};
			if (MouseItem != mouseItem) {
				MouseItem = mouseItem;
				if (hoverTimer == null && (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))) {
					hoverShowTime = DateTimeOffset.Now.AddMilliseconds(500);
				}
			}
		} else {
			MouseItem = null;
		}
		base.OnPreviewMouseMove(e);
		if (!isMouseDown || isDoubleClicked || isDragDropping || renamingItem != null) {
			return;
		}
		// 只有isMouseDown（即OnPreviewMouseDown触发过）为true，这个才有用
		if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) {
			var point = e.GetPosition(ContentGrid);
			if (Math.Abs(point.X - startDragPosition.X) >= SystemParameters.MinimumHorizontalDragDistance ||
				Math.Abs(point.Y - startDragPosition.Y) >= SystemParameters.MinimumVerticalDragDistance) {
				if (mouseDownRowIndex != -1) {
					draggingItems = SelectedItems.Cast<FileViewBaseItem>().ToArray();
					var data = new DataObject(DataFormats.FileDrop, draggingItems.Select(item => item.FullPath).ToArray(), true);
					var allowedEffects = draggingItems.Any(item => item is DiskDrive) ? DragDropEffects.Link : DragDropEffects.Copy | DragDropEffects.Link | DragDropEffects.Move;
					DragFilesPreview = new DragFilesPreview(draggingItems.Select(item => item.Icon).ToArray());
					isDragDropping = true;
					dragDropWindow = DragDropWindow.Show(DragFilesPreview, new Point(50, 100), 0.8, false);
					DragDrop.DoDragDrop(this, data, allowedEffects);
					draggingItems = null;
					isDragDropping = false;
					dragDropWindow.Close();
					dragDropWindow = null;
					DragFilesPreview = null;
				} else {
					if (!isRectSelecting) {
						UnselectAll();
						if (lastTIndex <= lastBIndex) {
							var items = ItemsSource;
							for (var i = lastTIndex; i <= lastBIndex; i++) {
								items[i].IsSelected = false;
							}
							lastTIndex = ItemsSource.Count;
							lastBIndex = -1;
						}
						SelectionRect.Visibility = Visibility.Visible;
						Mouse.Capture(this);
						scrollSpeed = new Vector();
						timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Input, RectSelectScroll, Dispatcher);
						timer.Start();
						isRectSelecting = true;
					}
					UpdateRectSelection();

					if (point.X < 0) {
						scrollSpeed.X = point.X / 10d;
					} else if (point.Y > ActualWidth) {
						scrollSpeed.X = (point.X - ActualWidth) / 10d;
					} else {
						scrollSpeed.X = 0;
					}
					if (point.Y < 0) {
						scrollSpeed.Y = point.Y / 10d;
					} else if (point.Y > ActualHeight) {
						scrollSpeed.Y = (point.Y - ActualHeight) / 10d;
					} else {
						scrollSpeed.Y = 0;
					}
				}
			}
		}
		e.Handled = true;
	}

	private int lastLIndex, lastRIndex, lastTIndex, lastBIndex;

	/// <summary>
	/// 计算框选的元素
	/// </summary>
	private void UpdateRectSelection() {
		var scrollViewer = ViewScrollViewer;
		var point = Mouse.GetPosition(ContentGrid);
		var actualWidth = ContentGrid.ActualWidth;
		var x = Math.Min(Math.Max(point.X, 0), actualWidth) + scrollViewer.HorizontalOffset;
		var y = Math.Min(Math.Max(point.Y, 0), ContentGrid.ActualHeight) + scrollViewer.VerticalOffset;
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
		SelectionRect.Margin = new Thickness(l - scrollViewer.HorizontalOffset, t - scrollViewer.VerticalOffset, 0, 0);
		SelectionRect.Width = w;
		SelectionRect.Height = h;

		if (ItemsSource.Count > 0) {
			var items = ItemsSource;
			var itemWidth = ItemSize.Width;
			var itemHeight = ItemSize.Height;
			var dY = itemHeight + 4;  // 上下两项的y值之差，4是两项之间的间距，是固定的值
			if (itemWidth > 0) {
				var xCount = (int)(actualWidth / itemWidth);  // 横向能容纳多少个元素
				var yCount = (int)MathF.Ceiling((float)items.Count / xCount);  // 纵向有多少行
				var dX = (ContentGrid.ActualWidth - itemWidth * xCount) / (xCount + 2);  // 横向两项之间的间距，分散对齐，两边是有间距的
				var r = l + w;
				if (r < dX || l > actualWidth - dX) {
					return;
				}
				var lIndex = Math.Max((int)((l - dX) / (dX + itemWidth)), 0);
				var rIndex = Math.Min((int)(r / (dX + itemWidth)), xCount - 1);
				var tIndex = Math.Max((int)((t + 4) / dY), 0);
				var bIndex = Math.Min((int)((h + t) / dY), yCount - 1);
				Trace.WriteLine($"l: {lIndex} r: {rIndex} t: {tIndex} b: {bIndex}");
				return;
				var selectedCount = 0;
				if (lIndex != lastLIndex && lIndex < xCount) {
					for (var yy = lastTIndex; yy <= lastBIndex; yy++) {
						var i = yy * xCount;
						for (var xx = lastLIndex; xx <= lIndex; xx++) {
							var j = i + xx;
							if (j < items.Count) {
								items[j].IsSelected = false;
							}
						}
					}
					for (var yy = tIndex; yy <= bIndex; yy++) {
						var i = yy * xCount;
						for (var xx = lIndex; xx <= lastLIndex && yy <= rIndex; xx++) {
							var j = i + xx;
							if (j < items.Count) {
								items[j].IsSelected = true;
								selectedCount++;
							}
						}
					}
					lastLIndex = lIndex;
				}
				if (rIndex != lastRIndex && rIndex >= 0) {
					for (var yy = lastTIndex; yy <= lastBIndex; yy++) {
						var i = yy * xCount;
						for (var xx = rIndex + 1; yy <= lastRIndex; xx++) {
							var j = i + xx;
							if (j < items.Count) {
								items[j].IsSelected = false;
							}
						}
					}
					for (var yy = tIndex; yy <= bIndex; yy++) {
						var i = yy * xCount;
						for (var xx = Math.Max(lastRIndex + 1, lIndex); xx <= rIndex; xx++) {
							var j = i + xx;
							if (j < items.Count) {
								items[j].IsSelected = true;
								selectedCount++;
							}
						}
					}
					lastRIndex = rIndex;
				}
				if (tIndex != lastTIndex && tIndex < yCount) {
					for (var yy = lastTIndex; yy <= tIndex; yy++) {
						var i = yy * xCount;
						for (var xx = lastLIndex; xx <= lastRIndex; xx++) {
							var j = i + xx;
							if (j < items.Count) {
								items[j].IsSelected = false;
							}
						}
					}
					for (var yy = tIndex; yy <= lastTIndex && yy <= bIndex; yy++) {
						var i = yy * xCount;
						for (var xx = lIndex; xx <= rIndex; xx++) {
							var j = i + xx;
							if (j < items.Count) {
								items[j].IsSelected = true;
								selectedCount++;
							}
						}
					}
					lastTIndex = tIndex;
				}
				if (bIndex != lastBIndex && bIndex >= 0) {
					for (var yy = bIndex + 1; yy <= lastBIndex; yy++) {
						var i = yy * xCount;
						for (var xx = lastLIndex; xx <= lastRIndex; xx++) {
							var j = i + xx;
							if (j < items.Count) {
								items[j].IsSelected = false;
							}
						}
					}
					for (var yy = Math.Max(lastBIndex + 1, tIndex); yy <= bIndex; yy++) {
						var i = yy * xCount;
						for (var xx = lIndex; xx <= rIndex; xx++) {
							var j = i + xx;
							if (j < items.Count) {
								items[j].IsSelected = true;
								selectedCount++;
							}
						}
					}
					lastBIndex = bIndex;
				}
				if (selectedCount == 0 && lastLIndex <= lastRIndex && lastTIndex <= lastBIndex) {
					for (var yy = lastTIndex; yy <= lastBIndex; yy++) {
						var i = yy * xCount;
						for (var xx = lastLIndex; xx <= lastRIndex; xx++) {
							var j = i + xx;
							if (j < items.Count) {
								items[j].IsSelected = false;
							}
						}
					}
					lastLIndex = xCount;
					lastRIndex = -1;
					lastTIndex = yCount;
					lastBIndex = -1;
				}
			} else {  // 只计算纵向
				var firstIndex = (int)(scrollViewer.VerticalOffset / dY);  // 视图中第一个元素的index，因为使用了虚拟化容器，所以不能找Items[0]，它可能不存在
				var row0 = FileViewType == FileViewType.Detail ? (UIElement)DataGrid.ItemContainerGenerator.ContainerFromIndex(firstIndex) : (UIElement)ListBox.ItemContainerGenerator.ContainerFromIndex(firstIndex);
				if (row0 == null) {
					return;
				}
				// l = 0, t = 0时，正好是第一个元素左上的坐标
				if (l < row0.DesiredSize.Width) {  // 框的左边界在列内。每列Width都是一样的
					var tIndex = Math.Max((int)((t + 4) / dY), 0);
					var bIndex = Math.Min((int)((h + t) / dY), items.Count - 1);
					if (tIndex > bIndex) {
						UnselectAll();
					} else {
						if (tIndex != lastTIndex && tIndex < items.Count) {
							for (var i = lastTIndex; i < tIndex; i++) {
								items[i].IsSelected = false;
							}
							for (var i = tIndex; i <= lastTIndex && i <= bIndex; i++) {
								items[i].IsSelected = true;
							}
							lastTIndex = tIndex;
						}
						if (bIndex != lastBIndex) {
							for (var i = bIndex + 1; i <= lastBIndex; i++) {
								items[i].IsSelected = false;
							}
							for (var i = Math.Max(lastBIndex + 1, tIndex); i <= bIndex; i++) {
								items[i].IsSelected = true;
							}
							lastBIndex = bIndex;
						}
					}
				} else if (lastTIndex <= lastBIndex) {
					for (var i = lastTIndex; i <= lastBIndex; i++) {
						items[i].IsSelected = false;
					}
					lastTIndex = items.Count;
					lastBIndex = -1;
				}
			}
		}
	}

	protected override void OnPreviewMouseUp(MouseButtonEventArgs e) {
		base.OnPreviewMouseUp(e);
		if (!isMouseDown || isDoubleClicked || renamingItem != null) {
			return;
		}
		// 只有isMouseDown（即OnPreviewMouseDown触发过）为true，这个才有用
		if (isMouseDown && e.ChangedButton is MouseButton.Left or MouseButton.Right) {
			isMouseDown = false;
			var isClickOnItem = mouseDownRowIndex >= 0 && mouseDownRowIndex < ItemsSource.Count;
			if (isRectSelecting) {
				isRectSelecting = false;
				SelectionRect.Visibility = Visibility.Collapsed;
				Mouse.Capture(null);
				timer?.Stop();
			} else if (isPreparedForRenaming) {
				isPreparedForRenaming = false;
				if (mouseDownRowIndex >= 0 && mouseDownRowIndex < ItemsSource.Count && DateTimeOffset.Now > lastMouseUpTime.AddMilliseconds(Win32Interop.GetDoubleClickTime() * 1.5)) {
					var item = ItemsSource[mouseDownRowIndex];
					if (item != renamingItem) {  // 上次命名的这次点击就不命名了
						if (renameCts == null) {
							var cts = renameCts = new CancellationTokenSource();
							Task.Run(() => {
								Thread.Sleep(Win32Interop.GetDoubleClickTime());
								if (!cts.IsCancellationRequested) {
									item.BeginRename();
								}
							}, renameCts.Token);
						} else {
							renameCts = null;
						}
					} else {
						renamingItem = null;
					}
				}
			} else {
				var isCtrlOrShiftPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ||
										   Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
				switch (e.ChangedButton) {
				case MouseButton.Left:
					if (isClickOnItem) {
						var item = ItemsSource[mouseDownRowIndex];
						if (!isCtrlOrShiftPressed && SelectedItems.Count > 1) {
							UnselectAll();
						}
						item.IsSelected = true;
					} else if (!isCtrlOrShiftPressed) {
						UnselectAll();
					}
					break;
				case MouseButton.Right:
					if (isClickOnItem && e.OriginalSource is DependencyObject o) {
						var item = ItemsSource[mouseDownRowIndex];
						openedContextMenu = ((FrameworkElement)ItemsControl.ContainerFromElement(ItemsControl, o))!.ContextMenu!;
						openedContextMenu.DataContext = item;
						openedContextMenu.IsOpen = true;
					} else if (Folder != null) {
						openedContextMenu = ContextMenu;
						openedContextMenu!.IsOpen = true;
					}
					break;
				}
			}
			if (isClickOnItem) {
				lastMouseUpItem = ItemsSource[mouseDownRowIndex];
				lastMouseUpTime = DateTimeOffset.Now;
				lastMouseUpPoint = e.GetPosition(ContentGrid);
			} else {
				lastMouseUpItem = null;
			}
		}
		mouseDownRowIndex = -1;
		e.Handled = true;
	}

	protected override void OnPreviewMouseWheel(MouseWheelEventArgs e) {
		if (previewPopup is { IsOpen: true }) {
			previewPopup.HandleMouseScroll(e);
			e.Handled = true;
		} else {
			base.OnPreviewMouseWheel(e);
		}
	}

	private static DispatcherTimer hoverTimer;
	private static DateTimeOffset hoverShowTime;
	private static PreviewPopup previewPopup;

	private void MouseHoverTimerWork(object s, EventArgs e) {
		var item = MouseItem;
		// 如果鼠标处没有项目或者没有按下Alt
		if (item == null || (Keyboard.IsKeyUp(Key.LeftAlt) && Keyboard.IsKeyUp(Key.RightAlt))) {
			if (previewPopup != null) {  // 如果popup是打开状态，那就关闭
				if (previewPopup.IsOpen) {
					previewPopup.Close();
				}
				previewPopup = null;
			}
		} else if (hoverShowTime < DateTimeOffset.Now && previewPopup == null || previewPopup.FilePath != item.FullPath) {  // 有项目且按下了Alt
			var newPopup = PreviewPopup.ChoosePopup(item.FullPath);
			if (newPopup != null) {
				if (newPopup != previewPopup) {
					previewPopup?.Close();
				}
				previewPopup = newPopup;
				previewPopup.PlacementTarget = this;
				var mousePos = Mouse.GetPosition(this);
				previewPopup.HorizontalOffset = mousePos.X + 20d;
				previewPopup.VerticalOffset = mousePos.Y + 20d;
				previewPopup.Load(item.FullPath);
			}
		}
	}

	public void ScrollIntoView(FileViewBaseItem item) {
		if (!isRectSelecting && !isDragDropping) {
			if (FileViewType == FileViewType.Detail) {
				DataGrid.ScrollIntoView(item);
			} else {
				ListBox.ScrollIntoView(item);
			}
		}
	}

	private void RectSelectScroll(object sender, EventArgs e) {
		var scrollViewer = ViewScrollViewer;
		scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollSpeed.X);
		scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollSpeed.Y);
		UpdateRectSelection();
	}

	protected override void OnDragEnter(DragEventArgs e) {
		isDragDropping = true;
		if (PathType == PathType.Home && TabItem.DraggingTab == null) {
			e.Effects = DragDropEffects.None;
			e.Handled = true;
			return;
		}
		if (DragFilesPreview != null) {
			DragFilesPreview.Destination = FullPath;
		}
	}

	protected override void OnDragLeave(DragEventArgs e) {
		isDragDropping = false;
		if (DragFilesPreview != null) {
			DragFilesPreview.Destination = null;
		}
		if (lastDragOnItem != null) {
			lastDragOnItem.IsSelected = false;
			lastDragOnItem = null;
		}
	}

	/// <summary>
	/// 上一个拖放到的item
	/// </summary>
	private FileViewBaseItem lastDragOnItem;

	protected override void OnDragOver(DragEventArgs e) {
		isDragDropping = true;
		if (TabItem.DraggingTab == null) {
			e.Effects = DragDropEffects.None;
			e.Handled = true;
		} else {
			var item = MouseItem;
			string path;
			if (item != null) {
				path = item.FullPath;
			} else {
				if (PathType == PathType.Home) {
					e.Effects = DragDropEffects.None;
					e.Handled = true;
					return;
				}
				path = FullPath;
			}
			var contains = draggingItems?.Any(item => item.FullPath == path) ?? false;
			if (lastDragOnItem != item) {
				if (lastDragOnItem != null) {
					lastDragOnItem.IsSelected = false;
				}
				if (item != null && !contains) {
					lastDragOnItem = item;
					item.IsSelected = true;  // 让拖放到的item高亮
				}
			}
			if (DragFilesPreview != null) {
				if (item != null && contains) {  // 自己不能往自己身上拖放
					e.Effects = DragDropEffects.None;
					e.Handled = true;
					return;
				}
				DragFilesPreview.Destination = path;
			}
			if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } fileList) {
				if (Path.GetDirectoryName(fileList[0]) == path) {  // 相同文件夹禁止移动
					e.Effects = DragDropEffects.None;
					e.Handled = true;
				}
			}
		}
	}

	protected override void OnDrop(DragEventArgs e) {
		string path;
		// 拖动文件到了项目上
		var item = MouseItem;
		if (item != null) {
			path = item.FullPath;
		} else {
			path = null;
		}
		RaiseEvent(new FileDropEventArgs(FileDropEvent, e, path));
	}

	protected override void OnPreviewGiveFeedback(GiveFeedbackEventArgs e) {
		if (e.Effects == DragDropEffects.None) {
			Mouse.SetCursor(Cursors.No);
			e.UseDefaultCursors = false;
			DragFilesPreview.Destination = null;
			e.Handled = true;
		} else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
			if (e.Effects.HasFlag(DragDropEffects.Move)) {
				DragFilesPreview.DragDropEffect = DragDropEffects.Move;
			}
		} else if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) {
			if (e.Effects.HasFlag(DragDropEffects.Copy)) {
				DragFilesPreview.DragDropEffect = DragDropEffects.Copy;
			}
		} else if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) {
			if (e.Effects.HasFlag(DragDropEffects.Link)) {
				DragFilesPreview.DragDropEffect = DragDropEffects.Link;
			}
		} else {
			DragFilesPreview.DragDropEffect = e.Effects.GetFirstEffect();
		}
		dragDropWindow.MoveWithCursor();
	}

	protected override void OnLostFocus(RoutedEventArgs e) {
		renamingItem?.StopRename();
		base.OnLostFocus(e);
	}

	public class ItemClickEventArgs : RoutedEventArgs {
		public FileViewBaseItem Item { get; }

		public ItemClickEventArgs(RoutedEvent e, FileViewBaseItem item) {
			RoutedEvent = e;
			Item = item;
		}
	}

	#region MenuItem点击事件，用Binding的话太浪费资源了
	private void Cut_OnClick(object sender, RoutedEventArgs e) {
		ViewModel?.Copy(true);
		if (openedContextMenu != null) {
			openedContextMenu.IsOpen = false;
		}
	}

	private void Copy_OnClick(object sender, RoutedEventArgs e) {
		ViewModel?.Copy(false);
		if (openedContextMenu != null) {
			openedContextMenu.IsOpen = false;
		}
	}

	private void Rename_OnClick(object sender, RoutedEventArgs e) {
		ViewModel?.Rename();
		if (openedContextMenu != null) {
			openedContextMenu.IsOpen = false;
		}
	}

	private void Delete_OnClick(object sender, RoutedEventArgs e) {
		ViewModel?.Delete();
		if (openedContextMenu != null) {
			openedContextMenu.IsOpen = false;
		}
	}

	private async void Open_OnClick(object sender, RoutedEventArgs e) {
		if (ViewModel == null) {
			return;
		}
		foreach (var item in ViewModel.SelectedItems) {
			await item.OpenAsync();
		}
	}

	private async void OpenAsAdmin_OnClick(object sender, RoutedEventArgs e) {
		if (ViewModel == null) {
			return;
		}
		foreach (var item in ViewModel.SelectedItems) {
			await item.OpenAsync(true);
		}
	}

	private async void OpenInNewTab_OnClick(object sender, RoutedEventArgs e) {
		if (ViewModel == null) {
			return;
		}
		foreach (var item in ViewModel.SelectedItems.Where(i => i.IsFolder)) {
			await item.OpenAsync();
		}
	}

	private void Refresh_OnClick(object sender, RoutedEventArgs e) {
		ViewModel?.Refresh();
	}

	private void NewFolder_OnClick(object sender, RoutedEventArgs e) {
		string folderName;
		try {
			folderName = FileUtils.GetNewFileName(FullPath, "New_folder".L());
			Directory.CreateDirectory(Path.Combine(FullPath, folderName));
		} catch (Exception ex) {
			Logger.Error(ex.Message);
			return;
		}
		StartRename(folderName);
	}

	private void OpenInNewWindow_OnClick(object sender, RoutedEventArgs e) {
		if (ViewModel == null) {
			return;
		}
		foreach (var item in ViewModel.SelectedItems.Where(i => i.IsFolder)) {
			new MainWindow(item.FullPath).Show();
		}
	}

	private void AddToBookmarks_OnClick(object sender, RoutedEventArgs e) {
		if (ViewModel == null || ViewModel.SelectedItems.Count == 0) {
			return;
		}
		FileTabControl.FocusedTabControl.MainWindow.AddToBookmark(ViewModel.SelectedItems.Select(i => i.FullPath).ToArray());
	}

	private void RemoveFromBookmarks_OnClick(object sender, RoutedEventArgs e) {
		if (ViewModel == null || ViewModel.SelectedItems.Count == 0) {
			return;
		}
		MainWindow.RemoveFromBookmark(ViewModel.SelectedItems.Select(i => i.FullPath).ToArray());
	}

	private void ShowProperties_OnClick(object sender, RoutedEventArgs e) {
		if (ViewModel == null) {
			return;
		}
		var selectedItems = ViewModel.SelectedItems;
		if (selectedItems.Count == 0) {
			Win32Interop.ShowFileProperties(FullPath);
		} else {
			foreach (var item in ViewModel.SelectedItems.Where(i => i.IsFolder)) {
				Win32Interop.ShowFileProperties(item.FullPath);
			}
		}
	}
	#endregion
}

public class FileDropEventArgs : RoutedEventArgs {
	public DragEventArgs DragEventArgs { get; }
	public DataObjectContent Content { get; }
	/// <summary>
	/// 拖动到的Path，可能是文件夹或者文件，为null表示当前路径
	/// </summary>
	public string Path { get; }

	public FileDropEventArgs(RoutedEvent e, DragEventArgs args, string path) {
		RoutedEvent = e;
		DragEventArgs = args;
		Content = DataObjectContent.Parse(args.Data);
		Path = path;
	}
}