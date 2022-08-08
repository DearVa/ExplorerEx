using System.Windows.Controls;

namespace HandyControl.Data;

/// <summary>
///     装箱后的值类型（用于提高效率）
/// </summary>
internal static class ValueBoxes {
	internal static readonly object TrueBox = true;

	internal static readonly object FalseBox = false;

	internal static readonly object Double0Box = .0;

	internal static readonly object Double01Box = .1;

	internal static readonly object Double1Box = 1.0;

	internal static readonly object Double10Box = 10.0;

	internal static readonly object Double20Box = 20.0;

	internal static readonly object Double100Box = 100.0;

	internal static readonly object Double200Box = 200.0;

	internal static readonly object Double300Box = 300.0;

	internal static readonly object DoubleNeg1Box = -1.0;

	internal static readonly object Int0Box = 0;

	internal static readonly object Int1Box = 1;

	internal static readonly object Int2Box = 2;

	internal static readonly object Int5Box = 5;

	internal static readonly object Int99Box = 99;

	internal static object BooleanBox(bool value) => value ? TrueBox : FalseBox;

	internal static readonly object VerticalBox = Orientation.Vertical;

	internal static readonly object HorizontalBox = Orientation.Horizontal;

	internal static object OrientationBox(Orientation value) =>
		value == Orientation.Horizontal ? HorizontalBox : VerticalBox;
}