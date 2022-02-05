using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HandyControl.Tools;

namespace HandyControl.Controls; 

public class TabPanel : Panel {
	public static readonly DependencyPropertyKey FluidMoveDurationPropertyKey =
		DependencyProperty.RegisterReadOnly("FluidMoveDuration", typeof(Duration), typeof(TabPanel),
			new PropertyMetadata(new Duration(TimeSpan.FromMilliseconds(0))));

	/// <summary>
	///     流式行为持续时间
	/// </summary>
	public static readonly DependencyProperty FluidMoveDurationProperty =
		FluidMoveDurationPropertyKey.DependencyProperty;

	/// <summary>
	///     标签宽度
	/// </summary>
	public static readonly DependencyProperty TabItemWidthProperty = DependencyProperty.Register(
		"TabItemWidth", typeof(double), typeof(TabPanel), new PropertyMetadata(200.0));

	/// <summary>
	///     标签高度
	/// </summary>
	public static readonly DependencyProperty TabItemHeightProperty = DependencyProperty.Register(
		"TabItemHeight", typeof(double), typeof(TabPanel), new PropertyMetadata(30.0));

	/// <summary>
	///     是否已经加载
	/// </summary>
	private bool _isLoaded;

	private int _itemCount;

	private Size _oldSize;

	/// <summary>
	///     是否可以更新
	/// </summary>
	internal bool CanUpdate = true;

	/// <summary>
	///     是否可以强制更新
	/// </summary>
	internal bool ForceUpdate;

	/// <summary>
	///     选项卡字典
	/// </summary>
	internal Dictionary<int, TabItem> ItemDic = new();

	public TabPanel() {
		Loaded += (s, e) => {
			if (_isLoaded) {
				return;
			}
			ForceUpdate = true;
			Measure(new Size(DesiredSize.Width, ActualHeight));
			ForceUpdate = false;
			foreach (var item in ItemDic.Values) {
				item.TabPanel = this;
			}
			_isLoaded = true;
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

	protected override Size MeasureOverride(Size constraint) {
		//if ((_itemCount == InternalChildren.Count || !CanUpdate) && !ForceUpdate && !IsTabFillEnabled) {
		//	return _oldSize;
		//}
		if (TemplatedParent is not TabControl tabControl) {
			return _oldSize;
		}
		constraint.Height = TabItemHeight;
		_itemCount = InternalChildren.Count;

		var size = new Size();

		ItemDic.Clear();

		var count = InternalChildren.Count;
		if (count == 0) {
			_oldSize = new Size();
			return _oldSize;
		}
		constraint.Width += InternalChildren.Count;

		var itemWidth = TabItemWidth;
		var containerWidth = tabControl.TabBorder.ActualWidth;
		if (containerWidth > 0 && itemWidth * count > containerWidth) {
			itemWidth = containerWidth / count;
		}

		for (var index = 0; index < count; index++) {
			if (InternalChildren[index] is TabItem tabItem) {
				tabItem.RenderTransform = new TranslateTransform();
				tabItem.MaxWidth = itemWidth;
				var rect = new Rect {
					X = size.Width - tabItem.BorderThickness.Left,
					Width = itemWidth,
					Height = TabItemHeight
				};
				tabItem.Arrange(rect);
				tabItem.ItemWidth = itemWidth - tabItem.BorderThickness.Left;
				tabItem.CurrentIndex = index;
				tabItem.TargetOffsetX = 0;
				ItemDic[index] = tabItem;
				size.Width += tabItem.ItemWidth;
			}
		}
		size.Height = constraint.Height;
		_oldSize = size;
		return _oldSize;
	}
}