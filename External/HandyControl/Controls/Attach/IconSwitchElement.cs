using System.Windows;
using System.Windows.Media;

namespace HandyControl.Controls
{
    public class IconSwitchElement : IconElement
    {
        public static readonly DependencyProperty IconSelectedProperty = DependencyProperty.RegisterAttached(
            "IconSelected", typeof(Geometry), typeof(IconSwitchElement), new PropertyMetadata(default(Geometry)));

        public static void SetIconSelected(DependencyObject element, Geometry value)
        {
            element.SetValue(IconSelectedProperty, value);
        }

        public static Geometry GetIconSelected(DependencyObject element)
        {
            return (Geometry) element.GetValue(IconSelectedProperty);
        }
    }
}
