using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ExplorerEx.Converter;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using ExplorerEx.Win32;
using HandyControl.Tools;
using TextBox = HandyControl.Controls.TextBox;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 要能够响应鼠标事件，处理点选、框选、拖放、重命名和双击
/// </summary>
public partial class FileDataGrid {
	public enum ViewTypes {
		/// <summary>
		/// 图标，表现为WarpPanel，每个小格上边是缩略图下边是文件名
		/// </summary>
		Icon,
		/// <summary>
		/// 列表，表现为WarpPanel，每个小格左边是缩略图右边是文件名
		/// </summary>
		List,
		/// <summary>
		/// 详细信息，表现为DataGrid，上面有Header，一列一列的
		/// </summary>
		Detail,
		/// <summary>
		/// 平铺，表现为WarpPanel，每个小格左边是缩略图右边从上到下依次是文件名、文件类型描述、文件大小
		/// </summary>
		Tile,
		/// <summary>
		/// 内容，表现为DataGrid，但Header不可见，左边是图标、文件名、大小，右边是详细信息
		/// </summary>
		Content
	}

	/// <summary>
	/// 选择详细信息中的显示列
	/// </summary>
	[Flags]
	public enum Lists {
		// Name必选，忽略

		/// <summary>
		/// 修改日期
		/// </summary>
		ModificationDate = 0b1,
		/// <summary>
		/// 类型
		/// </summary>
		Type = 0b10,
		/// <summary>
		/// 文件大小
		/// </summary>
		FileSize = 0b100,
		/// <summary>
		/// 创建日期
		/// </summary>
		CreationDate = 0b1000,
	}

	/// <summary>
	/// 当前路径的类型
	/// </summary>
	public enum PathTypes {
		/// <summary>
		/// 首页，“此电脑”
		/// </summary>
		Home,
		/// <summary>
		/// 普通，即浏览正常文件
		/// </summary>
		Normal,
		/// <summary>
		/// 网络驱动器
		/// </summary>
		NetworkDisk
	}

	private ScrollViewer scrollViewer;
	private Grid contentGrid;
	private Border selectionRect;

	/// <summary>
	/// 自带的Items索引方法比较复杂，使用这个
	/// </summary>
	public new ObservableCollection<FileViewBaseItem> Items => (ObservableCollection<FileViewBaseItem>)ItemsSource;

	public delegate void FileDropEventHandler(object sender, FileDropEventArgs e);

	public static readonly RoutedEvent FileDropEvent = EventManager.RegisterRoutedEvent(
		"FileDrop", RoutingStrategy.Bubble, typeof(FileDropEventHandler), typeof(FileDataGrid));

	public event FileDropEventHandler FileDrop {
		add => AddHandler(FileDropEvent, value);
		remove => RemoveHandler(FileDropEvent, value);
	}

	public delegate void ItemClickEventHandler(object sender, ItemClickEventArgs e);

	public static readonly RoutedEvent ItemClickedEvent = EventManager.RegisterRoutedEvent(
		"ItemClicked", RoutingStrategy.Bubble, typeof(ItemClickEventHandler), typeof(FileDataGrid));

	public event ItemClickEventHandler ItemClicked {
		add => AddHandler(ItemClickedEvent, value);
		remove => RemoveHandler(ItemClickedEvent, value);
	}

	public static readonly RoutedEvent ItemDoubleClickedEvent = EventManager.RegisterRoutedEvent(
		"ItemDoubleClicked", RoutingStrategy.Bubble, typeof(ItemClickEventHandler), typeof(FileDataGrid));

	public event ItemClickEventHandler ItemDoubleClicked {
		add => AddHandler(ItemDoubleClickedEvent, value);
		remove => RemoveHandler(ItemDoubleClickedEvent, value);
	}

	public static readonly DependencyProperty ViewTypeProperty = DependencyProperty.Register(
		"ViewType", typeof(ViewTypes), typeof(FileDataGrid), new PropertyMetadata(ViewTypes.Tile, ViewType_OnChanged));

