using System.Windows;
using System.Windows.Media;

namespace HandyControl.Controls {
	public class IconElement {
		public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
			"Icon", typeof(object), typeof(IconElement), new PropertyMetadata(default(object)));

		public static void SetIcon(DependencyObject element, object value) {
			element.SetValue(IconProperty, value);
		}

		public static object GetIcon(DependencyObject element)
			=> element.GetValue(IconProperty);

		public static readonly DependencyProperty WidthProperty = DependencyProperty.RegisterAttached(
			"Width", typeof(double), typeof(IconElement), new PropertyMetadata(double.NaN));

		public static void SetWidth(DependencyObject element, double value)
			=> element.SetValue(WidthProperty, value);

		public static double GetWidth(DependencyObject element)
			=> (double)element.GetValue(WidthProperty);

		public static readonly DependencyProperty HeightProperty = DependencyProperty.RegisterAttached(
			"Height", typeof(double), typeof(IconElement), new PropertyMetadata(double.NaN));

		public static void SetHeight(DependencyObject element, double value)
			=> element.SetValue(HeightProperty, value);

		public static double GetHeight(DependencyObject element)
			=> (double)element.GetValue(HeightProperty);

		public static readonly DependencyProperty PositionProperty = DependencyProperty.RegisterAttached(
			"Position", typeof(Position), typeof(IconElement), new PropertyMetadata(Position.Left));

		public static void SetPosition(DependencyObject element, Position value)
			=> element.SetValue(PositionProperty, value);

		public static Position GetPosition(DependencyObject element)
			=> (Position)element.GetValue(PositionProperty);

		public enum Position {
			Left,
			Top,
			Right,
			Bottom
		}
	}
}
