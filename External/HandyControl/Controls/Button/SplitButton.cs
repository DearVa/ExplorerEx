using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using HandyControl.Data;
using HandyControl.Data.Enum;
using HandyControl.Tools.Extension;

namespace HandyControl.Controls {
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

		public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
			"IsDropDownOpen", typeof(bool), typeof(SplitButton), new PropertyMetadata(ValueBoxes.FalseBox));

		public bool IsDropDownOpen {
			get => (bool)GetValue(IsDropDownOpenProperty);
			set => SetValue(IsDropDownOpenProperty, ValueBoxes.BooleanBox(value));
		}

		public static readonly DependencyProperty DropDownContentProperty = DependencyProperty.Register(
			"DropDownContent", typeof(object), typeof(SplitButton), new PropertyMetadata(default(object)));

		public object DropDownContent {
			get => GetValue(DropDownContentProperty);
			set => SetValue(DropDownContentProperty, value);
		}

		private ToggleButton arrowButton;
		private Popup popup;

		public override void OnApplyTemplate() {
			base.OnApplyTemplate();
			arrowButton = (ToggleButton)GetTemplateChild("PART_Arrow")!;
			if (HitMode == HitMode.None) {  // 这种模式就是点击整个按钮就会打开菜单，按钮本身没有作用
				arrowButton.IsHitTestVisible = false;  // 就不响应点击事件
			}
			popup = (Popup)GetTemplateChild("Popup");
		}

		private bool lastClickIsOpen;

		protected override void OnClick() {
			if (HitMode == HitMode.None) {
				if (lastClickIsOpen) {
					lastClickIsOpen = false;
					return;
				}
				IsDropDownOpen = !IsDropDownOpen;
				return;
			}
			base.OnClick();
		}

		protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) {
			if (IsChildOf(typeof(MenuItem), (UIElement)e.OriginalSource)) {
				SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.FalseBox);
				lastClickIsOpen = false;
				return;
			}

			if (IsDropDownOpen) {
				lastClickIsOpen = true;
			}

			base.OnPreviewMouseLeftButtonUp(e);

			switch (HitMode) {
			case HitMode.Hover:
				e.Handled = true;
				IsDropDownOpen = true;
				break;
			case HitMode.Click when IsChildOf(arrowButton, (UIElement)e.OriginalSource):
				e.Handled = true;
				if (IsDropDownOpen) {
					SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.FalseBox);
					Mouse.Capture(null);
				} else {
					SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.TrueBox);
				}
				break;
			}
		}

		protected override void OnMouseEnter(MouseEventArgs e) {
			base.OnMouseEnter(e);

			if (HitMode == HitMode.Hover) {
				SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.TrueBox);
			}
		}

		private static bool IsChildOf(UIElement parent, UIElement child) {
			while (child != null) {
				if (child == parent) {
					return true;
				}
				child = (UIElement)child.GetVisualOrLogicalParent();
			}
			return false;
		}

		private static bool IsChildOf(Type parentType, UIElement child) {
			while (child != null) {
				if (child.GetType() == parentType) {
					return true;
				}
				child = (UIElement)child.GetVisualOrLogicalParent();
			}
			return false;
		}
	}
}
