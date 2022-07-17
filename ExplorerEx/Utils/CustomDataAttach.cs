using System.Windows;

namespace ExplorerEx.Utils;

/// <summary>
/// 用于往组件上随意附加object
/// </summary>
public class CustomDataAttach {
	public static readonly DependencyProperty DataProperty = DependencyProperty.RegisterAttached(
		"Data", typeof(object), typeof(CustomDataAttach), new PropertyMetadata(null));
}