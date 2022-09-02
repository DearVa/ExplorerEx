using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Animation;
using HandyControl.Controls;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 这个控件会将自身停靠到<see cref="Target"/>，同时当<see cref="Target"/>改变时，会通过动画过渡到新的位置
/// 需要注意的是，停靠并不会更改自身的Parent，所以需要注意视觉树的覆盖关系以及是否会被裁剪
/// 请将本控件放在可以自由拉伸的父容器内，如Grid，且不要设置Row或者Column
/// </summary>
public class DockingTarget : SimplePanel {
	public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
		nameof(Target), typeof(FrameworkElement), typeof(DockingTarget), new PropertyMetadata(null, Target_OnChanged));

	public FrameworkElement? Target {
		get => (FrameworkElement?)GetValue(TargetProperty);
		set => SetValue(TargetProperty, value);
	}

	private static void Target_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var dt = (DockingTarget)d;
		if (e.OldValue is FrameworkElement oldTarget) {
			oldTarget.SizeChanged -= dt.Target_OnSizeChanged;
			oldTarget.Loaded -= dt.Target_OnLoaded;
		}
		if (e.NewValue is FrameworkElement newTarget) {
			newTarget.SizeChanged += dt.Target_OnSizeChanged;
			newTarget.Loaded += dt.Target_OnLoaded;
		}
		dt.MoveToTarget();
	}

	private void Target_OnLoaded(object sender, RoutedEventArgs e) {
		MoveToTarget();
	}

	private void Target_OnSizeChanged(object sender, SizeChangedEventArgs e) {
		MoveToTarget();
	}

	private static readonly CubicEase CubicEase = new() { EasingMode = EasingMode.EaseInOut };

	private ThicknessAnimation? animation;

	public DockingTarget() {
		Loaded += OnLoaded;
	}

	private void OnLoaded(object sender, RoutedEventArgs e) {
		MoveToTarget();
	}

	private void MoveToTarget() {
		var target = Target;
		if (target == null) {
			return;
		}
		if (target.ActualWidth == 0 && target.ActualHeight == 0) {
			return;
		}

		if (Parent is not FrameworkElement parent) {
			throw new InvalidOperationException();
		}

		var leftTop = target.TranslatePoint(new Point(), parent);
		var rightBottom = target.TranslatePoint(new Point(target.ActualWidth, target.ActualHeight), parent);
		if (leftTop == rightBottom) {
			return;
		}

		var thickness = new Thickness(leftTop.X, leftTop.Y, parent.ActualWidth - rightBottom.X, parent.ActualHeight - rightBottom.Y);
		Trace.WriteLine(thickness);

		if (animation != null) {
			animation.To = thickness;
		} else {
			animation = new ThicknessAnimation(thickness, TimeSpan.FromMilliseconds(500)) {
				EasingFunction = CubicEase
			};
			animation.Completed += (_, _) => animation = null;
		}
		BeginAnimation(MarginProperty, animation);
	}
}