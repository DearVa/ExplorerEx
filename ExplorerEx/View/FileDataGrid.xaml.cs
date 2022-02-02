using HandyControl.Tools.Extension;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ExplorerEx.View;

public partial class FileDataGrid {
	private ScrollViewer scrollViewer;
	private Grid contentGrid;
	private Border selectionRect, dgrBorder;

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
		dgrBorder = (Border)GetTemplateChild("DGR_Border");
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
			startPoint = e.GetPosition(contentGrid);
			isMouseDown = true;
		}
		base.OnPreviewMouseDown(e);
	}

	private Vector scrollSpeed;

	protected override void OnPreviewMouseMove(MouseEventArgs e) {
		if (isMouseDown && e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed) {
			if (!isRectSelecting) {
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

	private void UpdateRectSelection() {
		var point = Mouse.GetPosition(contentGrid);
		double l, t, r, b;
		if (point.X < startPoint.X) {
			l = point.X;
			r = contentGrid.ActualWidth - startPoint.X;
		} else {
			l = startPoint.X;
			r = contentGrid.ActualWidth - point.X;
		}
		if (point.Y < startPoint.Y) {
			t = point.Y;
			b = contentGrid.ActualHeight - startPoint.Y;
		} else {
			t = startPoint.Y;
			b = contentGrid.ActualHeight - point.Y;
		}
		selectionRect.Margin = new Thickness(l, t, r, b);


	}

	protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) {
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
		base.OnPreviewMouseLeftButtonUp(e);
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