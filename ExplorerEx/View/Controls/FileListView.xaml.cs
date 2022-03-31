using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ExplorerEx.Annotations;
using ExplorerEx.Command;
using ExplorerEx.Converter;
using ExplorerEx.Model;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.ViewModel;
using ExplorerEx.Win32;
using HandyControl.Controls;
using HandyControl.Tools;
using GridView = System.Windows.Controls.GridView;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using TabItem = HandyControl.Controls.TabItem;
using TextBox = HandyControl.Controls.TextBox;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 要能够响应鼠标事件，处理点选、框选、拖放、重命名和双击
/// </summary>
public partial class FileListView : INotifyPropertyChanged {
	/// <summary>
	/// 如果不为null，说明正在Drag，可以修改Destination或者Effect
	/// </summary>
	public static DragFilesPreview DragFilesPreview { get; private set; }

	private static DragDropWindow dragDropWindow;
	/// <summary>
	/// 正在拖放的items列表，从外部拖进来的不算
	/// </summary>
	private static FileItem[] draggingItems;

	public static readonly DependencyProperty ItemsCollectionProperty = DependencyProperty.Register(
		"ItemsCollection", typeof(ObservableCollection<FileItem>), typeof(FileListView), new PropertyMetadata(ItemsSource_OnChanged));

