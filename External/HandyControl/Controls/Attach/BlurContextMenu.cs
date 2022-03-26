using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using HandyControl.Tools.Interop;

namespace HandyControl.Controls;

public static class BlurContextMenu {
	public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
		"Enabled", typeof(bool), typeof(BlurContextMenu), new PropertyMetadata(false, Enabled_OnChanged));

	public static void SetEnabled(DependencyObject element, bool value) {
		element.SetValue(EnabledProperty, value);
	}

	public static bool GetEnabled(DependencyObject element) {
		return (bool)element.GetValue(EnabledProperty);
	}

	private static void Enabled_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if (d is ContextMenu contextMenu) {
			if (e.NewValue is true) {
				contextMenu.Opened += ContextMenu_OnOpened;
			} else {
				contextMenu.Opened -= ContextMenu_OnOpened;
			}
		}
	}

	private static void ContextMenu_OnOpened(object sender, EventArgs e) {
		if (sender is ContextMenu contextMenu) {
			if (PresentationSource.FromVisual(contextMenu) is HwndSource hwnd) {
				InteropMethods.EnableRoundCorner(hwnd.Handle);
				InteropMethods.EnableAcrylic(hwnd.Handle);
				InteropMethods.EnableShadows(hwnd.Handle);
			}
		}
	}
}