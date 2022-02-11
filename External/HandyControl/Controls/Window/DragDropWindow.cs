using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace HandyControl.Controls;

/// <summary>
/// 拖放一个控件时，可以Show这个窗口，是一个跟随鼠标的控件
/// </summary>
public sealed class DragDropWindow : Window {
	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(ref Win32Point pt);

	[StructLayout(LayoutKind.Sequential)]
	private struct Win32Point {
		public int X;
		public int Y;
	}

	[DllImport("user32")]
	private static extern uint SetWindowLong(IntPtr hwnd, int nIndex, int newLong);

	private Point cursorPoint;

	/// <summary>
	/// 创建一个DragDropWindow并显示
	/// </summary>
	/// <param name="visual">要显示的元素</param>
	/// <param name="cursorPoint">鼠标所处的位置</param>
	/// <param name="opacity"></param>
	/// <param name="useVisualBrush">如果为true，那就使用VisualBrush，相当于显示element的副本；如果为false，那就是把element当做窗口的Content，此时要确保element没有父级</param>
	public DragDropWindow(Visual visual, Point cursorPoint, double opacity = 1d, bool useVisualBrush = true) {
		this.cursorPoint = cursorPoint;
		Topmost = true;
		ShowInTaskbar = false;
		IsHitTestVisible = false;
		AllowsTransparency = true;
		WindowStyle = WindowStyle.None;
		ResizeMode = ResizeMode.NoResize;
		Opacity = opacity;
		AllowDrop = false;
		SizeToContent = SizeToContent.WidthAndHeight;
		if (useVisualBrush) {
			Background = new VisualBrush(visual);
		} else {
			Background = null;
			Content = visual;
		}
		BorderBrush = null;
		var mousePos = new Win32Point();
		GetCursorPos(ref mousePos);
		Left = mousePos.X - cursorPoint.X;
		Top = mousePos.Y - cursorPoint.Y;
		SetWindowLong(new WindowInteropHelper(this).EnsureHandle(), -20, 0x20);  // 设置鼠标穿透
		Show();
	}

	public void MoveWithCursor() {
		var mousePos = new Win32Point();
		GetCursorPos(ref mousePos);
		Left = mousePos.X - cursorPoint.X;
		Top = mousePos.Y - cursorPoint.Y;
	}
}