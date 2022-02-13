using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ExplorerEx.ViewModel;
using TabItem = HandyControl.Controls.TabItem;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 分屏方向
/// </summary>
public enum SplitOrientation {
	None,
	Left,
	Bottom,
	Right
}

/// <summary>
/// 提供分屏支持
/// </summary>
public partial class SplitGrid {
	public MainWindow MainWindow { get; }

	public SplitGrid OwnerSplitGrid { get; private set; }

	public FileTabControl FileTabControl { get; private set; }

	public bool AnyOtherTabs => OwnerSplitGrid != null || otherSplitGrid != null;
	/// <summary>
	/// 分屏的阈值，即鼠标到边界占比多少时显示为边界分屏
	/// </summary>
	private const double Threshold = 0.2d;

	private static DoubleAnimation showAnimation, hideAnimation;
	private SplitOrientation orientation;
	/// <summary>
	/// 如果分屏，会把FileTabControl放在这里面
	/// </summary>
	private SplitGrid thisSplitGrid;
	/// <summary>
	/// 如果分屏，这个是分的另一个Grid
	/// </summary>
	private SplitGrid otherSplitGrid;

	private bool isClosing;

	private SplitGrid(MainWindow mainWindow, SplitGrid ownerSplitGrid) {
		showAnimation ??= new DoubleAnimation(1d, new Duration(TimeSpan.FromMilliseconds(150)));
		hideAnimation ??= new DoubleAnimation(0d, new Duration(TimeSpan.FromMilliseconds(150)));

		MainWindow = mainWindow;
		OwnerSplitGrid = ownerSplitGrid;
		InitializeComponent();
		RowDefinitions.Add(new RowDefinition());
		ColumnDefinitions.Add(new ColumnDefinition());
	}

	public SplitGrid(MainWindow mainWindow, SplitGrid ownerSplitGrid, FileViewGridViewModel grid = null) : this(mainWindow, ownerSplitGrid) {
		FileTabControl = new FileTabControl(mainWindow, this, grid);
		Children.Insert(0, FileTabControl);
	}

	public SplitGrid(MainWindow mainWindow, SplitGrid ownerSplitGrid, FileTabControl tab) : this(mainWindow, ownerSplitGrid) {
		FileTabControl = tab;
		Children.Insert(0, tab);
	}

	/// <summary>
	/// 将FileTabControl放入thisSplitGrid
	/// </summary>
	private void FirstSplit() {
		// 首次分屏，要把FileTabControl加入一个新的splitGrid里
		Children.Remove(FileTabControl);
		thisSplitGrid = new SplitGrid(MainWindow, this, FileTabControl);
		Children.Insert(0, thisSplitGrid);
		DragArea.AllowDrop = false;  // 取消分屏拖动
		DragArea.Background = null;
	}

	/// <summary>
	/// 分屏，将一个TabItem加入
	/// </summary>
	public void Split(FileViewGridViewModel grid, SplitOrientation orientation) {
		if (otherSplitGrid != null) {  // 已经分屏了，就直接返回
			return;
		}
		var contains = FileTabControl.TabItems.Contains(grid);  // 要分屏的tab是否包含在了当前TabControl中
		var moreThan1 = FileTabControl.TabItems.Count > 1;
		switch (orientation) {
		case SplitOrientation.None:
			if (!contains) {  // 这种就是将tab加入到当前TabControl中，只有不包括的时候才加入
				FileTabControl.TabItems.Add(grid);
				FileTabControl.SelectedIndex = FileTabControl.TabItems.Count - 1;
				TabItem.MoveAfterDrag = true;
			}
			break;
		case SplitOrientation.Left when !contains || moreThan1: {  // tab不在当前TabControl中或者TabControl中有超过一个Tab
				ColumnDefinitions.Add(new ColumnDefinition());
				FirstSplit();
				thisSplitGrid.SetValue(ColumnProperty, 1);
				otherSplitGrid = new SplitGrid(MainWindow, this, grid);
				otherSplitGrid.SetValue(ColumnProperty, 0);
				Children.Insert(0, otherSplitGrid);
				TabItem.MoveAfterDrag = true;
				break;
			}
		case SplitOrientation.Bottom when !contains || moreThan1: {
				RowDefinitions.Add(new RowDefinition());
				FirstSplit();
				otherSplitGrid = new SplitGrid(MainWindow, this, grid);
				otherSplitGrid.SetValue(RowProperty, 1);
				Children.Insert(0, otherSplitGrid);
				TabItem.MoveAfterDrag = true;
				break;
			}
		case SplitOrientation.Right when !contains || moreThan1: {
				ColumnDefinitions.Add(new ColumnDefinition());
				FirstSplit();
				otherSplitGrid = new SplitGrid(MainWindow, this, grid);
				otherSplitGrid.SetValue(ColumnProperty, 1);
				Children.Insert(0, otherSplitGrid);
				TabItem.MoveAfterDrag = true;
				break;
			}
		}
	}

