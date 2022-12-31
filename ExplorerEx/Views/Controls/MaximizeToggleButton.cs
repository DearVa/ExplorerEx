using System.Windows;
using System.Windows.Controls.Primitives;

namespace ExplorerEx.Views.Controls; 

internal class MaximizeToggleButton : ToggleButton {
	public new static readonly DependencyProperty IsMouseOverProperty = DependencyProperty.Register(
		nameof(IsMouseOver), typeof(bool), typeof(MaximizeToggleButton), new PropertyMetadata(default(bool)));

	public new bool IsMouseOver {
		get => (bool)GetValue(IsMouseOverProperty);
		set => SetValue(IsMouseOverProperty, value);
	}

	public bool IsMouseLeftButtonDown { get; set; }
}