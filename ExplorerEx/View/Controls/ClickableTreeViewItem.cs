using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 可以点击的TreeViewItem，在文字部分光标会显示成手型
/// </summary>
public class ClickableTreeViewItem : TreeViewItem {
	public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
		"Icon", typeof(ImageSource), typeof(ClickableTreeViewItem), new PropertyMetadata(default(ImageSource)));

	public ImageSource Icon {
		get => (ImageSource)GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}
}