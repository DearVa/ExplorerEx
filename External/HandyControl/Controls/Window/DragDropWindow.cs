using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using HandyControl.Tools.Interop;

namespace HandyControl.Controls;

/// <summary>
/// 拖放一个控件时，可以Show这个窗口，是一个跟随鼠标的控件
/// </summary>
public sealed class DragDropWindow : System.Windows.Window {
	private Point cursorPoint;

	private DragDropWindow(FrameworkElement element, Point cursorPoint, double opacity, bool useVisualBrush) {
		this.cursorPoint = cursorPoint;
		Opacity = opacity;
		Topmost = true;
		ShowInTaskbar = false;
		IsHitTestVisible = false;
		AllowsTransparency = true;
		WindowStyle = WindowStyle.None;
		ResizeMode = ResizeMode.NoResize;
		if (useVisualBrush) {
			Background = new VisualBrush(element);
			Width = element.RenderSize.Width;
			Height = element.RenderSize.Height;
		} else {
			Background = null;
			Content = element;
			SizeToContent = SizeToContent.WidthAndHeight;
		}
		BorderBrush = null;
		var mousePos = InteropMethods.GetCursorPos();
		Left = mousePos.X - cursorPoint.X;
		Top = mousePos.Y - cursorPoint.Y;
		InteropMethods.SetWindowLongPtr32(new WindowInteropHelper(this).EnsureHandle(), -20, (IntPtr)0x20);  // 设置鼠标穿透
	}

	/// <summary>
	/// 创建一个DragDropWindow并显示
	/// </summary>
	/// <param name="element">要显示的元素</param>
	/// <param name="cursorPoint">鼠标所处的位置</param>
	/// <param name="opacity"></param>
	/// <param name="useVisualBrush">如果为true，那就使用VisualBrush，相当于显示element的副本；如果为false，那就是把element当做窗口的Content，此时要确保element没有父级</param>
	public static DragDropWindow Show(FrameworkElement element, Point cursorPoint, double opacity = 1d, bool useVisualBrush = true) {
		var window = new DragDropWindow(element, cursorPoint, opacity, useVisualBrush);
		window.Show();
		return window;
	}

	public void MoveWithCursor() {
		var mousePos = InteropMethods.GetCursorPos();
		Left = mousePos.X - cursorPoint.X;
		Top = mousePos.Y - cursorPoint.Y;
	}
}