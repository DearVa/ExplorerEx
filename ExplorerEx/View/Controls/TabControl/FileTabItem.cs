using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ExplorerEx.Win32;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Interactivity;
using HandyControl.Tools;

namespace ExplorerEx.View.Controls; 

public class FileTabItem : TabItem {
	/// <summary>
	/// 当前正在Drag的tab
	/// </summary>
	public static FileTabItem DraggingFileTab { get; private set; }
	/// <summary>
	/// 拖动标签的目标
	/// </summary>
	public static FileTabControl DragTabDestination { get; set; }
	/// <summary>
	/// Continue设为结束DragDrop
	/// </summary>
	public static DispatcherFrame DragFrame { get; private set; }
	/// <summary>
	/// Drag结束时触发，之后会清除所有事件
	/// </summary>
	public static event Action DragEnd;

	private static DragPreviewWindow dragTabPreviewWindow;

	public static readonly DependencyProperty CanMoveToNewWindowProperty = DependencyProperty.Register(
		"CanMoveToNewWindow", typeof(bool), typeof(FileTabItem), new PropertyMetadata(default(bool)));

	public bool CanMoveToNewWindow {
		get => (bool)GetValue(CanMoveToNewWindowProperty);
		set => SetValue(CanMoveToNewWindowProperty, value);
	}

	public static readonly DependencyProperty CanSplitScreenProperty = DependencyProperty.Register(
		"CanSplitScreen", typeof(bool), typeof(FileTabItem), new PropertyMetadata(default(bool)));

	public bool CanSplitScreen {
		get => (bool)GetValue(CanSplitScreenProperty);
		set => SetValue(CanSplitScreenProperty, value);
	}

	/// <summary>
	///     动画速度
	/// </summary>
	private const int AnimationSpeed = 150;

	/// <summary>
	///     选项卡拖动等待距离（在鼠标移动了超过20个像素无关单位后，选项卡才开始被拖动）
	/// </summary>
	private const double WaitLength = 20;

	/// <summary>
	///     选项卡是否处于拖动状态
	/// </summary>
	private static bool isItemDragging;

	public static readonly RoutedEvent ClosingEvent = EventManager.RegisterRoutedEvent("Closing", RoutingStrategy.Bubble, typeof(EventHandler), typeof(FileTabItem));

	public static readonly RoutedEvent MovedEvent = EventManager.RegisterRoutedEvent("Moved", RoutingStrategy.Bubble, typeof(EventHandler), typeof(FileTabItem));

	public static readonly RoutedEvent TabCommandEvent = EventManager.RegisterRoutedEvent("TabCommand", RoutingStrategy.Bubble, typeof(EventHandler), typeof(FileTabItem));

	public event EventHandler Closing {
		add => AddHandler(ClosingEvent, value);
		remove => RemoveHandler(ClosingEvent, value);
	}

	public event EventHandler Moved {
		add => AddHandler(MovedEvent, value);
		remove => RemoveHandler(MovedEvent, value);
	}

	public event EventHandler TabCommand {
		add => AddHandler(TabCommandEvent, value);
		remove => RemoveHandler(TabCommandEvent, value);
	}

	/// <summary>
	///     当前编号
	/// </summary>
	private int currentIndex;

	/// <summary>
	///     拖动中的选项卡坐标
	/// </summary>
	private Point dragPoint;

	/// <summary>
	///     选项卡是否已经被拖动
	/// </summary>
	private bool isDragged;

	/// <summary>
	///     选项卡是否处于拖动状态
	/// </summary>
	private bool isDragging;

	/// <summary>
	///     选项卡是否等待被拖动
	/// </summary>
	private bool isWaiting;

	/// <summary>
	///     左侧可移动的最大值
	/// </summary>
	private double maxMoveLeft;

	/// <summary>
	///     右侧可移动的最大值
	/// </summary>
	private double maxMoveRight;

	/// <summary>
	///     鼠标按下时选项卡横向偏移
	/// </summary>
	private double mouseDownOffsetX;

	/// <summary>
	///     鼠标按下相对于TabBorder的坐标
	/// </summary>
	private Point mouseDownPoint;

	/// <summary>
	///     鼠标按下时相对于被拖动Tab的坐标
	/// </summary>
	private Point mouseDownTabPoint;

	private FileTabPanel _fileTabPanel;

	private Grid templateRoot;

