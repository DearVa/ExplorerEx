using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExplorerEx.Command;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 带有动画、可收起的TabControl
/// </summary>
public class FluentTabControl : TabControl {
	public static readonly DependencyProperty CanDeselectProperty = DependencyProperty.Register(
		"CanDeselect", typeof(bool), typeof(FluentTabControl), new PropertyMetadata(default(bool)));

	/// <summary>
	/// 是否可以再次点击TabItem取消选择
	/// </summary>
	public bool CanDeselect {
		get => (bool)GetValue(CanDeselectProperty);
		set => SetValue(CanDeselectProperty, value);
	}

	public SimpleCommand TabItemPreviewMouseDownCommand { get; }
	public SimpleCommand TabItemPreviewMouseUpCommand { get; }

	public FluentTabControl() {
		TabStripPlacement = Dock.Left;
		TabItemPreviewMouseDownCommand = new SimpleCommand(OnTabItemPreviewMouseDown);
		TabItemPreviewMouseUpCommand = new SimpleCommand(OnTabItemPreviewMouseUp);
	}

	private TabItem _mouseDownFileTabItem;
	private Point mouseDownPoint;

	private void OnTabItemPreviewMouseDown(object args) {
		var e = (MouseButtonEventArgs)args;
		_mouseDownFileTabItem = (TabItem)ContainerFromElement((DependencyObject)e.OriginalSource);
		mouseDownPoint = e.GetPosition(this);
		e.Handled = true;
	}

	private void OnTabItemPreviewMouseUp(object args) {
		var e = (MouseButtonEventArgs)args;
		var tabItem = (TabItem)ContainerFromElement((DependencyObject)e.OriginalSource);
		if (tabItem != null && tabItem == _mouseDownFileTabItem) {
			var point = e.GetPosition(this);
			if (Math.Abs(point.X - mouseDownPoint.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - mouseDownPoint.Y) < SystemParameters.MinimumVerticalDragDistance) {
				if (!tabItem.IsSelected) {
					SelectedItem = tabItem;
				} else if (CanDeselect) {
					SelectedIndex = -1;
				}
			}
		}
	}
}