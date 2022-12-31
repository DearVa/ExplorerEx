using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ExplorerEx.Views.Controls; 

public class FileTabPanel : Panel {
	public static readonly DependencyPropertyKey FluidMoveDurationPropertyKey =
		DependencyProperty.RegisterReadOnly("FluidMoveDuration", typeof(Duration), typeof(FileTabPanel),
			new PropertyMetadata(new Duration(TimeSpan.Zero)));

	/// <summary>
	///     流式行为持续时间
	/// </summary>
	public static readonly DependencyProperty FluidMoveDurationProperty =
		FluidMoveDurationPropertyKey.DependencyProperty;

	/// <summary>
	///     标签宽度
	/// </summary>
	public static readonly DependencyProperty TabItemWidthProperty = DependencyProperty.Register(
		nameof(TabItemWidth), typeof(double), typeof(FileTabPanel), new PropertyMetadata(200.0));

	/// <summary>
	///     标签高度
	/// </summary>
	public static readonly DependencyProperty TabItemHeightProperty = DependencyProperty.Register(
		nameof(TabItemHeight), typeof(double), typeof(FileTabPanel), new PropertyMetadata(30.0));

	/// <summary>
	///     是否已经加载
	/// </summary>
	private bool isLoaded;

	private Size oldSize;

	/// <summary>
	///     选项卡字典
	/// </summary>
	internal readonly Dictionary<int, FileTabItem> ItemDict = new();

	public FileTabPanel() {
		Loaded += (_, _) => {
			if (isLoaded) {
				return;
			}
			Measure(new Size(DesiredSize.Width, ActualHeight));
			foreach (var item in ItemDict.Values) {
				item.FileTabPanel = this;
			}
			isLoaded = true;
		};
	}

	/// <summary>
	///     流式行为持续时间
	/// </summary>
	public Duration FluidMoveDuration {
		get => (Duration)GetValue(FluidMoveDurationProperty);
		set => SetValue(FluidMoveDurationProperty, value);
	}

	/// <summary>
	///     标签宽度
	/// </summary>
	public double TabItemWidth {
		get => (double)GetValue(TabItemWidthProperty);
		set => SetValue(TabItemWidthProperty, value);
	}

	/// <summary>
	///     标签高度
	/// </summary>
	public double TabItemHeight {
		get => (double)GetValue(TabItemHeightProperty);
		set => SetValue(TabItemHeightProperty, value);
	}

	/// <summary>
	/// 存储之前的，当鼠标在上面的时候，就不自动把标签页宽度调大，方便连续关闭
	/// </summary>
	private double prevActualItemWidth = double.PositiveInfinity;

	private bool isWaitingForMouseLeave;

	protected override Size MeasureOverride(Size constraint) {
		if (TemplatedParent is not FileTabControl tabControl) {
			return oldSize;
		}
		constraint.Height = TabItemHeight;

		var size = new Size();

		ItemDict.Clear();

		var count = InternalChildren.Count;
		if (count == 0) {
			oldSize = new Size();
			return oldSize;
		}
		constraint.Width += InternalChildren.Count;

		var itemWidth = TabItemWidth;
		var containerWidth = tabControl.TabBorder.ActualWidth;
		var newButtonLeft = 0d;
		if (containerWidth > 0 && itemWidth * count > containerWidth) {
			itemWidth = containerWidth / count;
		}
		var actualItemWidth = Math.Max(itemWidth - 1, 1);
		if (actualItemWidth > prevActualItemWidth) {
			// 标签页变大了，说明关闭了，这个时候就看鼠标指针是不是在上面悬浮着
			if (IsMouseOver) {
				actualItemWidth = prevActualItemWidth;  // 就先不变大，保留
				itemWidth = actualItemWidth + 1;
				isWaitingForMouseLeave = true;
			} else {
				prevActualItemWidth = actualItemWidth;
			}
		} else {
			prevActualItemWidth = actualItemWidth;
		}

		for (var index = 0; index < count; index++) {
			if (InternalChildren[index] is FileTabItem tabItem) {
				tabItem.RenderTransform = new TranslateTransform();
				tabItem.MaxWidth = itemWidth;
				var rect = new Rect {
					X = size.Width - tabItem.BorderThickness.Left,
					Width = itemWidth,
					Height = TabItemHeight
				};
				newButtonLeft = Math.Max(newButtonLeft, rect.X + itemWidth);
				tabItem.Arrange(rect);
				tabItem.ItemWidth = actualItemWidth;
				tabItem.CurrentIndex = index;
				tabItem.TargetOffsetX = 0;
				ItemDict[index] = tabItem;
				size.Width += actualItemWidth;
			}
		}
		tabControl.NewTabButton.Margin = new Thickness(newButtonLeft + 12, 5, 0, 3);

		size.Height = constraint.Height;
		oldSize = size;
		return oldSize;
	}

	protected override void OnMouseLeave(MouseEventArgs e) {
		base.OnMouseLeave(e);
		if (isWaitingForMouseLeave) {
			isWaitingForMouseLeave = false;
			prevActualItemWidth = double.PositiveInfinity;
			Measure(new Size(DesiredSize.Width, ActualHeight));
		}
	}
}