	/// <summary>
	/// 关闭这个Grid和他的所有子Grid
	/// </summary>
	/// <returns></returns>
	public void Close() {
		if (isClosing) {
			return;
		}
		isClosing = true;
		FileTabControl.CloseAllTabs();
		FileTabControl = null;
		thisSplitGrid = null;
		otherSplitGrid?.Close();
		otherSplitGrid = null;
		OwnerSplitGrid?.CancelSubSplit();
	}

	/// <summary>
	/// 关闭当前的分屏。比如说分屏了一次是在右边，那么就关闭左边的分屏，让右半边充满。下面的注释都假设是向右分屏。
	/// </summary>
	public void CancelSplit() {
		if (otherSplitGrid != null) {  // 如果右半边有分屏，否则就是没有分屏的状态，直接关闭
			otherSplitGrid.OwnerSplitGrid = null;
			FileTabControl.CloseAllTabs();  // FileTabControl没用了，把所有标签都关掉
			Children.RemoveRange(0, 2);  // index0和1分别是FileTabControl的SplitGrid和otherSplitGrid，2是分屏的预览Grid，不能Remove
			FileTabControl = otherSplitGrid.FileTabControl;  // 换成otherSplitGrid.FileTabControl
			FileTabControl.OwnerSplitGrid = this;
			if (otherSplitGrid.otherSplitGrid != null) {  // otherSplitGrid.otherSplitGrid不为null，即otherSplitGrid也是分了屏的
				otherSplitGrid.Children.RemoveRange(0, 2);
				if (ColumnDefinitions.Count < otherSplitGrid.ColumnDefinitions.Count) {
					ColumnDefinitions.Add(new ColumnDefinition());
				} else if (ColumnDefinitions.Count > otherSplitGrid.ColumnDefinitions.Count) {
					ColumnDefinitions.RemoveAt(1);
				}
				if (RowDefinitions.Count < otherSplitGrid.RowDefinitions.Count) {
					RowDefinitions.Add(new RowDefinition());
				} else if (RowDefinitions.Count > otherSplitGrid.RowDefinitions.Count) {
					RowDefinitions.RemoveAt(1);
				}
				Children.Insert(0, otherSplitGrid.thisSplitGrid);
				thisSplitGrid = otherSplitGrid.thisSplitGrid;
				Children.Insert(0, otherSplitGrid.otherSplitGrid);
				otherSplitGrid = otherSplitGrid.otherSplitGrid;
				otherSplitGrid.OwnerSplitGrid = this;  // 不要忘了改OwnerGrid
			} else {
				otherSplitGrid.Children.RemoveAt(0);  // 把otherSplitGrid的child0移除，这个child0可能是FileTabControl也可能是otherSplitGrid的otherSplitGrid（*1）
				Children.Insert(0, FileTabControl);  // 把FileTabControl加入child0，因为没有分屏，所以FileTabControl不用套SplitGrid
				thisSplitGrid = otherSplitGrid = null;
				if (ColumnDefinitions.Count == 2) {
					ColumnDefinitions.RemoveAt(1);
				}
				if (RowDefinitions.Count == 2) {
					RowDefinitions.RemoveAt(1);
				}
			}
			DragArea.AllowDrop = true;
			DragArea.Background = Brushes.Transparent;
		} else {
			Close();
		}
	}

