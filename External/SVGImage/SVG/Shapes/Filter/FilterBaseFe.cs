using System.Windows.Media.Effects;
using System.Xml;
using SharpSvgImage.Svg.Shapes;

namespace DotNetProjects.SVGImage.SVG.Shapes.Filter
{
    public abstract class FilterBaseFe : Shape
    {
        public FilterBaseFe(global::SharpSvgImage.Svg.Svg svg, XmlNode node, Shape parent)
            : base(svg, node, parent)
        {

        }

        public abstract BitmapEffect GetBitmapEffect();
    }
}