	private static void ViewType_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if (e.NewValue != e.OldValue) {
			((FileDataGrid)d).UpdateColumns();
		}
	}

	public ViewTypes ViewType {
		get => (ViewTypes)GetValue(ViewTypeProperty);
		set => SetValue(ViewTypeProperty, value);
	}

	public static readonly DependencyProperty PathTypeProperty = DependencyProperty.Register(
		"PathType", typeof(PathTypes), typeof(FileDataGrid), new PropertyMetadata(PathTypes.Home));

	public PathTypes PathType {
		get => (PathTypes)GetValue(PathTypeProperty);
		set => SetValue(PathTypeProperty, value);
	}

	public static readonly DependencyProperty ItemSizeProperty = DependencyProperty.Register(
		"ItemSize", typeof(Size), typeof(FileDataGrid), new PropertyMetadata(new Size(0d, 70d)));

	/// <summary>
	/// 项目大小
	/// </summary>
	public Size ItemSize {
		get => (Size)GetValue(ItemSizeProperty);
		set => SetValue(ItemSizeProperty, value);
	}

	public Lists DetailLists {
		get => detailLists;
		set {
			if (detailLists != value) {
				detailLists = value;
				UpdateColumns();
			}
		}
	}

	private Lists detailLists = Lists.ModificationDate | Lists.Type | Lists.FileSize;

	private readonly FileDataGridColumnsConverter columnsConverter;

	public FileDataGrid() {
		InitializeComponent();
		EventManager.RegisterClassHandler(typeof(TextBox), GotFocusEvent, new RoutedEventHandler(OnRenameTextBoxGotFocus));
		columnsConverter = (FileDataGridColumnsConverter)FindResource("ColumnsConverter");
		UpdateColumns();
	}

	/// <summary>
	/// 根据<see cref="PathType"/>、<see cref="ViewType"/>和<see cref="detailLists"/>选择列，同时更新<see cref="DataGrid.ColumnHeaderHeight"/>
	/// </summary>
	private void UpdateColumns() {
		columnsConverter.Convert(Columns, PathType, ViewType, detailLists);
		if (ViewType == ViewTypes.Detail) {
			ColumnHeaderHeight = 20d;
		} else {
			ColumnHeaderHeight = 0d;
		}
	}

	private void OnRenameTextBoxGotFocus(object sender, RoutedEventArgs e) {
		var textBox = (TextBox)sender;
		if (textBox.DataContext is FileViewBaseItem item) {
			renamingItem = item;
			var lastIndexOfDot = textBox.Text.LastIndexOf('.');
			if (lastIndexOfDot == -1) {
				textBox.SelectAll();
			} else {
				textBox.Select(0, lastIndexOfDot);
			}
			e.Handled = true;
		}
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		scrollViewer = (ScrollViewer)GetTemplateChild("DG_ScrollViewer");
		contentGrid = (Grid)GetTemplateChild("ContentGrid");
		selectionRect = (Border)GetTemplateChild("SelectionRect");
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

	protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e) {
		if (e.OldStartingIndex == lastMouseDownRowIndex) {
			lastMouseDownRowIndex = -1;
		}
		if (e.OldStartingIndex == mouseDownRowIndex) {
			mouseDownRowIndex = -1;
		}
		base.OnItemsChanged(e);
	}

	/// <summary>
	/// 用于处理双击事件
	/// </summary>
	private FileViewBaseItem lastClickItem;
	private DateTimeOffset lastClickTime;
	private bool isDoubleClicked;

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
		Trace.WriteLine("MouseDown");
		isDoubleClicked = false;
		if (e.OriginalSource.FindParent<ScrollBar, FileDataGrid>() != null) {
			base.OnPreviewMouseDown(e);
			return;
		}

		if (e.ChangedButton is MouseButton.Left or MouseButton.Right) {
			isMouseDown = true;

			if (renameCts != null) {
				renameCts.Cancel();
				renameCts = null;
			}
			if (renamingItem is { EditingName: not null }) {
				if (e.OriginalSource.FindParent<TextBox, FileDataGrid>() == null) {
					renamingItem.StopRename();
				}
			}

			if (ContainerFromElement(this, (DependencyObject)e.OriginalSource) is DataGridRow row) {
				var item = (FileViewBaseItem)row.Item;
				// 双击
				if (item == lastClickItem && DateTimeOffset.Now <= lastClickTime.AddMilliseconds(Win32Interop.GetDoubleClickTime())) {
					isDoubleClicked = true;
					renameCts?.Cancel();
					renameCts = null;
					UnselectAll();
					RaiseEvent(new ItemClickEventArgs(ItemDoubleClickedEvent, item));
				} else {
					mouseDownRowIndex = Items.IndexOf(item);
					if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) {
						lastMouseDownRowIndex = mouseDownRowIndex;
						row.IsSelected = !row.IsSelected;
					} else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
						if (lastMouseDownRowIndex == -1) {
							lastMouseDownRowIndex = mouseDownRowIndex;
							row.IsSelected = true;
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
									Items[i].IsSelected = true;
								}
							}
						}
					} else {
						lastMouseDownRowIndex = mouseDownRowIndex;
						var selectedItems = SelectedItems;
						if (selectedItems.Count == 0) {
							row.IsSelected = true;
						} else if (!row.IsSelected) {
							UnselectAll();
							row.IsSelected = true;
						} else if (selectedItems.Count == 1 && e.ChangedButton == MouseButton.Left) {
							isPreparedForRenaming = true;
							Trace.WriteLine("isPreparedForRenaming");
						}
					}
				}
				lastClickItem = item;
				lastClickTime = DateTimeOffset.Now;
			} else {
				mouseDownRowIndex = -1;
				startDragPosition = e.GetPosition(contentGrid);
				var x = Math.Min(Math.Max(startDragPosition.X, 0), contentGrid.ActualWidth);
				var y = Math.Min(Math.Max(startDragPosition.Y, 0), contentGrid.ActualHeight);
				startSelectionPoint = new Point(x + scrollViewer.HorizontalOffset, y + scrollViewer.VerticalOffset);
			}
		}
		e.Handled = true;
	}

	/// <summary>
	/// 框选或者拖放时，自动滚动的速度
	/// </summary>
	private Vector scrollSpeed;

	/// <summary>
	/// 鼠标移动时，分为以下几种情况
	///    如果鼠标点击在项目上，那就进行拖放，如果不在项目上，那就进行框选
	/// </summary>
	/// <param name="e"></param>
	protected override void OnPreviewMouseMove(MouseEventArgs e) {
		if (e.OriginalSource.FindParent<ScrollBar, FileDataGrid>() != null || isDoubleClicked) {
			base.OnPreviewMouseMove(e);
			return;
		}
		// 只有isMouseDown（即OnPreviewMouseDown触发过）为true，这个才有用
		if (isMouseDown && !isDragDropping && e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) {
			var point = e.GetPosition(contentGrid);
			if (Math.Abs(point.X - startDragPosition.X) > SystemParameters.MinimumHorizontalDragDistance ||
				Math.Abs(point.Y - startDragPosition.Y) > SystemParameters.MinimumVerticalDragDistance) {
				if (mouseDownRowIndex != -1) {
					var data = new DataObject(DataFormats.FileDrop, SelectedItems.Cast<FileViewBaseItem>().Select(item => item.FullPath).ToArray(), true);
					isDragDropping = true;
					DragDrop.DoDragDrop(this, data, DragDropEffects.Copy | DragDropEffects.Link | DragDropEffects.Move);
				} else {
					if (!isRectSelecting) {
						UnselectAll();
						if (lastStartIndex <= lastEndIndex) {
							var items = Items;
							for (var i = lastStartIndex; i <= lastEndIndex; i++) {
								items[i].IsSelected = false;
							}
							lastStartIndex = Items.Count;
							lastEndIndex = -1;
						}
						selectionRect.Visibility = Visibility.Visible;
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

	private int lastStartIndex, lastEndIndex;

	private void UpdateRectSelection() {
		var point = Mouse.GetPosition(contentGrid);
		var x = Math.Min(Math.Max(point.X, 0), contentGrid.ActualWidth) + scrollViewer.HorizontalOffset;
		var y = Math.Min(Math.Max(point.Y, 0), contentGrid.ActualHeight) + scrollViewer.VerticalOffset;
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
		selectionRect.Margin = new Thickness(l - scrollViewer.HorizontalOffset, t - scrollViewer.VerticalOffset, 0, 0);
		selectionRect.Width = w;
		selectionRect.Height = h;

		if (Items.Count > 0) {
			var point0 = new Point(Padding.Left, Padding.Top + RowHeight - 36);  // 第一项左上角的坐标
			var dY = RowHeight + 4;
			var firstIndex = (int)(scrollViewer.VerticalOffset / dY);
			var row0 = (DataGridRow)ItemContainerGenerator.ContainerFromIndex(firstIndex);
			// 0: 5-15 1: 20-30 2: 35-45
			// dY: 15  ActualHeight: 10  point0.Y: 5
			// 设 t = 14, h = 10
			// 选区为0-1
			// 设 t = 16, h = 10
			// 选区为1-1
			if (l < point0.X + row0.DesiredSize.Width) {  // 框的左边界在列内。每列Width都是一样的
				var startIndex = Math.Max((int)((t - point0.Y + 4) / dY), 0);
				var items = Items;
				var endIndex = Math.Min((int)((h + t - point0.Y) / dY), items.Count - 1);
				if (startIndex != lastStartIndex && startIndex < items.Count) {
					for (var i = lastStartIndex; i < startIndex; i++) {
						if (i < items.Count) {
							items[i].IsSelected = false;
						}
					}
					for (var i = startIndex; i <= lastStartIndex && i <= endIndex; i++) {
						items[i].IsSelected = true;
					}
					// Trace.WriteLine($"S: {startIndex} Ls: {lastStartIndex}");
					lastStartIndex = startIndex;
				}
				if (endIndex != lastEndIndex && endIndex >= 0) {
					for (var i = endIndex + 1; i <= lastEndIndex; i++) {
						items[i].IsSelected = false;
					}
					for (var i = Math.Max(lastEndIndex + 1, startIndex); i <= endIndex; i++) {
						items[i].IsSelected = true;
					}
					// Trace.WriteLine($"E: {endIndex} Le: {lastEndIndex}");
					lastEndIndex = endIndex;
				}
			} else if (lastStartIndex <= lastEndIndex) {
				var items = (ObservableCollection<FileViewBaseItem>)ItemsSource;  // 这里不用Items索引，我看过源代码，复杂度比较高
				for (var i = lastStartIndex; i <= lastEndIndex; i++) {
					items[i].IsSelected = false;
				}
				lastStartIndex = Items.Count;
				lastEndIndex = -1;
			}
		}
	}

	protected override void OnPreviewMouseUp(MouseButtonEventArgs e) {
		Trace.WriteLine("MouseUp");
		if (e.OriginalSource.FindParent<ScrollBar, FileDataGrid>() != null || isDoubleClicked) {
			base.OnPreviewMouseUp(e);
			return;
		}
		// 只有isMouseDown（即OnPreviewMouseDown触发过）为true，这个才有用
		if (isMouseDown && e.ChangedButton is MouseButton.Left or MouseButton.Right) {
			isMouseDown = false;
			if (isRectSelecting) {
				isRectSelecting = false;
				selectionRect.Visibility = Visibility.Collapsed;
				Mouse.Capture(null);
				timer?.Stop();
			} else if (isPreparedForRenaming) {
				isPreparedForRenaming = false;
				if (mouseDownRowIndex >= 0 && mouseDownRowIndex < Items.Count) {  // 防止集合改变过
					var item = Items[mouseDownRowIndex];
					if (renameCts == null) {
						var cts = renameCts = new CancellationTokenSource();
						Task.Run(() => {
							Thread.Sleep((int)(Win32Interop.GetDoubleClickTime() * 1.5f));  // 要比双击的时间长一些
							if (!cts.IsCancellationRequested) {
								item.BeginRename();
							}
						}, renameCts.Token);
					} else {
						renameCts = null;
					}
				}
			} else {
				var isClickOnItem = mouseDownRowIndex >= 0 && mouseDownRowIndex < Items.Count;
				var isCtrlOrShiftPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ||
										   Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
				switch (e.ChangedButton) {
				case MouseButton.Left:
					if (isClickOnItem) {
						var item = Items[mouseDownRowIndex];
						if (!isCtrlOrShiftPressed && SelectedItems.Count > 1) {
							UnselectAll();
						}
						item.IsSelected = true;
						RaiseEvent(new ItemClickEventArgs(ItemClickedEvent, item));
					} else if (!isCtrlOrShiftPressed) {
						UnselectAll();
					}
					break;
				case MouseButton.Right:
					if (isClickOnItem) {
						var item = Items[mouseDownRowIndex];
						var menu = ((DataGridRow)ContainerFromElement(this, (DependencyObject)e.OriginalSource))!.ContextMenu!;
						menu.DataContext = item;
						menu.IsOpen = true;
					} else {
						// ContextMenu!.IsOpen = true;
					}
					break;
				}
			}
		}
		mouseDownRowIndex = -1;
		e.Handled = true;
	}

	/// <summary>
	/// 双击的时候，这个方法会接管<see cref="OnPreviewMouseUp"/>
	/// </summary>
	/// <param name="e"></param>
	protected override void OnPreviewMouseDoubleClick(MouseButtonEventArgs e) {
		Trace.WriteLine("MouseDoubleClick");
		if (mouseDownRowIndex >= 0 && mouseDownRowIndex < Items.Count && e.ChangedButton == MouseButton.Left) {
			renameCts?.Cancel();
			renameCts = null;
			RaiseEvent(new ItemClickEventArgs(ItemDoubleClickedEvent, Items[mouseDownRowIndex]));
		}
		e.Handled = true;
	}

	private void RectSelectScroll(object sender, EventArgs e) {
		scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollSpeed.X);
		scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollSpeed.Y);
		UpdateRectSelection();
	}

	protected override void OnDragEnter(DragEventArgs e) {
		isDragDropping = true;
		base.OnDragEnter(e);
	}

	protected override void OnDragLeave(DragEventArgs e) {
		isDragDropping = false;
		base.OnDragLeave(e);
	}

	protected override void OnPreviewDrop(DragEventArgs e) {
		isDragDropping = false;
		base.OnPreviewDrop(e);
	}

	private void OnDragOver(object sender, DragEventArgs e) {
		e.Effects = DragDropEffects.Copy;
		//var data = new DataObjectContent(e.Data);
		//bool isDirectory;
		//if (ContainerFromElement(this, (DependencyObject)e.OriginalSource) is DataGridRow row) {
		//	var item = row.Item;
		//	if (item.IsDirectory) {
		//	
		//	}
		//} else {
		//
		//}
	}

	protected override void OnDrop(DragEventArgs e) {
		string path;
		// 拖动文件到了项目上
		if (ContainerFromElement(this, (DependencyObject)e.OriginalSource) is DataGridRow row) {
			var item = (FileViewBaseItem)row.Item;
			path = item.FullPath;
		} else {
			path = null;
		}
		RaiseEvent(new FileDropEventArgs(FileDropEvent, new DataObjectContent(e.Data), path));
	}

	//protected override void OnPreviewGiveFeedback(GiveFeedbackEventArgs e) {
	//	if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
	//		e.Handled = true;
	//	}
	//}

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
}

public class FileDropEventArgs : RoutedEventArgs {
	public DataObjectContent Content { get; }
	/// <summary>
	/// 拖动到的Path，可能是文件夹或者文件，为null表示当前路径
	/// </summary>
	public string Path { get; }

	public FileDropEventArgs(RoutedEvent e, DataObjectContent content, string path) {
		RoutedEvent = e;
		Content = content;
		Path = path;
	}
}