	/// <summary>
	/// 关闭子分屏
	/// </summary>
	private void CancelSubSplit() {
		if (thisSplitGrid != null) {
			otherSplitGrid.Close();
			thisSplitGrid.Children.Clear();
			Children.RemoveRange(0, 2);
			Children.Insert(0, FileTabControl);
			FileTabControl.OwnerSplitGrid = this;
			thisSplitGrid = otherSplitGrid = null;
			if (ColumnDefinitions.Count == 2) {
				ColumnDefinitions.RemoveAt(1);
			}
			if (RowDefinitions.Count == 2) {
				RowDefinitions.RemoveAt(1);
			}
			DragArea.AllowDrop = true;
			DragArea.Background = Brushes.Transparent;
		}
	}

	private void DragArea_OnDrop(object s, DragEventArgs e) {
		if (TabItem.DraggingTab != null) {
			HidePreview();
			Split((FileViewGridViewModel)TabItem.DraggingTab.DataContext, orientation);
		}
	}

	private void DragArea_OnDragEnter(object s, DragEventArgs e) {
		if (TabItem.DraggingTab != null) {  // 正在拖动标签页
			SplitPreviewRectangle.BeginAnimation(OpacityProperty, showAnimation);
			TabItem.DragEnd += HidePreview;
		}
	}

	private void DragArea_OnDragLeave(object s, DragEventArgs e) {
		if (TabItem.DraggingTab != null) {
			TabItem.DragEnd -= HidePreview;
			SplitPreviewRectangle.BeginAnimation(OpacityProperty, hideAnimation);
		}
	}

	private void HidePreview() {
		SplitPreviewRectangle.BeginAnimation(OpacityProperty, hideAnimation);
		DragArea.IsHitTestVisible = false;
	}

	private void DragArea_OnDragOver(object s, DragEventArgs e) {
		if (TabItem.DraggingTab != null) {
			var p = e.GetPosition(this);
			var width = ActualWidth;
			var height = ActualHeight;
			if (p.X < width * Threshold) {
				if (orientation != SplitOrientation.Left) {
					var animation = new ThicknessAnimation(new Thickness(0, 0, width * 0.5d, 0), new Duration(TimeSpan.FromMilliseconds(150)));
					SplitPreviewRectangle.BeginAnimation(MarginProperty, animation);
					orientation = SplitOrientation.Left;
				}
			} else if (p.Y > height * (1 - Threshold)) {
				if (orientation != SplitOrientation.Bottom) {
					var animation = new ThicknessAnimation(new Thickness(0, height * 0.5d, 0, 0), new Duration(TimeSpan.FromMilliseconds(150)));
					SplitPreviewRectangle.BeginAnimation(MarginProperty, animation);
					orientation = SplitOrientation.Bottom;
				}
			} else if (p.X > width * (1 - Threshold)) {
				if (orientation != SplitOrientation.Right) {
					var animation = new ThicknessAnimation(new Thickness(width * 0.5d, 0, 0, 0), new Duration(TimeSpan.FromMilliseconds(150)));
					SplitPreviewRectangle.BeginAnimation(MarginProperty, animation);
					orientation = SplitOrientation.Right;
				}
			} else if (orientation != SplitOrientation.None) {
				var animation = new ThicknessAnimation(new Thickness(), new Duration(TimeSpan.FromMilliseconds(150)));
				SplitPreviewRectangle.BeginAnimation(MarginProperty, animation);
				orientation = SplitOrientation.None;
			}
		}
	}

	private void SplitGrid_OnDragEnter(object sender, DragEventArgs e) {
		if (TabItem.DraggingTab != null) {
			DragArea.IsHitTestVisible = true;
		}
	}

	private void DragArea_OnMouseMove(object sender, MouseEventArgs e) {
		if (TabItem.DraggingTab == null) {
			DragArea.IsHitTestVisible = false;
		}
	}
}