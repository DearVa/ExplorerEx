using System.Windows.Media.Effects;
using System.Xml;
using SharpSvgImage.Svg.Shapes;

namespace DotNetProjects.SVGImage.SVG.Shapes.Filter
{
    public class Filter : Group
    {
        public Filter(global::SharpSvgImage.Svg.Svg svg, XmlNode node, Shape parent)
            : base(svg, node, parent)
        {
           
        }

        public BitmapEffect GetBitmapEffect()
        {
            var beg = new BitmapEffectGroup();
            foreach (FilterBaseFe element in this.Elements)
            {
                beg.Children.Add(element.GetBitmapEffect());
            }

            return beg;
        }
    }
}
