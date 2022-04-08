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
using HandyControl.Data;
using HandyControl.Interactivity;
using HandyControl.Tools;
using HandyControl.Tools.Interop;

namespace HandyControl.Controls; 

public class TabItem : System.Windows.Controls.TabItem {
	/// <summary>
	/// 当前正在Drag的tab
	/// </summary>
	public static TabItem DraggingTab { get; private set; }
	/// <summary>
	/// 这次Drag是否将标签页移走了，如果为false，说明是拖走又回来了。请在设置<see cref="DragFrame"/>之前先设置这个的值
	/// </summary>
	public static bool MoveAfterDrag { get; set; }
	/// <summary>
	/// 拖动到了TabControl上
	/// </summary>
	internal static TabControl DragToTabControl { get; set; }
	/// <summary>
	/// Continue设为结束DragDrop
	/// </summary>
	public static DispatcherFrame DragFrame { get; private set; }
	/// <summary>
	/// Drag结束时触发，之后会清除所有事件
	/// </summary>
	public static event Action DragEnd;

	public static DragDropWindow DragDropWindow { get; private set; }

	public static readonly DependencyProperty CanMoveToNewWindowProperty = DependencyProperty.Register(
		"CanMoveToNewWindow", typeof(bool), typeof(TabItem), new PropertyMetadata(default(bool)));

	public bool CanMoveToNewWindow {
		get => (bool)GetValue(CanMoveToNewWindowProperty);
		set => SetValue(CanMoveToNewWindowProperty, value);
	}

	public static readonly DependencyProperty CanSplitScreenProperty = DependencyProperty.Register(
		"CanSplitScreen", typeof(bool), typeof(TabItem), new PropertyMetadata(default(bool)));

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

	public static readonly RoutedEvent ClosingEvent = EventManager.RegisterRoutedEvent("Closing", RoutingStrategy.Bubble, typeof(EventHandler), typeof(TabItem));

	public static readonly RoutedEvent MovedEvent = EventManager.RegisterRoutedEvent("Moved", RoutingStrategy.Bubble, typeof(EventHandler), typeof(TabItem));

	public static readonly RoutedEvent TabCommandEvent = EventManager.RegisterRoutedEvent("TabCommand", RoutingStrategy.Bubble, typeof(EventHandler), typeof(TabItem));

	// DragEnter等方法找不出对应的TabItem是哪个，使用自定的方法（如果有更好的方法请帮我提个issue）
	public static readonly RoutedEvent DragDropEnterEvent = EventManager.RegisterRoutedEvent("DragDropEnter", RoutingStrategy.Bubble, typeof(DragEventHandler), typeof(TabItem));

	public static readonly RoutedEvent DragDropOverEvent = EventManager.RegisterRoutedEvent("DragDropOver", RoutingStrategy.Bubble, typeof(DragEventHandler), typeof(TabItem));

	public static readonly RoutedEvent DragDropEvent = EventManager.RegisterRoutedEvent("DragDrop", RoutingStrategy.Bubble, typeof(DragEventHandler), typeof(TabItem));

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

	public event DragEventHandler DragDropEnter {
		add => AddHandler(DragDropEnterEvent, value);
		remove => RemoveHandler(DragDropEnterEvent, value);
	}

	public event DragEventHandler DragDropOver {
		add => AddHandler(DragDropOverEvent, value);
		remove => RemoveHandler(DragDropOverEvent, value);
	}