	public FileTabItem() {
		Trace.WriteLine("New Tab");
		CommandBindings.Add(new CommandBinding(ControlCommands.Close, (_, _) => Close()));
		CommandBindings.Add(new CommandBinding(ControlCommands.CloseOther, (_, _) => TabControlParent.CloseOtherItems(this)));
		CommandBindings.Add(new CommandBinding(ControlCommands.TabCommand, (_, e) => RaiseEvent(new TabItemCommandArgs(TabCommandEvent, (string)e.Parameter, this))));
		Loaded += (s, _) => {
			((FileTabItem)s).BeginAnimation(OpacityProperty, new DoubleAnimation(1d, new Duration(TimeSpan.FromMilliseconds(300))) {
				EasingFunction = new SineEase {
					EasingMode = EasingMode.EaseIn
				}
			});
		};
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		ContextMenu!.DataContext = this;
		templateRoot = (Grid)GetTemplateChild("templateRoot")!;
		templateRoot.DragEnter += (_, args) => FileTabControl.TabItem_OnDrag(this, args);
		templateRoot.DragOver += (_, args) => FileTabControl.TabItem_OnDrag(this, args);
		templateRoot.Drop += (_, args) => FileTabControl.TabItem_OnDrop(this, args);
	}

	/// <summary>
	///     选项卡宽度
	/// </summary>
	public double ItemWidth { get; internal set; }

	/// <summary>
	///     目标横向位移
	/// </summary>
	internal double TargetOffsetX { get; set; }

	/// <summary>
	///     标签容器
	/// </summary>
	internal FileTabPanel FileTabPanel {
		get {
			if (_fileTabPanel == null && TabControlParent != null) {
				_fileTabPanel = TabControlParent.HeaderPanel;
			}

			return _fileTabPanel;
		}
		set => _fileTabPanel = value;
	}

	/// <summary>
	///     当前编号
	/// </summary>
	internal int CurrentIndex {
		get => currentIndex;
		set {
			if (currentIndex == value || value < 0) {
				return;
			}
			var oldIndex = currentIndex;
			currentIndex = value;
			UpdateItemOffsetX(oldIndex);
		}
	}

	private FileTabControl TabControlParent => ItemsControl.ItemsControlFromItemContainer(this) as FileTabControl;

	/// <summary>
	///     更新选项卡横向偏移
	/// </summary>
	/// <param name="oldIndex"></param>
	private void UpdateItemOffsetX(int oldIndex) {
		if (!isDragging || CurrentIndex >= FileTabPanel.ItemDict.Count) {
			return;
		}

		var moveItem = FileTabPanel.ItemDict[CurrentIndex];
		moveItem.CurrentIndex -= CurrentIndex - oldIndex;
		var offsetX = moveItem.TargetOffsetX;
		var resultX = offsetX + (oldIndex - CurrentIndex) * ItemWidth;
		FileTabPanel.ItemDict[CurrentIndex] = this;
		FileTabPanel.ItemDict[moveItem.CurrentIndex] = moveItem;
		moveItem.CreateAnimation(offsetX, resultX);
	}

	protected override void OnMouseRightButtonDown(MouseButtonEventArgs e) {
		base.OnMouseRightButtonDown(e);

		if (VisualTreeHelper.HitTest(this, e.GetPosition(this)) == null) {
			return;
		}
		IsSelected = true;
		Focus();
	}

	protected override void OnHeaderChanged(object oldHeader, object newHeader) {
		base.OnHeaderChanged(oldHeader, newHeader);

		if (FileTabPanel != null) {
			InvalidateMeasure();
		}
	}

	internal void Close() {
		var parent = TabControlParent;
		if (parent == null) {
			return;
		}

		var item = parent.ItemContainerGenerator.ItemFromContainer(this);

		var argsClosing = new CancelRoutedEventArgs(ClosingEvent, item);
		RaiseEvent(argsClosing);
		if (argsClosing.Cancel) {
			return;
		}

		FileTabPanel.SetValue(FileTabPanel.FluidMoveDurationPropertyKey, new Duration(TimeSpan.FromMilliseconds(200)));

		parent.IsInternalAction = true;

		var list = parent.GetActualList();
		list?.Remove(item);
	}

	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
		base.OnMouseLeftButtonDown(e);

		if (VisualTreeHelper.HitTest(this, e.GetPosition(this)) == null) {
			return;
		}
		// 所有TabItem放在这个里边
		if (TabControlParent == null) {
			return;
		}

