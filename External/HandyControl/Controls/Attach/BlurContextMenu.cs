using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media.Animation;
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
				contextMenu.Closed += ContextMenu_OnClosed;
			} else {
				contextMenu.Opened -= ContextMenu_OnOpened;
				contextMenu.Closed -= ContextMenu_OnClosed;
			}
		}
	}

	public static readonly DependencyProperty OpacityProperty = DependencyProperty.RegisterAttached(
		"Opacity", typeof(byte), typeof(BlurContextMenu), new PropertyMetadata((byte)255, OpacityProperty_OnChanged));

	private static void OpacityProperty_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var contextMenu = (ContextMenu)d;
		if (HwndDictionary.TryGetValue(contextMenu, out var hwnd)) {
			InteropMethods.SetLayeredWindowAttributes(hwnd, 0, (byte)e.NewValue, InteropMethods.LayeredWindowFlags.Alpha);
		}
	}

	public static void SetOpacity(DependencyObject element, byte value) {
		element.SetValue(OpacityProperty, value);
	}

	public static byte GetOpacity(DependencyObject element) {
		return (byte)element.GetValue(OpacityProperty);
	}

	private static readonly Dictionary<ContextMenu, IntPtr> HwndDictionary = new();

	private static void ContextMenu_OnOpened(object sender, EventArgs e) {
		var contextMenu = (ContextMenu)sender;
		if (PresentationSource.FromVisual(contextMenu) is HwndSource src) {
			InteropMethods.EnableRoundCorner(src.Handle);
			InteropMethods.EnableAcrylic(src.Handle, InteropMethods.IsDarkMode);
			InteropMethods.EnableShadows(src.Handle);
			HwndDictionary[contextMenu] = src.Handle;
		}
	}

	private static void ContextMenu_OnClosed(object sender, RoutedEventArgs e) {
		HwndDictionary.Remove((ContextMenu)sender);
	}
}