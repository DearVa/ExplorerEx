using System.Windows;
using System.Windows.Controls;

namespace ExplorerEx.Views.Controls; 

public class FluentMenuItem : MenuItem {
	public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
		nameof(Description), typeof(string), typeof(FluentMenuItem), new PropertyMetadata(default(string)));

	public string Description {
		get => (string)GetValue(DescriptionProperty);
		set => SetValue(DescriptionProperty, value);
	}

	public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
		nameof(Content), typeof(object), typeof(FluentMenuItem), new PropertyMetadata(default(object)));

	public object Content {
		get => GetValue(ContentProperty);
		set => SetValue(ContentProperty, value);
	}
}