	private static void ItemsSource_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var fileGrid = (FileListView)d;
		if (e.OldValue is ObservableCollection<FileItem> oldList) {
			oldList.CollectionChanged -= fileGrid.OnItemsChanged;
		}
		if (e.NewValue is ObservableCollection<FileItem> newList) {
			newList.CollectionChanged += fileGrid.OnItemsChanged;
			fileGrid.ItemsSource = newList;
		} else {
			fileGrid.ItemsSource = null;
		}
		fileGrid.UpdateView();
	}

	public ObservableCollection<FileItem> ItemsCollection {
		get => (ObservableCollection<FileItem>)GetValue(ItemsCollectionProperty);
		set => SetValue(ItemsCollectionProperty, value);
	}

	public FileGridViewModel ViewModel { get; private set; }

	public delegate void FileDropEventHandler(object sender, FileDropEventArgs e);

	public static readonly RoutedEvent FileDropEvent = EventManager.RegisterRoutedEvent(
		"FileDrop", RoutingStrategy.Bubble, typeof(FileDropEventHandler), typeof(FileListView));

	public event FileDropEventHandler FileDrop {
		add => AddHandler(FileDropEvent, value);
		remove => RemoveHandler(FileDropEvent, value);
	}

	public delegate void ItemDoubleClickEventHandler(object sender, ItemClickEventArgs e);

	public static readonly RoutedEvent ItemDoubleClickedEvent = EventManager.RegisterRoutedEvent(
		"ItemDoubleClicked", RoutingStrategy.Bubble, typeof(ItemDoubleClickEventHandler), typeof(FileListView));

	public event ItemDoubleClickEventHandler ItemDoubleClicked {
		add => AddHandler(ItemDoubleClickedEvent, value);
		remove => RemoveHandler(ItemDoubleClickedEvent, value);
	}

	public static readonly DependencyProperty FileViewProperty = DependencyProperty.Register(
		"FileView", typeof(FileView), typeof(FileListView), new PropertyMetadata(default(FileView), OnViewChanged));

	public FileView FileView {
		get => (FileView)GetValue(FileViewProperty);
		set => SetValue(FileViewProperty, value);
	}

	public Size ItemSize => FileView?.ItemSize ?? new Size(0, 30);

	public Size ActualItemSize {
		get {
			var itemSize = ItemSize;
			return new Size(itemSize.Width + 2d, itemSize.Height + 6d);
		}
	}

	public static readonly DependencyProperty FullPathProperty = DependencyProperty.Register(
		"FullPath", typeof(string), typeof(FileListView), new PropertyMetadata(null, OnFullPathChanged));

	private static void OnFullPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var fileDataGrid = (FileListView)d;
		fileDataGrid.isMouseDown = false;
		fileDataGrid.isRectSelecting = false;
		fileDataGrid.isDoubleClicked = false;
		fileDataGrid.isPreparedForRenaming = false;
		fileDataGrid.lastBIndex = fileDataGrid.lastRIndex = -1;
		fileDataGrid.lastTIndex = fileDataGrid.lastLIndex = int.MaxValue;
	}

	public string FullPath {
		get => (string)GetValue(FullPathProperty);
		set => SetValue(FullPathProperty, value);
	}

	public static readonly DependencyProperty FolderProperty = DependencyProperty.Register(
		"Folder", typeof(FileItem), typeof(FileListView), new PropertyMetadata(default(FileItem)));

	/// <summary>
	/// 当前文件夹，用于空白右键ContextMenu的DataContext
	/// </summary>
	public FileItem Folder {
		get => (FileItem)GetValue(FolderProperty);
		set => SetValue(FolderProperty, value);
	}

	public static readonly DependencyProperty MouseItemProperty = DependencyProperty.Register(
		"MouseItem", typeof(FileItem), typeof(FileListView), new PropertyMetadata(default(FileItem)));

	/// <summary>
	/// 鼠标所在的那一项，随着MouseMove事件更新
	/// </summary>
	public FileItem MouseItem {
		get => (FileItem)GetValue(MouseItemProperty);
		set => SetValue(MouseItemProperty, value);
	}

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
	private readonly ItemsPanelTemplate virtualizingWrapPanel, virtualizingStackPanel;

	private ContextMenu openedContextMenu;
	private ScrollViewer scrollViewer;
	private SimplePanel contentPanel;
	private Border selectionRect;

	public FileListView() {
		DataContextChanged += OnDataContextChanged;
		InitializeComponent();
		EventManager.RegisterClassHandler(typeof(TextBox), GotFocusEvent, new RoutedEventHandler(OnRenameTextBoxGotFocus));
		EventManager.RegisterClassHandler(typeof(TextBox), KeyDownEvent, new RoutedEventHandler(OnRenameTextBoxKeyDown));
		SelectCommand = new SimpleCommand(Select);
		StartRenameCommand = new SimpleCommand(e => StartRename((string)e));
		SwitchViewCommand = new SimpleCommand(OnSwitchView);
		columnsConverter = (FileGridDataGridColumnsConverter)Resources["ColumnsConverter"];
		listBoxTemplateConverter = (FileGridListBoxTemplateConverter)Resources["ListBoxTemplateConverter"];
		virtualizingWrapPanel = (ItemsPanelTemplate)Resources["VirtualizingWrapPanel"];
		virtualizingStackPanel = (ItemsPanelTemplate)Resources["VirtualizingStackPanel"];
		((FileGridListBoxTemplateConverter)Resources["ListBoxTemplateConverter"]).FileListView = this;
		hoverTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Input, MouseHoverTimerWork, Dispatcher);
	}

	private void OnSwitchView(object e) {
		if (e is string param && int.TryParse(param, out var type)) {
			_ = ViewModel.SwitchViewType(type);
		}
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
		if (e.NewValue is FileGridViewModel viewModel) {
			ViewModel = viewModel;
			ContextMenu!.DataContext = viewModel;
		}
	}

	private static void OnViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var fileGrid = (FileListView)d;
		if (e.NewValue is FileView fileView) {
			fileView.Changed += fileGrid.UpdateView;
			fileView.PropertyChanged += fileGrid.OnFileViewPropertyChanged;
			fileGrid.UpdateView();
		}
	}

	private void OnFileViewPropertyChanged(object sender, PropertyChangedEventArgs e) {
		var fileView = FileView;
		if (fileView == null) {
			return;
		}
		switch (e.PropertyName) {
		case nameof(fileView.SortBy):
		case nameof(fileView.IsAscending):
			var sorts = Items.SortDescriptions;
			sorts.Clear();
			sorts.Add(new SortDescription("IsFolder", ListSortDirection.Descending));
			sorts.Add(new SortDescription(fileView.SortBy.ToString(), fileView.IsAscending ? ListSortDirection.Ascending : ListSortDirection.Descending));
			break;
		case nameof(fileView.GroupBy):
			var groups = Items.GroupDescriptions!;
			groups.Clear();
			if (fileView.GroupBy.HasValue) {
				groups.Add(new PropertyGroupDescription(fileView.GroupBy.ToString()));
			}
			break;
		case nameof(fileView.ItemSize):
			UpdateUI(nameof(ItemSize));
			UpdateUI(nameof(ActualItemSize));
			break;
		}
	}

	/// <summary>
	/// 更新视图
	/// </summary>
	public void UpdateView() {
		if (FileView == null) {
			return;
		}
		if (FileView.FileViewType == FileViewType.Details) {
			ItemsPanel = virtualizingStackPanel;
			var view = new GridView();
			columnsConverter.Convert(view.Columns, FileView);
			View = view;
			var padding = Padding;
			contentPanel.Margin = new Thickness(padding.Left, 30d + padding.Top, padding.Right, padding.Bottom);
		} else {
			ItemsPanel = virtualizingWrapPanel;
			ItemTemplate = listBoxTemplateConverter.Convert();
			View = null;
			contentPanel.Margin = Padding;
		}
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		scrollViewer = (ScrollViewer)GetTemplateChild("ViewScrollViewer");
		contentPanel = (SimplePanel)GetTemplateChild("ContentPanel");
		selectionRect = (Border)GetTemplateChild("SelectionRect");
	}

	/// <summary>
	/// 选择某一项
	/// </summary>
	/// <param name="fileName"></param>
	public void Select(object fileName) {
		var item = ItemsCollection.FirstOrDefault(item => item.Name == (string)fileName);
		if (item != null) {
			ScrollIntoView(item);
			item.IsSelected = true;
		}
	}

	public void StartRename(string fileName) {
		var item = ItemsCollection.FirstOrDefault(item => item.Name == fileName);
		if (item == null) {
			if (ViewModel == null || (item = ViewModel.AddSingleItem(fileName)) == null) {
				return;
			}
		}
		ScrollIntoView(item);
		item.IsSelected = true;
		item.StartRename();
	}

	private void OnRenameTextBoxGotFocus(object sender, RoutedEventArgs e) {
		var textBox = (TextBox)sender;
		if (textBox.DataContext is FileItem item) {
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

	private void OnRenameTextBoxKeyDown(object sender, RoutedEventArgs e) {
		if (renamingItem != null && ((KeyEventArgs)e).Key is Key.Enter or Key.Escape) {
			renamingItem.FinishRename();
			renamingItem = null;
		}
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

	private FileItem renamingItem;
	private bool shouldRename;

	private void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e) {
		if (e.OldStartingIndex == lastMouseDownRowIndex) {
			lastMouseDownRowIndex = -1;
		}
		if (e.OldStartingIndex == mouseDownRowIndex) {
			mouseDownRowIndex = -1;
		}
	}

	public void InverseSelection() {
		foreach (var item in ItemsCollection) {
			item.IsSelected = !item.IsSelected;
		}
	}

	/// <summary>
	/// 用于处理双击事件
	/// </summary>
	private bool isDoubleClicked;

	private FileItem lastMouseUpItem;
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
		if (e.OriginalSource.FindParent<VirtualizingPanel, ListView>() == null) {
			if (renamingItem != null) {  // 如果正在重命名就停止
				renamingItem.FinishRename();
				renamingItem = null;
			}
			return;  // 如果没有点击在VirtualizingPanel或者点击在了TextBox内就不处理事件，直接返回
		}
		if (e.OriginalSource.FindParent<TextBox, VirtualizingPanel>() != null) {
			return;
		}

		Focus();
		if (e.ChangedButton is MouseButton.Left or MouseButton.Right) {
			isMouseDown = true;
			shouldRename = false;
			startDragPosition = e.GetPosition(contentPanel);
			var item = MouseItem;
			if (item != null) {
				mouseDownRowIndex = ItemsCollection.IndexOf(item);
				if (e.ChangedButton == MouseButton.Left) {
					if (item == lastMouseUpItem &&
						Math.Abs(startDragPosition.X - lastMouseUpPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
						Math.Abs(startDragPosition.Y - lastMouseUpPoint.Y) < SystemParameters.MinimumVerticalDragDistance &&
						DateTimeOffset.Now <= lastMouseUpTime.AddMilliseconds(Win32Interop.GetDoubleClickTime())) {
						isDoubleClicked = true;
						if (ViewModel.SelectedItems.Count > 1) {  // 如果双击就取消其他项的选择，只选择当前项
							foreach (var fileItem in ViewModel.SelectedItems.Where(i => i != item)) {
								fileItem.IsSelected = false;
							}
						}
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
										ItemsCollection[i].IsSelected = true;
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
							} else if (selectedItems.Count == 1 && renamingItem == null) {
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
			if (renamingItem != null) {  // 如果正在重命名就停止
				renamingItem.FinishRename();
				renamingItem = null;
			}
			var x = Math.Min(Math.Max(startDragPosition.X, 0), contentPanel.ActualWidth);
			var y = Math.Min(Math.Max(startDragPosition.Y, 0), contentPanel.ActualHeight);
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
			var mouseItem = ContainerFromElement(o) switch {
				ListBoxItem i => (FileItem)i.Content,
				DataGridRow r => (FileItem)r.Item,
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
			var point = e.GetPosition(contentPanel);
			if (Math.Abs(point.X - startDragPosition.X) >= SystemParameters.MinimumHorizontalDragDistance ||
				Math.Abs(point.Y - startDragPosition.Y) >= SystemParameters.MinimumVerticalDragDistance) {
				if (mouseDownRowIndex != -1) {
					draggingItems = SelectedItems.Cast<FileItem>().ToArray();
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
							var items = ItemsCollection;
							for (var i = lastTIndex; i <= lastBIndex; i++) {
								items[i].IsSelected = false;
							}
							lastTIndex = ItemsCollection.Count;
							lastBIndex = -1;
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
	}

	private int lastLIndex, lastRIndex, lastTIndex, lastBIndex;

	/// <summary>
	/// 计算框选的元素
	/// </summary>
	private void UpdateRectSelection() {
		var point = Mouse.GetPosition(contentPanel);
		var actualWidth = contentPanel.ActualWidth;
		var x = Math.Min(Math.Max(point.X, 0), actualWidth) + scrollViewer.HorizontalOffset;
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
		selectionRect.Margin = new Thickness(l - scrollViewer.HorizontalOffset, t - scrollViewer.VerticalOffset, 0, 0);
		selectionRect.Width = w;
		selectionRect.Height = h;

		if (ItemsCollection.Count > 0) {
			var items = ItemsCollection;
			var itemWidth = FileView.ItemSize.Width;
			var itemHeight = FileView.ItemSize.Height;
			var dY = itemHeight + 6;  // 上下两项的y值之差，4是两项之间的间距，是固定的值
			if (itemWidth > 0) {
				var xCount = (int)(actualWidth / itemWidth);  // 横向能容纳多少个元素
				var yCount = (int)MathF.Ceiling((float)items.Count / xCount);  // 纵向有多少行
				var dX = (contentPanel.ActualWidth - itemWidth * xCount) / (xCount + 2);  // 横向两项之间的间距，分散对齐，两边是有间距的
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
				var row0 = (UIElement)ItemContainerGenerator.ContainerFromIndex(firstIndex);
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
		do {
			if (!isMouseDown) {  // 只有isMouseDown（即OnPreviewMouseDown触发过）为true，这个才有用
				break;
			}
			isMouseDown = false;
			if (isDoubleClicked || renamingItem != null) {  // 如果双击了或者正在重命名就不处理
				break;
			}
			if (e.ChangedButton is MouseButton.Left or MouseButton.Right) {
				if (isRectSelecting) {  // 如果正在框选，那就取消框选
					isRectSelecting = false;
					selectionRect.Visibility = Visibility.Collapsed;
					Mouse.Capture(null);
					timer?.Stop();
					break;
				}

				if (e.OriginalSource.FindParent<VirtualizingPanel, ListView>() == null || e.OriginalSource.FindParent<TextBox, VirtualizingPanel>() != null) {
					break; // 如果没有点击在VirtualizingPanel或者点击在了TextBox内就不处理事件，直接返回
				}
				var isClickOnItem = mouseDownRowIndex >= 0 && mouseDownRowIndex < ItemsCollection.Count;
				if (isPreparedForRenaming) {
					isPreparedForRenaming = false;
					if (mouseDownRowIndex >= 0 && mouseDownRowIndex < ItemsCollection.Count && DateTimeOffset.Now > lastMouseUpTime.AddMilliseconds(Win32Interop.GetDoubleClickTime() * 1.5)) {
						var item = ItemsCollection[mouseDownRowIndex];
						if (!shouldRename) {
							shouldRename = true;
							Task.Run(() => {
								Thread.Sleep(Win32Interop.GetDoubleClickTime());
								if (shouldRename) {
									Trace.WriteLine(item);
									renamingItem = item;
									item.StartRename();
								}
							});
						} else {
							shouldRename = false;
						}
					}
				} else {
					var isCtrlOrShiftPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
					switch (e.ChangedButton) {
					case MouseButton.Left:
						if (isClickOnItem) {
							var item = ItemsCollection[mouseDownRowIndex];
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
							var item = ItemsCollection[mouseDownRowIndex];
							openedContextMenu = ((FrameworkElement)ContainerFromElement(o))!.ContextMenu!;
							openedContextMenu.SetValue(FileItemAttach.FileItemProperty, item);
							openedContextMenu.DataContext = this;
							openedContextMenu.IsOpen = true;
						} else if (Folder != null) {
							openedContextMenu = ContextMenu;
							openedContextMenu!.IsOpen = true;
						}
						break;
					}
				}
				if (isClickOnItem) {
					lastMouseUpItem = ItemsCollection[mouseDownRowIndex];
					lastMouseUpTime = DateTimeOffset.Now;
					lastMouseUpPoint = e.GetPosition(contentPanel);
				} else {
					lastMouseUpItem = null;
				}
				e.Handled = true;
			}
		} while (false);
		mouseDownRowIndex = -1;
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

	public void ScrollIntoView(FileItem item) {
		if (!isRectSelecting && !isDragDropping) {
			if (item == null) {
				scrollViewer.ScrollToTop();
			} else {
				scrollViewer.ScrollToBottom(); // 确保在最上面
				ScrollIntoView((object)item);
			}
		}
	}

	private void RectSelectScroll(object sender, EventArgs e) {
		scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollSpeed.X);
		scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollSpeed.Y);
		UpdateRectSelection();
	}

	protected override void OnDragEnter(DragEventArgs e) {
		isDragDropping = true;
		if (FileView.PathType == PathType.Home && TabItem.DraggingTab == null) {
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
	private FileItem lastDragOnItem;

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
				if (FileView.PathType == PathType.Home) {
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
		if (!IsKeyboardFocusWithin && renamingItem != null) {
			renamingItem.FinishRename();
			renamingItem = null;
		}
		base.OnLostFocus(e);
	}

	public class ItemClickEventArgs : RoutedEventArgs {
		public FileItem Item { get; }

		public ItemClickEventArgs(RoutedEvent e, FileItem item) {
			RoutedEvent = e;
			Item = item;
		}
	}

	#region MenuItem点击事件，用Binding的话太浪费资源了
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

	private void FormatDiskDrive_OnClick(object sender, RoutedEventArgs e) {
		if (ViewModel == null) {
			return;
		}
		foreach (var item in ViewModel.SelectedItems.Where(i => i is DiskDrive).Cast<DiskDrive>().ToImmutableList()) {
			Shell32Interop.ShowFormatDriveDialog(item.Drive);
		}
	}
	#endregion

	public event PropertyChangedEventHandler PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected void UpdateUI([CallerMemberName] string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
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