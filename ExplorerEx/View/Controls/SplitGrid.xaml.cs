using System;
using System.Collections;
using System.Collections.Generic;
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
public partial class SplitGrid : IEnumerable<FileTabControl> {
	public MainWindow MainWindow { get; }

	private SplitGrid OwnerSplitGrid {
		set {
			if (ownerSplitGrid != value) {
				ownerSplitGrid = value;
				FileTabControl.UpdateTabContextMenu();
			}
		}
	}

	private SplitGrid ownerSplitGrid;

	public FileTabControl FileTabControl { get; private set; }

	/// <summary>
	/// 是否有其他分屏
	/// </summary>
	public bool AnySplitScreen => ownerSplitGrid != null || otherSplitGrid != null;

	/// <summary>
	/// 分屏的阈值，即鼠标到边界占比多少时显示为边界分屏
	/// </summary>
	private const double Threshold = 0.3d;

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

	/// <summary>
	/// 是否为最右上的Grid，如果是，那么其中的TabControl要避免遮挡窗口的控制按钮
	/// </summary>
	private bool IsTopRightGrid {
		set {
			if (isTopRightGrid != value) {
				isTopRightGrid = value;
				if (value) {
					FileTabControl.TabBorderRootMargin = new Thickness(0, 0, 160, 0);
				} else {
					FileTabControl.TabBorderRootMargin = new Thickness();
				}
			}
		}
	}

	private bool isTopRightGrid;

#if DEBUG
	private static int id;
#endif

	private static readonly CubicEase CubicEase = new() { EasingMode = EasingMode.EaseInOut };

	private SplitGrid(MainWindow mainWindow, SplitGrid ownerSplitGrid) {
		showAnimation ??= new DoubleAnimation(0.5d, new Duration(TimeSpan.FromMilliseconds(100))) { EasingFunction = CubicEase };
		hideAnimation ??= new DoubleAnimation(0d, new Duration(TimeSpan.FromMilliseconds(100))) { EasingFunction = CubicEase };

		MainWindow = mainWindow;
		this.ownerSplitGrid = ownerSplitGrid;
		InitializeComponent();
#if DEBUG
		Name = $"SplitGrid{id++}";
#endif
	}

	public SplitGrid(MainWindow mainWindow, SplitGrid ownerSplitGrid, FileTabViewModel tab = null) : this(mainWindow, ownerSplitGrid) {
		FileTabControl = new FileTabControl(mainWindow, this, tab);
		Children.Insert(0, FileTabControl);
		if (ownerSplitGrid == null) {
			IsTopRightGrid = true;
		}
		FileTabControl.UpdateTabContextMenu();
	}

	public SplitGrid(MainWindow mainWindow, SplitGrid ownerSplitGrid, FileTabControl tab) : this(mainWindow, ownerSplitGrid) {
		FileTabControl = tab;
		FileTabControl.OwnerSplitGrid = this;
		Children.Insert(0, tab);
		FileTabControl.UpdateTabContextMenu();
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
		Splitter.Visibility = Visibility.Visible;
	}

