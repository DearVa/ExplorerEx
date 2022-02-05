﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using HandyControl.Data;
using HandyControl.Interactivity;
using HandyControl.Tools;
using HandyControl.Tools.Extension;

namespace HandyControl.Controls; 

public class TabItem : System.Windows.Controls.TabItem {
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

	/// <summary>
	///     是否显示上下文菜单
	/// </summary>
	public static readonly DependencyProperty ShowContextMenuProperty =
		TabControl.ShowContextMenuProperty.AddOwner(typeof(TabItem), new FrameworkPropertyMetadata(OnShowContextMenuChanged));

	public static readonly DependencyProperty MenuProperty = DependencyProperty.Register(
		"Menu", typeof(ContextMenu), typeof(TabItem), new PropertyMetadata(default(ContextMenu), OnMenuChanged));

	public static readonly RoutedEvent ClosingEvent = EventManager.RegisterRoutedEvent("Closing", RoutingStrategy.Bubble, typeof(EventHandler), typeof(TabItem));

	public static readonly RoutedEvent ClosedEvent = EventManager.RegisterRoutedEvent("Closed", RoutingStrategy.Bubble, typeof(EventHandler), typeof(TabItem));

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
	///     鼠标按下时选项卡位置
	/// </summary>
	private int mouseDownIndex;

	/// <summary>
	///     鼠标按下时选项卡横向偏移
	/// </summary>
	private double mouseDownOffsetX;

	/// <summary>
	///     鼠标按下时的坐标
	/// </summary>
	private Point mouseDownPoint;

	private TabPanel tabPanel;

	public TabItem() {
		CommandBindings.Add(new CommandBinding(ControlCommands.Close, (_, _) => Close()));
		CommandBindings.Add(new CommandBinding(ControlCommands.CloseOther, (_, _) => TabControlParent.CloseOtherItems(this)));
		Loaded += (_, _) => OnMenuChanged(Menu);
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

	/// <summary>
	///     是否显示上下文菜单
	/// </summary>
	public bool ShowContextMenu {
		get => (bool)GetValue(ShowContextMenuProperty);
		set => SetValue(ShowContextMenuProperty, ValueBoxes.BooleanBox(value));
	}

	public ContextMenu Menu {
		get => (ContextMenu)GetValue(MenuProperty);
		set => SetValue(MenuProperty, value);
	}

	private TabControl TabControlParent => ItemsControl.ItemsControlFromItemContainer(this) as TabControl;

	private Border TabBorder => TabControlParent.TabBorder;

	private static void OnShowContextMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var ctl = (TabItem)d;
		if (ctl.Menu != null) {
			var show = (bool)e.NewValue;
			ctl.Menu.IsEnabled = show;
			ctl.Menu.Show(show);
		}
	}

	public static void SetShowContextMenu(DependencyObject element, bool value) {
		element.SetValue(ShowContextMenuProperty, ValueBoxes.BooleanBox(value));
	}

	public static bool GetShowContextMenu(DependencyObject element) {
		return (bool)element.GetValue(ShowContextMenuProperty);
	}

	private static void OnMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var ctl = (TabItem)d;
		ctl.OnMenuChanged(e.NewValue as ContextMenu);
	}

	private void OnMenuChanged(ContextMenu menu) {
		if (IsLoaded && menu != null) {
			var parent = TabControlParent;
			if (parent == null) {
				return;
			}

			var item = parent.ItemContainerGenerator.ItemFromContainer(this);

			menu.DataContext = item;
			menu.SetBinding(IsEnabledProperty, new Binding(ShowContextMenuProperty.Name) {
				Source = this
			});
			menu.SetBinding(VisibilityProperty, new Binding(ShowContextMenuProperty.Name) {
				Source = this,
				Converter = ResourceHelper.GetResourceInternal<IValueConverter>(ResourceToken.Boolean2VisibilityConverter)
			});
		}
	}

	/// <summary>
	///     更新选项卡横向偏移
	/// </summary>
	/// <param name="oldIndex"></param>
	private void UpdateItemOffsetX(int oldIndex) {
		if (!isDragging || CurrentIndex >= TabPanel.ItemDic.Count) {
			return;
		}

		var moveItem = TabPanel.ItemDic[CurrentIndex];
		moveItem.CurrentIndex -= CurrentIndex - oldIndex;
		var offsetX = moveItem.TargetOffsetX;
		var resultX = offsetX + (oldIndex - CurrentIndex) * ItemWidth;
		TabPanel.ItemDic[CurrentIndex] = this;
		TabPanel.ItemDic[moveItem.CurrentIndex] = moveItem;
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
		RaiseEvent(new RoutedEventArgs(ClosedEvent, item));

		var list = parent.GetActualList();
		list?.Remove(item);
	}

	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
		base.OnMouseLeftButtonDown(e);

		if (VisualTreeHelper.HitTest(this, e.GetPosition(this)) == null) {
			return;
		}
		// 所有TabItem放在这个里边
		var border = TabBorder;
		if (border == null) {
			return;
		}

		if (!isItemDragging && !isDragging) {
			TabPanel.SetValue(TabPanel.FluidMoveDurationPropertyKey, new Duration(TimeSpan.FromSeconds(0)));
			mouseDownOffsetX = RenderTransform.Value.OffsetX;
			var mx = TranslatePoint(new Point(), border).X;
			mouseDownIndex = CalLocationIndex(mx);
			var subIndex = mouseDownIndex;
			maxMoveLeft = -subIndex * ItemWidth;
			maxMoveRight = border.ActualWidth - ActualWidth + maxMoveLeft;

			isDragging = true;
			isItemDragging = true;
			isWaiting = true;
			dragPoint = e.GetPosition(border);
			dragPoint = new Point(dragPoint.X, dragPoint.Y);
			mouseDownPoint = dragPoint;
			CaptureMouse();
		}
	}

	protected override void OnMouseMove(MouseEventArgs e) {
		base.OnMouseMove(e);

		if (isItemDragging && isDragging) {
			var parent = TabControlParent;
			if (parent == null) {
				return;
			}

			var subX = TranslatePoint(new Point(), parent).X;
			CurrentIndex = CalLocationIndex(subX);

			var p = e.GetPosition(parent);

			var subLeft = p.X - dragPoint.X;
			var totalLeft = p.X - mouseDownPoint.X;

			if (Math.Abs(subLeft) <= WaitLength && isWaiting) {
				return;
			}

			isWaiting = false;
			isDragged = true;

			var left = subLeft + RenderTransform.Value.OffsetX;
			if (totalLeft < maxMoveLeft) {
				left = maxMoveLeft + mouseDownOffsetX;
			} else if (totalLeft > maxMoveRight) {
				left = maxMoveRight + mouseDownOffsetX;
			}

			var t = new TranslateTransform(left, 0);
			RenderTransform = t;
			dragPoint = p;
		}
	}

	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
		base.OnMouseLeftButtonUp(e);

		ReleaseMouseCapture();

		if (isDragged) {
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

		isDragging = false;
		isItemDragging = false;
		isDragged = false;
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

			if (list.IndexOf(item) != index) {
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

	public event EventHandler Closing {
		add => AddHandler(ClosingEvent, value);
		remove => RemoveHandler(ClosingEvent, value);
	}

	public event EventHandler Closed {
		add => AddHandler(ClosedEvent, value);
		remove => RemoveHandler(ClosedEvent, value);
	}
}