using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using HandyControl.Data.Enum;
using HandyControl.Tools;

namespace HandyControl.Controls;

public class SplitButton : ButtonBase {
	public static readonly DependencyProperty HitModeProperty = DependencyProperty.Register(
		"HitMode", typeof(HitMode), typeof(SplitButton), new PropertyMetadata(default(HitMode)));

	public HitMode HitMode {
		get => (HitMode)GetValue(HitModeProperty);
		set => SetValue(HitModeProperty, value);
	}

	public static readonly DependencyProperty MaxDropDownHeightProperty = DependencyProperty.Register(
		"MaxDropDownHeight", typeof(double), typeof(SplitButton), new PropertyMetadata(SystemParameters.PrimaryScreenHeight / 3.0));

	public double MaxDropDownHeight {
		get => (double)GetValue(MaxDropDownHeightProperty);
		set => SetValue(MaxDropDownHeightProperty, value);
	}

	public static readonly DependencyProperty DropDownContentProperty = DependencyProperty.Register(
		"DropDownContent", typeof(object), typeof(SplitButton), new PropertyMetadata(default, OnDropDownContentChanged));

	private static void OnDropDownContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var button = (SplitButton)d;
		if (e.NewValue is ContextMenu menu) {
			button.isPopup = false;
			menu.PlacementTarget = button;
			menu.Placement = PlacementMode.Bottom;
			menu.VerticalOffset = 6;
			menu.Closed += (_, _) => button.IsDropDownOpen = false;
			menu.PreviewMouseLeftButtonUp += button.OnMenuMouseLeftButtonUp;
		} else {
			button.isPopup = true;
		}
	}

	private void OnMenuMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
		if (Command != null) {
			var item = e.OriginalSource.FindParent<MenuItem>();
			if (item != null) {
				Command.Execute(item);
			}
		}
	}

	public object DropDownContent {
		get => GetValue(DropDownContentProperty);
		set => SetValue(DropDownContentProperty, value);
	}

	public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
		"IsDropDownOpen", typeof(bool), typeof(SplitButton), new PropertyMetadata(default(bool), OnIsDropDownOpenChanged));

	private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if (e.NewValue is true) {
			var button = (SplitButton)d;
			if (button.isPopup) {
				button.popup.IsOpen = true;
			} else {
				var menu = (ContextMenu)button.DropDownContent;
				menu.DataContext = button.DataContext;
				menu.IsOpen = true;
			}
		}
	}

	public bool IsDropDownOpen {
		get => (bool)GetValue(IsDropDownOpenProperty);
		set => SetValue(IsDropDownOpenProperty, value);
	}

	private Popup popup;
	private bool isPopup;

	private ToggleButton arrowButton;

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		arrowButton = (ToggleButton)GetTemplateChild("PART_Arrow")!;
		if (HitMode == HitMode.None) {  // 这种模式就是点击整个按钮就会打开菜单，按钮本身没有作用
			arrowButton.IsHitTestVisible = false;  // 就不响应点击事件
		}
		popup = (Popup)GetTemplateChild("Popup");
	}

	protected override void OnClick() {
		if (HitMode == HitMode.None) {
			IsDropDownOpen = true;
		}
		base.OnClick();
	}

	protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) {
		base.OnPreviewMouseLeftButtonUp(e);
		if (isPopup) {
			if (popup.IsOpen) {
				var menuItem = e.OriginalSource.FindParent<MenuItem, SplitButton>();
				if (menuItem != null) {
					Command.Execute(menuItem.CommandParameter);  // 点击到了MenuItem
					popup.IsOpen = false;
				}
			} else {
				switch (HitMode) {
				case HitMode.Hover:
					e.Handled = true;
					popup.IsOpen = true;
					break;
				case HitMode.Click when e.OriginalSource.IsChildOf(arrowButton, this):
					e.Handled = true;
					if (popup.IsOpen) {
						popup.IsOpen = false;
						Mouse.Capture(null);
					} else {
						popup.IsOpen = true;
					}
					break;
				}
			}
		}
	}

	protected override void OnMouseEnter(MouseEventArgs e) {
		base.OnMouseEnter(e);

		if (HitMode == HitMode.Hover) {
			IsDropDownOpen = true;
		}
	}
}