	public event DragEventHandler DragDrop {
		add => AddHandler(DragDropEvent, value);
		remove => RemoveHandler(DragDropEvent, value);
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

	private TabPanel tabPanel;

	private Grid templateRoot;

	public TabItem() {
		CommandBindings.Add(new CommandBinding(ControlCommands.Close, (_, _) => Close()));
		CommandBindings.Add(new CommandBinding(ControlCommands.CloseOther, (_, _) => TabControlParent.CloseOtherItems(this)));
		CommandBindings.Add(new CommandBinding(ControlCommands.TabCommand, (_, e) => RaiseEvent(new TabItemCommandArgs(TabCommandEvent, (string)e.Parameter, this))));
		Loaded += (s, _) => {
			((TabItem)s).BeginAnimation(OpacityProperty, new DoubleAnimation(1d, new Duration(TimeSpan.FromMilliseconds(300))) {
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
		templateRoot.DragEnter += (_, args) => {
			args.RoutedEvent = DragDropEnterEvent;
			RaiseEvent(new TabItemDragEventArgs(args, this));
		};
		templateRoot.DragOver += (_, args) => {
			args.RoutedEvent = DragDropOverEvent;
			RaiseEvent(new TabItemDragEventArgs(args, this));
		};
		templateRoot.Drop += (_, args) => {
			args.RoutedEvent = DragDropEvent;
			RaiseEvent(new TabItemDragEventArgs(args, this));
		};
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
	internal TabPanel TabPanel {
		get {
			if (tabPanel == null && TabControlParent != null) {
				tabPanel = TabControlParent.HeaderPanel;
			}

			return tabPanel;
		}
		set => tabPanel = value;
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

	private TabControl TabControlParent => ItemsControl.ItemsControlFromItemContainer(this) as TabControl;

	/// <summary>
	///     更新选项卡横向偏移
	/// </summary>
	/// <param name="oldIndex"></param>
	private void UpdateItemOffsetX(int oldIndex) {
		if (!isDragging || CurrentIndex >= TabPanel.ItemDict.Count) {
			return;
		}

		var moveItem = TabPanel.ItemDict[CurrentIndex];
		moveItem.CurrentIndex -= CurrentIndex - oldIndex;
		var offsetX = moveItem.TargetOffsetX;
		var resultX = offsetX + (oldIndex - CurrentIndex) * ItemWidth;
		TabPanel.ItemDict[CurrentIndex] = this;
		TabPanel.ItemDict[moveItem.CurrentIndex] = moveItem;
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

		if (TabPanel != null) {
			TabPanel.ForceUpdate = true;
			InvalidateMeasure();
			TabPanel.ForceUpdate = true;
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

		TabPanel.SetValue(TabPanel.FluidMoveDurationPropertyKey, new Duration(TimeSpan.FromMilliseconds(200)));

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
			TabPanel.SetValue(TabPanel.FluidMoveDurationPropertyKey, new Duration(TimeSpan.FromSeconds(0)));
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

			if ((p.X < 0 || p.X > parent.ActualWidth || p.Y < 0 || p.Y > parent.ActualHeight) && DraggingTab == null) {
				DraggingTab = this;
				// 这里把Child给DragDropWindow显示，不然设为了Hidden显示不出来
				Visibility = Visibility.Hidden;
				DragDropWindow = DragDropWindow.Show((Grid)GetVisualChild(0), mouseDownTabPoint);
				var timer = new DispatcherTimer(TimeSpan.FromSeconds(1 / 60d), DispatcherPriority.Input, DragTimerWork, Dispatcher);
				// isItemDragging = isDragging = false 防止继续响应Move事件
				isItemDragging = isDragging = MoveAfterDrag = false;
				DragToTabControl = null;
				DragFrame = new DispatcherFrame();
				Dispatcher.PushFrame(DragFrame);
				DragFrame = null;

				timer.Stop();
				DragDropWindow.Close();
				DragDropWindow = null;

				DragEnd?.Invoke();
				DragEnd = null;
				DraggingTab = null;

				if (MoveAfterDrag) {  // 为true就删掉当前的标签页
					IEditableCollectionView items = tabControl.Items;
					if (items.CanRemove) {
						items.Remove(DataContext);
					}
					tabControl.NewTabButton.Visibility = Visibility.Visible;
					tabControl.RaiseEvent(new RoutedEventArgs(MovedEvent, this));
					if (DragToTabControl != null) {
						ContinueDrag(e, DragToTabControl.TabBorder);
					}
				} else {
					Visibility = Visibility.Visible;
					if (DragToTabControl != null) {
						ContinueDrag(e, parent);
					} else {
						EndDrag();
					}
				}
			}
		}
	}

	private void DragTimerWork(object s, EventArgs e) {
		DragDropWindow?.MoveWithCursor();
		if (Mouse.LeftButton != MouseButtonState.Pressed || Keyboard.IsKeyDown(Key.Escape)) {
			EndDrag();  // 由于Dispatcher.PushFrame会取消MouseCapture，所以需要在这里处理EndDrag事件
		}
	}

	private void ContinueDrag(MouseEventArgs e, Border parent) {
		// 这里使用e.GetPosition(parent)获取到的是错误的结果，此时Mouse坐标被错误地认为位于屏幕的左上角
		var mousePoint = InteropMethods.GetCursorPos();
		var parentPoint = e.GetPosition(parent);
		mouseDownPoint = new Point(mousePoint.X + parentPoint.X, mousePoint.Y + parentPoint.Y);
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
			TabControlParent.NewTabButton.Visibility = Visibility.Visible;

			if (MoveAfterDrag) {
				return;
			}

			var parent = TabControlParent;
			if (parent == null) {
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

			TabPanel.CanUpdate = false;
			parent.IsInternalAction = true;

			var indexOf = list.IndexOf(item);
			if (indexOf != -1 && indexOf != index) {
				list.Remove(item);
				parent.IsInternalAction = true;
				list.Insert(index, item);
			}

			tabPanel.SetValue(TabPanel.FluidMoveDurationPropertyKey, new Duration(TimeSpan.FromMilliseconds(0)));
			TabPanel.CanUpdate = true;
			TabPanel.ForceUpdate = true;
			TabPanel.Measure(new Size(TabPanel.DesiredSize.Width, ActualHeight));
			TabPanel.ForceUpdate = false;

			Focus();
			IsSelected = true;

			if (!IsMouseCaptured) {
				parent.SetCurrentValue(Selector.SelectedIndexProperty, currentIndex);
			}
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

public class TabItemDragEventArgs : RoutedEventArgs {
	public DragEventArgs DragEventArgs { get; }

	public TabItem TabItem { get; }

	public TabItemDragEventArgs(DragEventArgs args, TabItem tabItem) {
		RoutedEvent = args.RoutedEvent;
		DragEventArgs = args;
		TabItem = tabItem;
	}
}

public class TabItemCommandArgs : RoutedEventArgs {
	public string CommandParameter { get; }

	public TabItem TabItem { get; }

	public TabItemCommandArgs(RoutedEvent routedEvent, string commandParameter, TabItem tabItem) {
		RoutedEvent = routedEvent;
		CommandParameter = commandParameter;
		TabItem = tabItem;
	}
}
