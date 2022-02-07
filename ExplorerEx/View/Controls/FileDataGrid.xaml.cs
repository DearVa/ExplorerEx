using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ExplorerEx.Model;
using ExplorerEx.Win32;
using HandyControl.Tools;
using TextBox = HandyControl.Controls.TextBox;

namespace ExplorerEx.View.Controls;

public partial class FileDataGrid {
	private ScrollViewer scrollViewer;
	private Grid contentGrid;
	private Border selectionRect;

	public event Action<object> ItemClicked;

	public delegate void FileDropEventHandler(object sender, FileDropEventArgs e);

	public static readonly RoutedEvent FileDropEvent = EventManager.RegisterRoutedEvent(
		"FileDrop", RoutingStrategy.Bubble, typeof(FileDropEventHandler), typeof(FileDataGrid));

	public event FileDropEventHandler FileDrop {
		add => AddHandler(FileDropEvent, value);
		remove => RemoveHandler(FileDropEvent, value);
	}

	/// <summary>
	/// 没有点击到item
	/// </summary>
	public event Action BackgroundClicked;

	public FileDataGrid() {
		InitializeComponent();
		EventManager.RegisterClassHandler(typeof(TextBox), GotFocusEvent, new RoutedEventHandler(OnRenameTextBoxGotFocus));
	}

	private void OnRenameTextBoxGotFocus(object sender, RoutedEventArgs e) {
		var textBox = (TextBox)sender;
		renamingItem = (FileViewBaseItem)textBox.DataContext;
		var lastIndexOfDot = textBox.Text.LastIndexOf('.');
		if (lastIndexOfDot == -1) {
			textBox.SelectAll();
		} else {
			textBox.Select(0, lastIndexOfDot);
		}
		e.Handled = true;
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
	private bool isPreparedForRect;

	private bool isMouseDownOnItem;
	private Point startDragPosition;

	/// <summary>
	/// 是否正在框选
	/// </summary>
	private bool isRectSelecting;

	private Point startSelectionPoint;
	private DispatcherTimer timer;

	private FileViewBaseItem renamingItem;
	private CancellationTokenSource renameCts;

	protected override void OnPreviewMouseDown(MouseButtonEventArgs e) {
		if (renamingItem is { EditingName: not null }) {
			if (!e.OriginalSource.IsChildOf(typeof(TextBox), typeof(FileDataGrid))) {
				renamingItem.StopRename();
			}
			return;
		}
		renameCts?.Cancel();
		if (e.ChangedButton is MouseButton.Left or MouseButton.Right) {
			if (e.OriginalSource.IsChildOf(typeof(ScrollBar), typeof(FileDataGrid))) {
				isMouseDownOnItem = false;
				return;
			}
			if (ContainerFromElement(this, (DependencyObject)e.OriginalSource) is DataGridRow row) {
				var item = (FileSystemItem)row.Item;
				if (item.IsSelected) {
					if (renameCts == null) {
						var cts = renameCts = new CancellationTokenSource();
						Task.Run(() => {
							Thread.Sleep(600);
							if (!cts.IsCancellationRequested) {
								item.BeginRename();
							}
						}, renameCts.Token);
					} else {
						renameCts = null;
					}
				} else {
					item.IsSelected = true;
					renameCts = null;
				}
				isMouseDownOnItem = true;
				isPreparedForRect = false;
			} else {
				isPreparedForRect = true;
				isMouseDownOnItem = false;
				startDragPosition = e.GetPosition(contentGrid);
				var x = Math.Min(Math.Max(startDragPosition.X, 0), contentGrid.ActualWidth);
				var y = Math.Min(Math.Max(startDragPosition.Y, 0), contentGrid.ActualHeight);
				startSelectionPoint = new Point(x + scrollViewer.HorizontalOffset, y + scrollViewer.VerticalOffset);
			}
		}
		base.OnPreviewMouseDown(e);
	}

	private Vector scrollSpeed;

	protected override void OnPreviewMouseMove(MouseEventArgs e) {
		if (renamingItem is {EditingName: not null}) {
			return;
		}
		if ((isPreparedForRect || isMouseDownOnItem) && e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) {
			var point = e.GetPosition(contentGrid);
			if (isMouseDownOnItem) {
				if (Math.Abs(point.X - startDragPosition.X) > SystemParameters.MinimumHorizontalDragDistance ||
					Math.Abs(point.Y - startDragPosition.Y) > SystemParameters.MinimumVerticalDragDistance) {
					var data = new DataObject(DataFormats.FileDrop, SelectedItems.Cast<FileSystemItem>().Select(item => item.FullPath).ToArray(), true);
					DragDrop.DoDragDrop(this, data, DragDropEffects.Copy | DragDropEffects.Link | DragDropEffects.Move);
				}
				return;
			}
			if (!isRectSelecting) {
				if (lastStartIndex <= lastEndIndex) {
					var items = (ObservableCollection<FileViewBaseItem>)ItemsSource;  // 这里不用Items索引，我看过源代码，复杂度比较高
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
		base.OnPreviewMouseMove(e);
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
				var items = (ObservableCollection<FileViewBaseItem>)ItemsSource;  // 这里不用Items索引，我看过源代码，复杂度比较高
				var endIndex = Math.Min((int)((h + t - point0.Y) / dY), items.Count - 1);
				if (startIndex != lastStartIndex) {
					for (var i = lastStartIndex; i < startIndex; i++) {
						if (i < items.Count)
						{
							items[i].IsSelected = false;
						}
					}
					for (var i = startIndex; i <= lastStartIndex && i <= endIndex; i++) {
						items[i].IsSelected = true;
					}
					// Trace.WriteLine($"S: {startIndex} Ls: {lastStartIndex}");
					lastStartIndex = startIndex;
				}
				if (endIndex != lastEndIndex) {
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
		isMouseDownOnItem = false;
		if (renamingItem is { EditingName: not null }) {
			return;
		}
		if (isRectSelecting && e.ChangedButton is MouseButton.Left or MouseButton.Right) {
			selectionRect.Visibility = Visibility.Collapsed;
			Mouse.Capture(null);
			isPreparedForRect = isRectSelecting = false;
			timer?.Stop();
		} else if (e.ChangedButton == MouseButton.Left) {
			if (ContainerFromElement(this, (DependencyObject)e.OriginalSource) is DataGridRow row) {
				ItemClicked?.Invoke(row.Item);
			} else {
				BackgroundClicked?.Invoke();
			}
		}
		base.OnPreviewMouseUp(e);
	}

	private void RectSelectScroll(object sender, EventArgs e) {
		scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollSpeed.X);
		scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollSpeed.Y);
		UpdateRectSelection();
	}

	private void OnDragOver(object sender, DragEventArgs e) {
		e.Effects = DragDropEffects.Copy;
		//var data = new DataObjectContent(e.Data);
		//bool isDirectory;
		//if (ContainerFromElement(this, (DependencyObject)e.OriginalSource) is DataGridRow row) {
		//	var item = (FileSystemItem)row.Item;
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
			var item = (FileSystemItem)row.Item;
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