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
using ExplorerEx.Converter.Grouping;
using ExplorerEx.Model;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.ViewModel;
using ExplorerEx.Win32;
using HandyControl.Controls;
using HandyControl.Tools;
using GridView = System.Windows.Controls.GridView;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using TextBox = HandyControl.Controls.TextBox;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 要能够响应鼠标事件，处理点选、框选、拖放、重命名和双击
/// </summary>
public partial class FileListView : INotifyPropertyChanged {
	/// <summary>
	/// 正在拖放的路径列表
	/// </summary>
	private static string[] draggingPaths;

	public new static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
		"ItemsSource", typeof(ObservableCollection<FileListViewItem>), typeof(FileListView), new PropertyMetadata(ItemsSource_OnChanged));

	private static void ItemsSource_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var fileGrid = (FileListView)d;
		if (e.OldValue is ObservableCollection<FileListViewItem> oldList) {
			oldList.CollectionChanged -= fileGrid.OnItemsChanged;
		}
		if (e.NewValue is ObservableCollection<FileListViewItem> newList) {
			newList.CollectionChanged += fileGrid.OnItemsChanged;
			((ItemsControl)fileGrid).ItemsSource = newList;
		} else {
			((ItemsControl)fileGrid).ItemsSource = null;
		}
		fileGrid.UpdateView();
	}

	public new ObservableCollection<FileListViewItem> ItemsSource {
		get => (ObservableCollection<FileListViewItem>)GetValue(ItemsSourceProperty);
		set => SetValue(ItemsSourceProperty, value);
	}

	public FileTabViewModel ViewModel { get; private set; }

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

	public double ItemWidth => FileView?.ItemWidth ?? 0d;

	public double ItemHeight => FileView?.ItemHeight ?? 30d;

	public Size ActualItemSize => new(ItemWidth + 2d, ItemHeight + 6d);

	public static readonly DependencyProperty FullPathProperty = DependencyProperty.Register(
		"FullPath", typeof(string), typeof(FileListView), new PropertyMetadata(null, OnFullPathChanged));

	private static void OnFullPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var fileDataGrid = (FileListView)d;
		fileDataGrid.isMouseDown = false;
		fileDataGrid.isRectSelecting = false;
		fileDataGrid.isDoubleClicked = false;
		fileDataGrid.isPreparedForRenaming = false;
	}

	public string FullPath {
		get => (string)GetValue(FullPathProperty);
		set => SetValue(FullPathProperty, value);
	}

	public static readonly DependencyProperty FolderProperty = DependencyProperty.Register(
		"Folder", typeof(FileListViewItem), typeof(FileListView), new PropertyMetadata(default(FileListViewItem)));

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
		"MouseItem", typeof(FileListViewItem), typeof(FileListView), new PropertyMetadata(default(FileListViewItem)));

	/// <summary>
	/// 鼠标所在的那一项，随着MouseMove事件更新
	/// </summary>
	public FileListViewItem MouseItem {
		get => (FileListViewItem)GetValue(MouseItemProperty);
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
		EventManager.RegisterClassHandler(typeof(TextBox), GotFocusEvent, new RoutedEventHandler(RenameTextBox_OnGotFocus));
		EventManager.RegisterClassHandler(typeof(TextBox), PreviewKeyDownEvent, new RoutedEventHandler(RenameTextBox_OnPreviewKeyDown));
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

	private async void OnSwitchView(object e) {
		if (e is string param && int.TryParse(param, out var type)) {
			await ViewModel.SwitchViewType(type);
		}
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
		if (e.NewValue is FileTabViewModel viewModel) {
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
			//if (fileView.GroupBy.HasValue) {
			//	sorts.Add(new SortDescription("Order", ListSortDirection.Ascending));
			//}
			sorts.Add(new SortDescription("IsFolder", ListSortDirection.Descending));
			sorts.Add(new SortDescription(fileView.SortBy.ToString(), fileView.IsAscending ? ListSortDirection.Ascending : ListSortDirection.Descending));
			break;
		case nameof(fileView.GroupBy):
			var groups = Items.GroupDescriptions!;
			groups.Clear();
			if (fileView.GroupBy.HasValue) {
				Items.IsLiveGrouping = true;
				IValueConverter converter = null;
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
				Items.IsLiveGrouping = false;
			}
			break;
		case nameof(fileView.ItemSize):
			UpdateUI(nameof(ItemWidth));
			UpdateUI(nameof(ItemHeight));
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
			FileGroupStyle.Panel = ItemsPanel = virtualizingStackPanel;
			var view = new GridView();
			columnsConverter.Convert(view.Columns, FileView);
			View = view;
			var padding = Padding;
			contentPanel.Margin = new Thickness(padding.Left, 30d + padding.Top, padding.Right, padding.Bottom);
		} else {
			FileGroupStyle.Panel = ItemsPanel = virtualizingWrapPanel;
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
		var item = ItemsSource.FirstOrDefault(item => item.Name == (string)fileName);
		if (item != null) {
			ScrollIntoView(item);
			item.IsSelected = true;
		}
	}

	public void StartRename(string fileName) {
		Focus();
		var item = ItemsSource.FirstOrDefault(item => item.Name == fileName);
		if (item == null) {
			if (ViewModel == null || (item = ViewModel.AddSingleItem(fileName)) == null) {
				return;
			}
		}
		ScrollIntoView(item);
		item.IsSelected = true;
		item.StartRename();
	}

	private void RenameTextBox_OnGotFocus(object sender, RoutedEventArgs e) {
		var textBox = (TextBox)sender;
		if (textBox.DataContext is FileListViewItem item) {
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

	private void RenameTextBox_OnPreviewKeyDown(object sender, RoutedEventArgs e) {
		if (renamingItem != null && ((KeyEventArgs)e).Key is Key.Enter or Key.Escape) {
			renamingItem.FinishRename();
			renamingItem = null;
			e.Handled = true;
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
	private DispatcherTimer timer;

	private FileListViewItem renamingItem;
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
		foreach (var item in ItemsSource) {
			item.IsSelected = !item.IsSelected;
		}
	}

	/// <summary>
	/// 用于处理双击事件
	/// </summary>
	private bool isDoubleClicked;

	private FileListViewItem lastMouseUpItem;
	private Point prevMouseUpPoint;
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
		isDoubleClicked = false;
		if (e.OriginalSource.FindParent<VirtualizingPanel, ListView>() == null) {  // 如果没有点击在VirtualizingPanel的范围内
			if (renamingItem != null) {  // 如果正在重命名就停止
				renamingItem.FinishRename();
				renamingItem = null;
			}
			return;  // 如果没有点击在VirtualizingPanel或者点击在了TextBox内就不处理事件，直接返回
		}

		if (e.OriginalSource.FindParent<ListViewItem, VirtualizingPanel>() != null) {  // 点击在了项目上
			if (e.OriginalSource.FindParent<TextBox, ListViewItem>() != null) {  // 如果点击在了重命名的TextBox里，就直接返回
				return;
			}
		} else {
			if (e.OriginalSource.FindParent<Expander, VirtualizingPanel>() != null) {  // 如果点击在了Expander上，也直接返回
				return;
			}
		}

		Focus();
		if (e.ChangedButton is MouseButton.Left or MouseButton.Right) {
			isMouseDown = true;
			shouldRename = false;
			mouseDownPoint = e.GetPosition(contentPanel);
			var item = MouseItem;
			if (item != null) {
				mouseDownRowIndex = ItemsSource.IndexOf(item);
				if (e.ChangedButton == MouseButton.Left) {
					if (item == lastMouseUpItem &&
						Math.Abs(mouseDownPoint.X - prevMouseUpPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
						Math.Abs(mouseDownPoint.Y - prevMouseUpPoint.Y) < SystemParameters.MinimumVerticalDragDistance &&
						DateTimeOffset.Now <= lastMouseUpTime.AddMilliseconds(Win32Interop.GetDoubleClickTime())) {
						isDoubleClicked = true;
						if (ViewModel.SelectedItems.Count > 1) {  // 如果双击就取消其他项的选择，只选择当前项
							foreach (var fileItem in ViewModel.SelectedItems.Where(i => i != item)) {
								fileItem.IsSelected = false;
							}
						}
						RaiseEvent(new ItemClickEventArgs(ItemDoubleClickedEvent, item));
					} else {
						var keyboard = Keyboard.PrimaryDevice;
						if (keyboard.IsKeyDown(Key.LeftCtrl) || keyboard.IsKeyDown(Key.RightCtrl)) {
							lastMouseDownRowIndex = mouseDownRowIndex;
							item.IsSelected = !item.IsSelected;
						} else if (keyboard.IsKeyDown(Key.LeftShift) || keyboard.IsKeyDown(Key.RightShift)) {
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
				if (Settings.Instance.DoubleClickGoBack && lastMouseUpItem == null && e.ChangedButton == MouseButton.Left) {
					if (Math.Abs(mouseDownPoint.X - prevMouseUpPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
					    Math.Abs(mouseDownPoint.Y - prevMouseUpPoint.Y) < SystemParameters.MinimumVerticalDragDistance &&
					    DateTimeOffset.Now <= lastMouseUpTime.AddMilliseconds(Win32Interop.GetDoubleClickTime())) {
						ViewModel.GoToUpperLevelAsync();
					}
				}
			}
			if (renamingItem != null) {  // 如果正在重命名就停止
				renamingItem.FinishRename();
				renamingItem = null;
			}
			var x = Math.Min(Math.Max(mouseDownPoint.X, 0), contentPanel.ActualWidth);
			var y = Math.Min(Math.Max(mouseDownPoint.Y, 0), contentPanel.ActualHeight);
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
				ListBoxItem i => (FileListViewItem)i.Content,
				DataGridRow r => (FileListViewItem)r.Item,
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
		if (!isMouseDown || isDoubleClicked || isDragDropping || renamingItem != null) {
			return;
		}
		// 只有isMouseDown（即OnPreviewMouseDown触发过）为true，这个才有用
		if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) {
			var point = e.GetPosition(contentPanel);
			if (Math.Abs(point.X - mouseDownPoint.X) >= SystemParameters.MinimumHorizontalDragDistance ||
				Math.Abs(point.Y - mouseDownPoint.Y) >= SystemParameters.MinimumVerticalDragDistance) {
				if (mouseDownRowIndex != -1) {
					draggingPaths = ViewModel.SelectedItems.Select(i => i.FullPath).ToArray();
					var selectedItems = ViewModel.SelectedItems;
					var data = new DataObject(DataFormats.FileDrop, selectedItems.Select(item => item.FullPath).ToArray(), true);
					var allowedEffects = selectedItems.Any(item => item is DiskDriveItem) ? DragDropEffects.Link : DragDropEffects.All;
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
				var isClickOnItem = mouseDownRowIndex >= 0 && mouseDownRowIndex < ItemsSource.Count;
				if (isPreparedForRenaming) {
					isPreparedForRenaming = false;
					if (mouseDownRowIndex >= 0 && mouseDownRowIndex < ItemsSource.Count && DateTimeOffset.Now > lastMouseUpTime.AddMilliseconds(Win32Interop.GetDoubleClickTime() * 1.5)) {
						var item = ItemsSource[mouseDownRowIndex];
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
							openedContextMenu = ((FrameworkElement)ContainerFromElement(o))!.ContextMenu!;
							openedContextMenu.SetValue(FileItemAttach.FileItemProperty, item);
							openedContextMenu.DataContext = this;
							var ext = Path.GetExtension(item.FullPath);
							FileAssocList.Clear();
							if (!string.IsNullOrWhiteSpace(ext) && ViewModel.SelectedItems.Count == 1) {
								var list = FileAssocItem.GetAssocList(ext);
								if (list != null) {
									foreach (var fileAssocItem in list) {
										FileAssocList.Add(fileAssocItem);
									}
								}
							}
							openedContextMenu.IsOpen = true;
						} else if (Folder != null) {
							UnselectAll();
							openedContextMenu = ContextMenu;
							openedContextMenu!.IsOpen = true;
						}
						break;
					}
				}
				if (isClickOnItem) {
					lastMouseUpItem = ItemsSource[mouseDownRowIndex];
				} else {
					lastMouseUpItem = null;
				}
				lastMouseUpTime = DateTimeOffset.Now;
				prevMouseUpPoint = e.GetPosition(contentPanel);
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

	/// <summary>
	/// 屏蔽原有的AutoScroll
	/// </summary>
	/// <param name="e"></param>
	protected override void OnIsMouseCapturedChanged(DependencyPropertyChangedEventArgs e) { }

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
		} else if (IsFocused && hoverShowTime < DateTimeOffset.Now && previewPopup == null || previewPopup.FilePath != item.FullPath) {  // 有项目且按下了Alt
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

	public void ScrollIntoView(FileListViewItem item) {
		if (!isRectSelecting && !isDragDropping) {
			if (item == null) {
				scrollViewer.ScrollToTop();
			} else {
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
		DragFilesPreview.Instance.Destination = null;
		if (lastDragOnItem != null) {
			lastDragOnItem.IsSelected = false;
			lastDragOnItem = null;
		}
	}

	/// <summary>
	/// 上一个拖放到的item
	/// </summary>
	private FileListViewItem lastDragOnItem;

	protected override void OnDragOver(DragEventArgs e) {
		e.Handled = true;
		isDragDropping = true;
		if (FileTabItem.DraggingFileTab != null || draggingPaths == null) {
			return;
		}
		FileListViewItem mouseItem;
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
		string destination;  // 拖放的目的地
		if (mouseItem != null) {
			destination = mouseItem.DisplayText;
			contains = draggingPaths.Any(path => path == mouseItem.FullPath);
		} else {
			if (FileView.PathType == PathType.Home) {
				e.Effects = DragDropEffects.None;
				if (lastDragOnItem != null) {
					lastDragOnItem.IsSelected = false;
					lastDragOnItem = null;
				}
				DragFilesPreview.Instance.DragDropEffect = DragDropEffects.None;
				return;
			}
			destination = FullPath;
			contains = draggingPaths.Any(path => path == destination);
		}
		if (lastDragOnItem != mouseItem) {
			if (lastDragOnItem != null) {
				lastDragOnItem.IsSelected = false;
			}
			if (mouseItem != null && !contains) {
				lastDragOnItem = mouseItem;
				mouseItem.IsSelected = true;  // 让拖放到的item高亮
			}
		}

		if (mouseItem != null && contains) {  // 自己不能往自己身上拖放
			e.Effects = DragDropEffects.None;
		} else if (mouseItem is { IsFolder: false } and not FileItem { IsExecutable: true }) {  // 不是可执行文件就禁止拖放
			e.Effects = DragDropEffects.None;
		} else if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } fileList && Path.GetDirectoryName(fileList[0]) == destination) {  // 相同文件夹禁止移动
			e.Effects = DragDropEffects.None;
		}

		var dragFilesPreview = DragFilesPreview.Instance;
		dragFilesPreview.Destination = destination;
		if (mouseItem is FileItem { IsExecutable: true }) {
			dragFilesPreview.OperationText = "DragOpenWith";
			dragFilesPreview.Icon = DragDropEffects.Move;
			dragFilesPreview.DragDropEffect = DragDropEffects.All;
		} else {
			dragFilesPreview.DragDropEffect = GetEffectWithKeyboard(e.Effects);
		}
	}

	protected override void OnDrop(DragEventArgs e) {
		isDragDropping = false;
		var path = e.OriginalSource is DependencyObject d ? ContainerFromElement(d) switch {
			ListBoxItem i => ((FileListViewItem)i.Content).FullPath,
			DataGridRow r => ((FileListViewItem)r.Item).FullPath,
			_ => FullPath
		} : FullPath;
		if (path == null) {
			return;
		}
		FileUtils.HandleDrop(DataObjectContent.Drag, path, GetEffectWithKeyboard(e.Effects));
	}

	protected override void OnPreviewGiveFeedback(GiveFeedbackEventArgs e) {
		DragFilesPreview.MoveWithCursor();
	}

	/// <summary>
	/// 根据键盘按键决定要执行什么操作（Shift移动，Ctrl复制，Alt链接）
	/// </summary>
	/// <param name="effects"></param>
	/// <returns></returns>
	private static DragDropEffects GetEffectWithKeyboard(DragDropEffects effects) {
		var keyboard = Keyboard.PrimaryDevice;
		if (keyboard.IsKeyDown(Key.LeftShift) || keyboard.IsKeyDown(Key.RightShift)) {
			if (effects.HasFlag(DragDropEffects.Move)) {
				return DragDropEffects.Move;
			}
		} else if (keyboard.IsKeyDown(Key.LeftCtrl) || keyboard.IsKeyDown(Key.RightCtrl)) {
			if (effects.HasFlag(DragDropEffects.Copy)) {
				return DragDropEffects.Copy;
			}
		} else if (keyboard.IsKeyDown(Key.LeftAlt) || keyboard.IsKeyDown(Key.RightAlt)) {
			if (effects.HasFlag(DragDropEffects.Link)) {
				return DragDropEffects.Link;
			}
		}
		return effects.GetActualEffect();
	}

	protected override void OnLostFocus(RoutedEventArgs e) {
		if (!IsKeyboardFocusWithin && renamingItem != null) {
			renamingItem.FinishRename();
			renamingItem = null;
		}
		base.OnLostFocus(e);
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
		foreach (var item in ViewModel.SelectedItems.Where(i => i is DiskDriveItem).Cast<DiskDriveItem>().ToImmutableList()) {
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

	public event PropertyChangedEventHandler PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected void UpdateUI([CallerMemberName] string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
