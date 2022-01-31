using System.Windows;
using System.Windows.Media;

namespace HandyControl.Controls {
	public class IconElement {
		public static readonly DependencyProperty GeometryProperty = DependencyProperty.RegisterAttached(
			"Geometry", typeof(Geometry), typeof(IconElement), new PropertyMetadata(default(Geometry), GeometryPropertyChangedCallback));

		private static void GeometryPropertyChangedCallback(DependencyObject element, DependencyPropertyChangedEventArgs e) {
			SetIsAnyGeometry(element, e.NewValue != null);
		}

		public static void SetGeometry(DependencyObject element, Geometry value) {
			element.SetValue(GeometryProperty, value);
			SetIsAnyGeometry(element, value != null);
		}

		public static Geometry GetGeometry(DependencyObject element)
			=> (Geometry)element.GetValue(GeometryProperty);

		public static readonly DependencyProperty IsAnyGeometryProperty = DependencyProperty.RegisterAttached(
			"IsAnyGeometry", typeof(bool), typeof(IconElement), new PropertyMetadata(default(bool)));

		public static void SetIsAnyGeometry(DependencyObject element, bool value)
			=> element.SetValue(IsAnyGeometryProperty, value);

		public static bool GetIsAnyGeometry(DependencyObject element)
			=> element.GetValue(GeometryProperty) != null;

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
