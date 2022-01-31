using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ExplorerEx.View; 

/// <summary>
/// 可以根据圆角来裁剪内容
/// </summary>
public class ClippingBorder : Border {
	private readonly VisualBrush mask;

	public ClippingBorder() {
		var clipBorder = new Border {
			Background = Brushes.Black,
			BorderBrush = Brushes.Transparent,
			SnapsToDevicePixels = true,
		};
		clipBorder.SetBinding(CornerRadiusProperty, new Binding {
			Mode = BindingMode.OneWay,
			Path = new PropertyPath("CornerRadius"),
			Source = this
		});
		clipBorder.SetBinding(BorderThicknessProperty, new Binding {
			Mode = BindingMode.OneWay,
			Path = new PropertyPath("BorderThickness"),
			Source = this
		});
		clipBorder.SetBinding(HeightProperty, new Binding {
			Mode = BindingMode.OneWay,
			Path = new PropertyPath("ActualHeight"),
			Source = this
		});
		clipBorder.SetBinding(WidthProperty, new Binding {
			Mode = BindingMode.OneWay,
			Path = new PropertyPath("ActualWidth"),
			Source = this
		});

		mask = new VisualBrush(clipBorder);
	}

	public override UIElement Child {
		get => base.Child;
		set {
			base.Child = value;
			value.OpacityMask = mask;
		}
	}
}