		var parent = TabControlParent.TabBorder;
		if (!isItemDragging && !isDragging) {
			FileTabPanel.SetValue(FileTabPanel.FluidMoveDurationPropertyKey, new Duration(TimeSpan.FromSeconds(0)));
			mouseDownOffsetX = RenderTransform.Value.OffsetX;
			var mx = TranslatePoint(new Point(), parent).X;
			mouseDownPoint = e.GetPosition(parent);
			mouseDownTabPoint = e.GetPosition(this);
			StartDrag(parent, mouseDownPoint, CalLocationIndex(mx));
		}
	}

	internal void StartDrag(Border parent, Point mouseDownPoint, int mouseDownIndex) {
		maxMoveLeft = -mouseDownIndex * ItemWidth - mouseDownOffsetX;
		maxMoveRight = parent.ActualWidth - ActualWidth + maxMoveLeft;

		Trace.WriteLine(maxMoveLeft + ", " + maxMoveRight);
		isDragging = true;
		isItemDragging = true;
		isWaiting = true;
		dragPoint = mouseDownPoint;
		CaptureMouse();
	}

	protected override void OnMouseMove(MouseEventArgs e) {
		base.OnMouseMove(e);

		if (DraggingFileTab != null) {
			dragTabPreviewWindow?.MoveWithCursor();
			if (Mouse.LeftButton != MouseButtonState.Pressed || Keyboard.IsKeyDown(Key.Escape)) {
				EndDrag();
			}
			return;
		}

		if (isItemDragging && isDragging) {
			var tabControl = TabControlParent;
			if (tabControl == null) {
				return;
			}

			var parent = tabControl.TabBorder;
			var subX = TranslatePoint(new Point(), parent).X;
			CurrentIndex = CalLocationIndex(subX);

			var p = e.GetPosition(parent);

			var subLeft = p.X - dragPoint.X;
			var subTop = p.Y - dragPoint.Y;

			if (Math.Abs(subLeft) <= WaitLength && Math.Abs(subTop) <= WaitLength && isWaiting) {
				return;
			}

			isWaiting = false;
			isDragged = true;
			tabControl.NewTabButton.Visibility = Visibility.Hidden;

			double left;
			var totalLeft = p.X - mouseDownPoint.X;
			if (totalLeft < maxMoveLeft) {
				left = maxMoveLeft + mouseDownOffsetX;
			} else if (totalLeft > maxMoveRight) {
				left = maxMoveRight + mouseDownOffsetX;
			} else {
				left = subLeft + RenderTransform.Value.OffsetX;
			}
#if DEBUG
			Trace.WriteLine($"{p.X} {left} {totalLeft} {maxMoveLeft} {maxMoveRight}");
#endif

			RenderTransform = new TranslateTransform(left, 0);
			dragPoint = p;

			if (p.X < 0 || p.X > parent.ActualWidth || p.Y < -10 || p.Y > parent.ActualHeight + 10) {  // 开始拖动标签页
				DraggingFileTab = this;
				Opacity = 0d;  // 不能设置Visibility = Hidden; 会导致MouseCapture被取消
				// 把Child给DragDropWindow显示
				dragTabPreviewWindow = DragPreviewWindow.Show((Grid)GetVisualChild(0), mouseDownTabPoint);
				// isItemDragging = isDragging = false 防止继续响应Move事件
				isItemDragging = isDragging = false;
				DragTabDestination = tabControl;
				DragFrame = new DispatcherFrame();
				Dispatcher.PushFrame(DragFrame);

				ReleaseMouseCapture();
				DragFrame = null;
				
				dragTabPreviewWindow.Close();
				dragTabPreviewWindow = null;

				DragEnd?.Invoke();
				DragEnd = null;

				if (DragTabDestination != tabControl) {  // 目标不是当前的TabControl
					IEditableCollectionView items = tabControl.Items;
					if (items.CanRemove) {
						items.Remove(DataContext);
					}
					tabControl.NewTabButton.Visibility = Visibility.Visible;
					tabControl.RaiseEvent(new RoutedEventArgs(MovedEvent, this));
					if (DragTabDestination != null) {
						ContinueDrag(e, DragTabDestination.TabBorder);
					}
				} else {
					Opacity = 1d;
					if (DragTabDestination != null) {
						ContinueDrag(e, parent);
					} else {
						EndDrag();
					}
				}

				DraggingFileTab = null;
			}
		}
	}

	private void ContinueDrag(MouseEventArgs e, Border parent) {
		// 这里使用e.GetPosition(parent)获取到的是错误的结果，此时Mouse坐标被错误地认为位于屏幕的左上角
		Win32Interop.GetCursorPos(out var mousePoint);
		var parentPoint = e.GetPosition(parent);
		mouseDownPoint = new Point(mousePoint.x + parentPoint.X, mousePoint.y + parentPoint.Y);
		var left = mouseDownPoint.X - ActualWidth / 2;
		if (left < 0) {
			left = 0;
		} else if (left > parent.ActualWidth) {
			left = parent.ActualWidth;
		}
		CurrentIndex = CalLocationIndex(left);
		mouseDownOffsetX = left - currentIndex * ItemWidth;
		RenderTransform = new TranslateTransform(mouseDownOffsetX, 0);
		StartDrag(parent, mouseDownPoint, currentIndex);
		isWaiting = false;
	}

	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
		base.OnMouseLeftButtonUp(e);
		ReleaseMouseCapture();
		EndDrag();
	}

	private void EndDrag() {
		if (DragFrame != null) {
			DragFrame.Continue = false;
		}

		isDragging = false;
		isItemDragging = false;
		
		if (isDragged) {
			isDragged = false;

			var parent = TabControlParent;
			if (parent == null) {
				return;
			}
			if (DraggingFileTab != null && DragTabDestination != parent) {
				return;
			}

			var subX = TranslatePoint(new Point(), parent).X;
			var index = CalLocationIndex(subX);
			var left = index * ItemWidth + 8;
			var offsetX = RenderTransform.Value.OffsetX;
			CreateAnimation(offsetX, offsetX - subX + left, index);
		}
	}

	protected override void OnMouseDown(MouseButtonEventArgs e) {
		if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed) {
			Close();
		}
	}

	/// <summary>
	///     创建动画
	/// </summary>
	internal void CreateAnimation(double offsetX, double resultX, int index = -1) {
		var parent = TabControlParent;

		void AnimationCompleted() {
			RenderTransform = new TranslateTransform(resultX, 0);
			if (index == -1) {
				return;
			}

			var list = parent.GetActualList();
			if (list == null) {
				return;
			}

			var item = parent.ItemContainerGenerator.ItemFromContainer(this);
			if (item == null) {
				return;
			}
			
			parent.IsInternalAction = true;

			var indexOf = list.IndexOf(item);
			if (indexOf != -1 && indexOf != index) {
				list.Remove(item);
				parent.IsInternalAction = true;
				list.Insert(index, item);
			}

			_fileTabPanel.SetValue(FileTabPanel.FluidMoveDurationPropertyKey, new Duration(TimeSpan.FromMilliseconds(0)));
			FileTabPanel.Measure(new Size(FileTabPanel.DesiredSize.Width, ActualHeight));

			Focus();
			IsSelected = true;

			if (!IsMouseCaptured) {
				parent.SetCurrentValue(Selector.SelectedIndexProperty, currentIndex);
			}

			parent.NewTabButton.Visibility = Visibility.Visible;
		}

		TargetOffsetX = resultX;

		var animation = AnimationHelper.CreateAnimation(resultX, AnimationSpeed);
		animation.FillBehavior = FillBehavior.Stop;
		animation.Completed += (_, _) => AnimationCompleted();
		var f = new TranslateTransform(offsetX, 0);
		RenderTransform = f;
		f.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.Compose);
	}

	/// <summary>
	///     计算选项卡当前合适的位置编号
	/// </summary>
	/// <param name="left"></param>
	/// <returns></returns>
	private int CalLocationIndex(double left) {
		if (isWaiting) {
			return CurrentIndex;
		}

		var maxIndex = TabControlParent.Items.Count - 1;
		var div = (int)(left / ItemWidth);
		var rest = left % ItemWidth;
		var result = rest / ItemWidth > .5 ? div + 1 : div;

		return result > maxIndex ? maxIndex : result;
	}
}

public class TabItemCommandArgs : RoutedEventArgs {
	public string CommandParameter { get; }

	public FileTabItem FileTabItem { get; }

	public TabItemCommandArgs(RoutedEvent routedEvent, string commandParameter, FileTabItem fileTabItem) {
		RoutedEvent = routedEvent;
		CommandParameter = commandParameter;
		FileTabItem = fileTabItem;
	}
}
