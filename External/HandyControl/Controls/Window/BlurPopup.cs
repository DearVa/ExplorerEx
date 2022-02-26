using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using static HandyControl.Tools.Interop.InteropMethods;

namespace HandyControl.Controls; 

/// <summary>
/// 一个背景亚克力模糊的Popup，目前只支持Win8以上
/// </summary>
public class BlurPopup : Popup {
	public BlurPopup() {
		DependencyPropertyDescriptor.FromProperty(IsOpenProperty, typeof(Popup)).AddValueChanged(this, IsOpened_OnChanged);
	}

	private void IsOpened_OnChanged(object sender, EventArgs e) {
		var popup = (BlurPopup)sender;
		if (popup.IsOpen) {
			var hwnd = ((HwndSource)PresentationSource.FromVisual(Child))!.Handle;
			EnableAcrylic(hwnd);
			if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) {
				var attr = DwmWindowCornerPreference.Round;
				DwmSetWindowAttribute(hwnd, DwmWindowAttribute.WindowCornerPreference, ref attr, sizeof(uint));
			}
		}
	}
}