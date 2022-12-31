using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExplorerEx.Views.Controls; 

public class FluentExpander : Expander {
	public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
		nameof(Icon), typeof(ImageSource), typeof(FluentExpander), new PropertyMetadata(default(ImageSource)));

	public ImageSource Icon {
		get => (ImageSource)GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
		nameof(Description), typeof(string), typeof(FluentExpander), new PropertyMetadata(default(string)));

	public string Description {
		get => (string)GetValue(DescriptionProperty);
		set => SetValue(DescriptionProperty, value);
	}

	public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
		nameof(ItemsSource), typeof(ObservableCollection<FluentMenuItem>), typeof(FluentExpander), new PropertyMetadata(default(ObservableCollection<FluentMenuItem>)));

	public ObservableCollection<FluentMenuItem> ItemsSource {
		get => (ObservableCollection<FluentMenuItem>)GetValue(ItemsSourceProperty);
		set => SetValue(ItemsSourceProperty, value);
	}
}