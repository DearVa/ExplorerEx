using System;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HandyControl.Data;
using HandyControl.Interactivity;

namespace HandyControl.Controls; 

[TemplatePart(Name = HeaderPanelKey, Type = typeof(TabPanel))]
[TemplatePart(Name = HeaderBorder, Type = typeof(Border))]
[TemplatePart(Name = TabBorderKey, Type = typeof(Border))]
[TemplatePart(Name = NewTabButtonKey, Type = typeof(Button))]
public class TabControl : System.Windows.Controls.TabControl {
	private const string HeaderPanelKey = "PART_HeaderPanel";

	private const string HeaderBorder = "PART_HeaderBorder";

	private const string TabBorderKey = "PART_TabBorder";

	private const string NewTabButtonKey = "NewTabButton";

	/// <summary>
	///     是否显示上下文菜单
	/// </summary>
	public static readonly DependencyProperty ShowContextMenuProperty = DependencyProperty.RegisterAttached(
		"ShowContextMenu", typeof(bool), typeof(TabControl), new FrameworkPropertyMetadata(ValueBoxes.TrueBox, FrameworkPropertyMetadataOptions.Inherits));

	/// <summary>
	///     标签宽度
	/// </summary>
	public static readonly DependencyProperty TabItemWidthProperty = DependencyProperty.Register(
		"TabItemWidth", typeof(double), typeof(TabControl), new PropertyMetadata(200.0));

	/// <summary>
	///     标签高度
	/// </summary>
	public static readonly DependencyProperty TabItemHeightProperty = DependencyProperty.Register(
		"TabItemHeight", typeof(double), typeof(TabControl), new PropertyMetadata(30.0));

	private Border headerBorder;

	internal Border TabBorder { get; private set; }

	internal Button NewTabButton { get; private set; }

	/// <summary>
	///     是否为内部操作
	/// </summary>
	internal bool IsInternalAction;

	internal TabPanel HeaderPanel { get; private set; }

	/// <summary>
	///     是否显示上下文菜单
	/// </summary>
	public bool ShowContextMenu {
		get => (bool)GetValue(ShowContextMenuProperty);
		set => SetValue(ShowContextMenuProperty, ValueBoxes.BooleanBox(value));
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

	public static readonly RoutedEvent NewTabEvent = EventManager.RegisterRoutedEvent("NewTab", RoutingStrategy.Bubble, typeof(EventHandler), typeof(TabControl));

	public event EventHandler NewTab {
		add => AddHandler(NewTabEvent, value);
		remove => RemoveHandler(NewTabEvent, value);
	}

	public TabControl() {
		// 新建标签页
		CommandBindings.Add(new CommandBinding(ControlCommands.Open, (_, _) => RaiseEvent(new RoutedEventArgs(NewTabEvent))));
	}

	public static void SetShowContextMenu(DependencyObject element, bool value) {
		element.SetValue(ShowContextMenuProperty, ValueBoxes.BooleanBox(value));
	}

	public static bool GetShowContextMenu(DependencyObject element) {
		return (bool)element.GetValue(ShowContextMenuProperty);
	}

	protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e) {
		base.OnItemsChanged(e);

		if (HeaderPanel == null) {
			IsInternalAction = false;
			return;
		}

		if (IsInternalAction) {
			IsInternalAction = false;
			return;
		}

		switch (e.Action) {
		case NotifyCollectionChangedAction.Add: {
			for (var i = 0; i < Items.Count; i++) {
				if (ItemContainerGenerator.ContainerFromIndex(i) is not TabItem item) {
					return;
				}
				item.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
				item.TabPanel = HeaderPanel;
			}
			break;
		}
		case NotifyCollectionChangedAction.Remove:
			if (Items.Count > 0 && TabIndex >= Items.Count) {
				TabIndex = Items.Count - 1;
			}
			break;
		}

		headerBorder?.InvalidateMeasure();
		IsInternalAction = false;
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		HeaderPanel = GetTemplateChild(HeaderPanelKey) as TabPanel;
		TabBorder = GetTemplateChild(TabBorderKey) as Border;
		headerBorder = GetTemplateChild(HeaderBorder) as Border;
		NewTabButton = GetTemplateChild(NewTabButtonKey) as Button;
	}

	internal void CloseOtherItems(TabItem currentItem) {
		var actualItem = currentItem != null ? ItemContainerGenerator.ItemFromContainer(currentItem) : null;

		var list = GetActualList();
		if (list == null) {
			return;
		}

		IsInternalAction = true;

		for (var i = 0; i < Items.Count; i++) {
			var item = list[i];
			if (!Equals(item, actualItem) && item != null) {
				var argsClosing = new CancelRoutedEventArgs(TabItem.ClosingEvent, item);

				if (ItemContainerGenerator.ContainerFromItem(item) is not TabItem tabItem) {
					continue;
				}

				tabItem.RaiseEvent(argsClosing);
				if (argsClosing.Cancel) {
					return;
				}

				tabItem.RaiseEvent(new RoutedEventArgs(TabItem.ClosedEvent, item));
				list.Remove(item);

				i--;
			}
		}

		SetCurrentValue(SelectedIndexProperty, Items.Count == 0 ? -1 : 0);
	}

	internal IList GetActualList() {
		IList list;
		if (ItemsSource != null) {
			list = ItemsSource as IList;
		} else {
			list = Items;
		}

		return list;
	}

	protected override bool IsItemItsOwnContainerOverride(object item) {
		return item is TabItem;
	}

	protected override DependencyObject GetContainerForItemOverride() {
		return new TabItem();
	}
}