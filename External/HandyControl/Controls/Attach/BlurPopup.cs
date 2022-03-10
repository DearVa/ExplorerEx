using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using HandyControl.Tools.Interop;

namespace HandyControl.Controls;

public static class BlurPopup {
	public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
		"Enabled", typeof(bool), typeof(BlurPopup), new PropertyMetadata(false, Enabled_OnChanged));

	public static void SetEnabled(DependencyObject element, bool value) {
		element.SetValue(EnabledProperty, value);
	}

	public static bool GetEnabled(DependencyObject element) {
		return (bool)element.GetValue(EnabledProperty);
	}

	private static void Enabled_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if (d is Popup popup) {
			if (e.NewValue is true) {
				popup.Opened += Popup_OnOpened;
			} else {
				popup.Opened -= Popup_OnOpened;
			}
		}
	}

	private static void Popup_OnOpened(object sender, EventArgs e) {
		if (sender is Popup popup) {
			if (PresentationSource.FromVisual(popup.Child) is HwndSource hwnd) {
				InteropMethods.EnableRoundCorner(hwnd.Handle);
				InteropMethods.EnableAcrylic(hwnd.Handle);
				InteropMethods.EnableShadows(hwnd.Handle);
			}
		}
	}
}