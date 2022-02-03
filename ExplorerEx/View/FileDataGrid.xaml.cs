using HandyControl.Tools.Extension;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using ExplorerEx.Model;

namespace ExplorerEx.View;

public partial class FileDataGrid {
	private ScrollViewer scrollViewer;
	private Grid contentGrid;
	private Border selectionRect;

	public event Action<object> ItemClicked;

	/// <summary>
	/// 没有点击到item
	/// </summary>
	public event Action BackgroundClicked;

	public FileDataGrid() {
		InitializeComponent();
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		scrollViewer = (ScrollViewer)GetTemplateChild("DG_ScrollViewer");
		contentGrid = (Grid)GetTemplateChild("ContentGrid");
		selectionRect = (Border)GetTemplateChild("SelectionRect");
	}

	/// <summary>
	/// 是否左键点击了，不加这个可能会从外部拖进来依旧是框选状态
	/// </summary>
	private bool isMouseDown;

	/// <summary>
	/// 是否正在框选
	/// </summary>
	private bool isRectSelecting;

	private Point startPoint;
	private DispatcherTimer timer;

	protected override void OnPreviewMouseDown(MouseButtonEventArgs e) {
		if (e.ChangedButton == MouseButton.Left && ContainerFromElement(this, (DependencyObject)e.OriginalSource) is DataGridRow) {
			return;
		}
		if (IsChildOf(typeof(ScrollBar), (UIElement)e.OriginalSource)) {
			return;
		}
		if (e.ChangedButton is MouseButton.Left or MouseButton.Right) {
			var point = e.GetPosition(contentGrid);
			var x = Math.Min(Math.Max(point.X, 0), contentGrid.ActualWidth);
			var y = Math.Min(Math.Max(point.Y, 0), contentGrid.ActualHeight);
			startPoint = new Point(x, y);
			isMouseDown = true;
		}
		base.OnPreviewMouseDown(e);
	}

	private Vector scrollSpeed;

	protected override void OnPreviewMouseMove(MouseEventArgs e) {
		if (isMouseDown && e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) {
			if (!isRectSelecting) {
				lastStartIndex = Items.Count;
				lastEndIndex = -1;
				selectionRect.Visibility = Visibility.Visible;
				Mouse.Capture(this);
				scrollSpeed = new Vector();
				timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Input, RectSelectScroll, Dispatcher);
				timer.Start();
				isRectSelecting = true;
			}
			UpdateRectSelection();

			var relativePoint = e.GetPosition(this);
			if (relativePoint.X < 0) {
				scrollSpeed.X = relativePoint.X / 10d;
			} else if (relativePoint.Y > ActualWidth) {
				scrollSpeed.X = (relativePoint.X - ActualWidth) / 10d;
			} else {
				scrollSpeed.X = 0;
			}
			if (relativePoint.Y < 0) {
				scrollSpeed.Y = relativePoint.Y / 10d;
			} else if (relativePoint.Y > ActualHeight) {
				scrollSpeed.Y = (relativePoint.Y - ActualHeight) / 10d;
			} else {
				scrollSpeed.Y = 0;
			}
		}
		base.OnPreviewMouseMove(e);
	}

	private int lastStartIndex, lastEndIndex;

	private void UpdateRectSelection() {
		var point = Mouse.GetPosition(contentGrid);
		var x = Math.Min(Math.Max(point.X, 0), contentGrid.ActualWidth);
		var y = Math.Min(Math.Max(point.Y, 0), contentGrid.ActualHeight);
		double l, t, w, h;
		if (x < startPoint.X) {
			l = x;
			w = startPoint.X - x;
		} else {
			l = startPoint.X;
			w = x - startPoint.X;
		}
		if (y < startPoint.Y) {
			t = y;
			h = startPoint.Y - y;
		} else {
			t = startPoint.Y;
			h = y - startPoint.Y;
		}
		selectionRect.Margin = new Thickness(l, t, 0, 0);
		selectionRect.Width = w;
		selectionRect.Height = h;

		if (Items.Count > 0) {
			var row0 = (DataGridRow)ItemContainerGenerator.ContainerFromIndex(0);
			var point0 = row0.TranslatePoint(new Point(), contentGrid);
			if (l < point0.X + row0.DesiredSize.Width) {  // 框的左边界在列内。每列Width都是一样的
				var row1 = (DataGridRow)ItemContainerGenerator.ContainerFromIndex(1);
				var point1 = row1.TranslatePoint(new Point(), contentGrid);
				var dY = point1.Y - point0.Y;
				// 0: 5-15 1: 20-30 2: 35-45
				// dY: 15  ActualHeight: 10  point0.Y: 5
				// 设 t = 14, h = 10
				// 选区为0-1
				var startIndex = Math.Max((int)((t - point0.Y) / dY), 0);
				var items = (ObservableCollection<FileViewBaseItem>)ItemsSource;  // 这里不用Items索引，我看过源代码，复杂度比较高
				var endIndex = Math.Min((int)((h + t - point0.Y) / dY), items.Count - 1);
				if (startIndex != lastStartIndex) {
					for (var i = lastStartIndex; i < startIndex; i++) {
						items[i].IsSelected = false;
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
		if (isRectSelecting && e.ChangedButton is MouseButton.Left or MouseButton.Right) {
			selectionRect.Visibility = Visibility.Collapsed;
			Mouse.Capture(null);
			isMouseDown = isRectSelecting = false;
			timer?.Stop();
		} else {
			if (e.ChangedButton == MouseButton.Left && ContainerFromElement(this, (DependencyObject)e.OriginalSource) is DataGridRow row) {
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

	private static bool IsChildOf(Type parentType, UIElement child) {
		while (child != null) {
			if (child.GetType() == parentType) {
				return true;
			}
			child = (UIElement)child.GetVisualOrLogicalParent();
		}
		return false;
	}
}