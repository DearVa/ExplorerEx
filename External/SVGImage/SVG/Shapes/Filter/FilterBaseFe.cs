using System.Windows.Media.Effects;
using System.Xml;
using SVGImage.SVG.Shapes;

namespace DotNetProjects.SVGImage.SVG.Shapes.Filter
{
    public abstract class FilterBaseFe : Shape
    {
        public FilterBaseFe(global::SVGImage.SVG.SVG svg, XmlNode node, Shape parent)
            : base(svg, node, parent)
        {

        }

        public abstract BitmapEffect GetBitmapEffect();
    }
}

