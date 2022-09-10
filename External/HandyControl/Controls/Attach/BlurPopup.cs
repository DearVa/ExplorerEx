using System;
using System.Collections.Generic;
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
				popup.Closed += Popup_OnClosed;
			} else {
				popup.Opened -= Popup_OnOpened;
			}
		}
	}

	public static readonly DependencyProperty BlurOpacityProperty = DependencyProperty.RegisterAttached(
		"BlurOpacity", typeof(byte), typeof(BlurPopup), new PropertyMetadata((byte)255, BlurOpacityProperty_OnChanged));

	private static void BlurOpacityProperty_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var popup = (Popup)d;
		if (HwndDictionary.TryGetValue(popup, out var hwnd)) {
			InteropMethods.SetLayeredWindowAttributes(hwnd, 0, (byte)e.NewValue, InteropMethods.LayeredWindowFlags.Alpha);
		}
	}

	public static void SetBlurOpacity(DependencyObject element, byte value) {
		element.SetValue(BlurOpacityProperty, value);
	}

	public static byte GetBlurOpacity(DependencyObject element) {
		return (byte)element.GetValue(BlurOpacityProperty);
	}

	private static readonly Dictionary<Popup, IntPtr> HwndDictionary = new();

	private static void Popup_OnOpened(object sender, EventArgs e) {
		var popup = (Popup)sender;
		if (PresentationSource.FromVisual(popup.Child) is HwndSource hwnd) {
			InteropMethods.EnableRoundCorner(hwnd.Handle);
			InteropMethods.EnableAcrylic(hwnd.Handle, InteropMethods.IsDarkMode);
			InteropMethods.EnableShadows(hwnd.Handle);
			HwndDictionary[popup] = hwnd.Handle;
		}
	}

	private static void Popup_OnClosed(object sender, EventArgs e) {
		HwndDictionary.Remove((Popup)sender);
	}
}