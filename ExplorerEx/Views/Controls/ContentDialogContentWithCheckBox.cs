using System.Windows;
using System.Windows.Controls;

namespace ExplorerEx.Views.Controls; 

internal class ContentDialogContentWithCheckBox : ContentControl {
	public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
		nameof(IsChecked), typeof(bool), typeof(ContentDialogContentWithCheckBox), new PropertyMetadata(default(bool)));

	public bool IsChecked {
		get => (bool)GetValue(IsCheckedProperty);
		set => SetValue(IsCheckedProperty, value);
	}
}