	/// <summary>
	/// 分屏，将一个TabItem加入
	/// </summary>
	/// <returns>是否成功</returns>
	public bool Split(FileTabViewModel tab, SplitOrientation orientation) {
		if (otherSplitGrid != null) {  // 已经分屏了，就直接返回
			return false;
		}
		var contains = FileTabControl.TabItems.Contains(tab);  // 要分屏的tab是否包含在了当前TabControl中
		if (orientation == SplitOrientation.None) {
			if (!contains) {
				FileTabControl.TabItems.Add(tab);
				FileTabControl.SelectedIndex = FileTabControl.TabItems.Count - 1;
				TabItem.MoveAfterDrag = true;
			}
		} else if (!contains || FileTabControl.TabItems.Count > 1) {  // 要么不包含，要么TabControl中有超过一个Tab才分屏
			switch (orientation) {
			case SplitOrientation.Left: {  // tab不在当前TabControl中或者TabControl中有超过一个Tab
				ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d) });  // Splitter
				Splitter.SetValue(ColumnProperty, 1);
				ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 100 });
				FirstSplit();
				thisSplitGrid.SetValue(ColumnProperty, 2);
				otherSplitGrid = new SplitGrid(MainWindow, this, tab);
				otherSplitGrid.SetValue(ColumnProperty, 0);
				Children.Insert(0, otherSplitGrid);
				TabItem.MoveAfterDrag = true;
				return true;
			}
			case SplitOrientation.Bottom: {
				RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d) });
				Splitter.SetValue(RowProperty, 1);
				RowDefinitions.Add(new RowDefinition { MinHeight = 100 });
				FirstSplit();
				otherSplitGrid = new SplitGrid(MainWindow, this, tab);
				otherSplitGrid.SetValue(RowProperty, 2);
				Children.Insert(0, otherSplitGrid);
				TabItem.MoveAfterDrag = true;
				return true;
			}
			case SplitOrientation.Right: {
				ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d) });  // Splitter
				Splitter.SetValue(ColumnProperty, 1);
				ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 100 });
				FirstSplit();
				otherSplitGrid = new SplitGrid(MainWindow, this, tab);
				if (isTopRightGrid) {
					otherSplitGrid.isTopRightGrid = true;
					isTopRightGrid = false;
					FileTabControl.TabBorderRoot.Margin = new Thickness();
				}
				otherSplitGrid.SetValue(ColumnProperty, 2);
				Children.Insert(0, otherSplitGrid);
				TabItem.MoveAfterDrag = true;
				return true;
			}
			}
		}
		return false;
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
		FileTabControl.Close();
		FileTabControl = null;
		thisSplitGrid = null;
		otherSplitGrid?.Close();
		otherSplitGrid = null;
		if (ownerSplitGrid != null) {
			if (ownerSplitGrid.thisSplitGrid == this) {
				ownerSplitGrid.CancelSplit();
			} else {
				ownerSplitGrid.CancelSubSplit();
			}
		}
	}

	/// <summary>
	/// 关闭当前的分屏。比如说分屏了一次是在右边，那么就关闭左边的分屏，让右半边充满。下面的注释都假设是向右分屏。
	/// </summary>
	public void CancelSplit() {
		if (otherSplitGrid != null) {  // 如果右半边有分屏，否则就是没有分屏的状态，直接关闭
			otherSplitGrid.IsTopRightGrid = isTopRightGrid;
			otherSplitGrid.OwnerSplitGrid = null;
			thisSplitGrid.Close();
			Children.RemoveRange(0, 2);  // index0和1分别是FileTabControl的SplitGrid和otherSplitGrid，2是分屏的预览Grid，不能Remove
			FileTabControl = otherSplitGrid.FileTabControl;  // 换成otherSplitGrid.FileTabControl
			FileTabControl.OwnerSplitGrid = this;
			otherSplitGrid.MoveChildren(this);
			FileTabControl.UpdateTabContextMenu();
		} else {
			Close();
		}
	}

	/// <summary>
	/// 关闭子分屏
	/// </summary>
	public void CancelSubSplit() {
		if (thisSplitGrid != null) {
			IsTopRightGrid = otherSplitGrid.isTopRightGrid;
			otherSplitGrid.Close();
			Children.RemoveRange(0, 2);
			thisSplitGrid.MoveChildren(this);
			FileTabControl.UpdateTabContextMenu();
		}
	}

	/// <summary>
	/// 将Children和分屏状况移动到另一个splitGrid中，要保证splitGrid的Children是空的，之后这个SplitGrid就没用了
	/// </summary>
	/// <param name="splitGrid"></param>
	private void MoveChildren(SplitGrid splitGrid) {
#if DEBUG
		Name = "Deleted" + Name;
#endif
		if (otherSplitGrid != null) {  // 说明自己分屏了
			Children.RemoveRange(0, 2);  // 那就需要Remove两个SplitGrid
										 // 下面需要将Column和Row定义设置成一样的
			if (splitGrid.ColumnDefinitions.Count < ColumnDefinitions.Count) {
				splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d) });
				splitGrid.Splitter.SetValue(ColumnProperty, 1);
				splitGrid.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 100 });
			} else if (splitGrid.ColumnDefinitions.Count > ColumnDefinitions.Count) {
				splitGrid.ColumnDefinitions.RemoveRange(1, 2);
			}
			if (splitGrid.RowDefinitions.Count < RowDefinitions.Count) {
				splitGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d) });
				splitGrid.Splitter.SetValue(RowProperty, 1);
				splitGrid.RowDefinitions.Add(new RowDefinition { MinHeight = 100 });
			} else if (splitGrid.RowDefinitions.Count > RowDefinitions.Count) {
				splitGrid.RowDefinitions.RemoveRange(1, 2);
			}
			splitGrid.Children.Insert(0, thisSplitGrid);
			splitGrid.thisSplitGrid = thisSplitGrid;
			thisSplitGrid.OwnerSplitGrid = splitGrid;
			splitGrid.Children.Insert(0, otherSplitGrid);
			splitGrid.otherSplitGrid = otherSplitGrid;
			otherSplitGrid.OwnerSplitGrid = splitGrid;
		} else {
			Children.RemoveAt(0);  // 把child0移除，即FileTabControl
			splitGrid.Children.Insert(0, FileTabControl);  // 把FileTabControl加入child0，因为没有分屏，所以FileTabControl不用套SplitGrid
			FileTabControl.OwnerSplitGrid = splitGrid;
			splitGrid.thisSplitGrid = splitGrid.otherSplitGrid = null;
			splitGrid.Splitter.Visibility = Visibility.Collapsed;
			if (splitGrid.ColumnDefinitions.Count == 3) {
				splitGrid.ColumnDefinitions.RemoveRange(1, 2);
			}
			if (splitGrid.RowDefinitions.Count == 3) {
				splitGrid.RowDefinitions.RemoveRange(1, 2);
			}
			splitGrid.DragArea.AllowDrop = true;
			splitGrid.DragArea.Background = Brushes.Transparent;
		}
		FileTabControl.UpdateTabContextMenu();
	}

	protected override void OnMouseMove(MouseEventArgs e) {
		base.OnMouseMove(e);
		if (TabItem.DraggingTab != null && !DragArea.IsHitTestVisible) {  // 正在拖动标签页
			DragArea.IsHitTestVisible = true;
			SplitPreviewRectangle.BeginAnimation(OpacityProperty, showAnimation);
			TabItem.DragEnd += OnDragEnd;
		}
	}

	protected void DragArea_OnMouseLeave(object s, MouseEventArgs e) {
		if (TabItem.DraggingTab != null) {
			TabItem.DragEnd -= OnDragEnd;
			HidePreview();
		}
	}

	private void OnDragEnd() {
		HidePreview();
		if (TabItem.DraggingTab != null) {
			Split((FileTabViewModel)TabItem.DraggingTab.DataContext, orientation);
		}
	}

	private void HidePreview() {
		SplitPreviewRectangle.BeginAnimation(OpacityProperty, hideAnimation);
		DragArea.IsHitTestVisible = false;
	}

	protected void DragArea_OnMouseMove(object s, MouseEventArgs e) {
		if (TabItem.DraggingTab != null) {
			var p = e.GetPosition(this);
			var width = ActualWidth;
			var height = ActualHeight;
			if (p.X < width * Threshold) {
				if (orientation != SplitOrientation.Left) {
					var animation = new ThicknessAnimation(new Thickness(0, 0, width * 0.5d, 0), new Duration(TimeSpan.FromMilliseconds(100))) { EasingFunction = CubicEase };
					SplitPreviewRectangle.BeginAnimation(MarginProperty, animation);
					orientation = SplitOrientation.Left;
				}
			} else if (p.Y > height * (1 - Threshold)) {
				if (orientation != SplitOrientation.Bottom) {
					var animation = new ThicknessAnimation(new Thickness(0, height * 0.5d, 0, 0), new Duration(TimeSpan.FromMilliseconds(100))) { EasingFunction = CubicEase };
					SplitPreviewRectangle.BeginAnimation(MarginProperty, animation);
					orientation = SplitOrientation.Bottom;
				}
			} else if (p.X > width * (1 - Threshold)) {
				if (orientation != SplitOrientation.Right) {
					var animation = new ThicknessAnimation(new Thickness(width * 0.5d, 0, 0, 0), new Duration(TimeSpan.FromMilliseconds(100))) { EasingFunction = CubicEase };
					SplitPreviewRectangle.BeginAnimation(MarginProperty, animation);
					orientation = SplitOrientation.Right;
				}
			} else if (orientation != SplitOrientation.None) {
				var animation = new ThicknessAnimation(new Thickness(), new Duration(TimeSpan.FromMilliseconds(100))) { EasingFunction = CubicEase };
				SplitPreviewRectangle.BeginAnimation(MarginProperty, animation);
				orientation = SplitOrientation.None;
			}
		}
	}

	public IEnumerator<FileTabControl> GetEnumerator() {
		return new Enumerator(this);
	}

	public override string ToString() {
		return Name;
	}

	IEnumerator IEnumerable.GetEnumerator() {
		return GetEnumerator();
	}

	public class Enumerator : IEnumerator<FileTabControl> {
		private readonly SplitGrid splitGrid;

		private Stack<SplitGrid> splitGrids;

		public Enumerator(SplitGrid splitGrid) {
			this.splitGrid = splitGrid;
		}

		public bool MoveNext() {
			if (splitGrids == null) {
				splitGrids = new Stack<SplitGrid>();
				splitGrids.Push(splitGrid);
			} else if (splitGrids.Count == 0) {
				return false;
			}
			while (true) {
				var splitGrid = splitGrids.Pop();
				if (splitGrid.thisSplitGrid == null) { // 没有分屏，
					Current = splitGrid.FileTabControl;
					break;
				}
				if (splitGrid.otherSplitGrid.thisSplitGrid != null) { // 分的两个屏都也分屏了
					splitGrids.Push(splitGrid.thisSplitGrid);
					splitGrids.Push(splitGrid.otherSplitGrid);  // 入栈，继续遍历
				} else {
					splitGrids.Push(splitGrid.thisSplitGrid);
					Current = splitGrid.otherSplitGrid.FileTabControl;
					break;
				}
			}
			return true;
		}

		public void Reset() {
			splitGrids = null;
		}

		public FileTabControl Current { get; private set; }

		object IEnumerator.Current => Current;

		public void Dispose() {
			Current = null;
			GC.SuppressFinalize(this);
		}
	}
}
