using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using ExplorerEx.Command;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 带有动画、可收起的TabControl
/// </summary>
public class FluentTabControl : TabControl {
	public static readonly DependencyProperty CanDeselectProperty = DependencyProperty.Register(
		nameof(CanDeselect), typeof(bool), typeof(FluentTabControl), new PropertyMetadata(default(bool)));

	/// <summary>
	/// 是否可以再次点击TabItem取消选择
	/// </summary>
	public bool CanDeselect {
		get => (bool)GetValue(CanDeselectProperty);
		set => SetValue(CanDeselectProperty, value);
	}

	public SimpleCommand TabItemPreviewMouseDownCommand { get; }
	public SimpleCommand TabItemPreviewMouseUpCommand { get; }

	private TabItem? mouseDownFileTabItem;
	private Point mouseDownPoint;
	private Border contentPanel = null!;
	private FluentBorder fluentBorder = null!;
	private readonly Storyboard storyboard;
	private int? targetIndex;
	private static readonly CubicEase CubicEase = new() { EasingMode = EasingMode.EaseInOut };

	public FluentTabControl() {
		TabStripPlacement = Dock.Left;
		TabItemPreviewMouseDownCommand = new SimpleCommand(OnTabItemPreviewMouseDown);
		TabItemPreviewMouseUpCommand = new SimpleCommand(OnTabItemPreviewMouseUp);
		storyboard = new Storyboard();
		storyboard.Completed += (_, _) => Animate();
		Loaded += OnLoaded;
	}

	private void OnLoaded(object sender, RoutedEventArgs e) {
		if (SelectedIndex == -1) {
			contentPanel.Visibility = Visibility.Collapsed;
		} else {
			contentPanel.Visibility = Visibility.Visible;
		}
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		contentPanel = (Border)GetTemplateChild("ContentPanel")!;
		fluentBorder = (FluentBorder)GetTemplateChild("FluentBorder")!;
		Storyboard.SetTarget(storyboard, fluentBorder);
	}

	private void OnTabItemPreviewMouseDown(object? args) {
		var e = (MouseButtonEventArgs)args!;
		mouseDownFileTabItem = (TabItem)ContainerFromElement((DependencyObject)e.OriginalSource)!;
		mouseDownPoint = e.GetPosition(this);
		e.Handled = true;
	}

	private void OnTabItemPreviewMouseUp(object? args) {
		var e = (MouseButtonEventArgs)args!;
		var tabItem = (TabItem?)ContainerFromElement((DependencyObject)e.OriginalSource);
		if (tabItem != null && tabItem == mouseDownFileTabItem) {
			var point = e.GetPosition(this);
			if (Math.Abs(point.X - mouseDownPoint.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - mouseDownPoint.Y) < SystemParameters.MinimumVerticalDragDistance) {
				if (!tabItem.IsSelected) {
					SelectedItem = tabItem;
					contentPanel.Visibility = Visibility.Visible;
				} else if (CanDeselect) {
					SelectedIndex = -1;
					contentPanel.Visibility = Visibility.Collapsed;
				}
			}
		}
	}

	protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
		base.OnSelectionChanged(e);
		if (targetIndex == null) {
			targetIndex = SelectedIndex;
			Animate();
		} else {
			targetIndex = SelectedIndex;
		}
	}

	private void Animate() {
		if (targetIndex == null) {
			return;
		}
		var index = targetIndex.Value;
		targetIndex = null;
		double toTop;
		var nowTop = fluentBorder.Margin.Top;
		if (index < 0) {
			var itemsCount = Items.Count;
			if (nowTop > itemsCount * 20d) {
				toTop = itemsCount * 40d + 8d;
			} else {
				toTop = -32d;
			}
		} else {
			toTop = index * 40d + 8d;
		}
		var topAnimation = new DoubleAnimation(toTop, TimeSpan.FromMilliseconds(200)) { EasingFunction = CubicEase };
		Storyboard.SetTargetProperty(topAnimation, new PropertyPath(FluentBorder.TopProperty));
		var bottomAnimation = new DoubleAnimation(toTop + 24d, TimeSpan.FromMilliseconds(200)) { EasingFunction = CubicEase };
		Storyboard.SetTargetProperty(bottomAnimation, new PropertyPath(FluentBorder.BottomProperty));
		if (nowTop > toTop) {
			bottomAnimation.BeginTime = TimeSpan.FromMilliseconds(100);
		} else {
			topAnimation.BeginTime = TimeSpan.FromMilliseconds(100);
		}
		storyboard.Children.Clear();
		storyboard.Children.Add(topAnimation);
		storyboard.Children.Add(bottomAnimation);
		storyboard.Begin(fluentBorder);
	}
}