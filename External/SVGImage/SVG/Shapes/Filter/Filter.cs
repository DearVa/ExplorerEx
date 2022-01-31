using System.Windows.Media.Effects;
using System.Xml;
using SVGImage.SVG.Shapes;

namespace DotNetProjects.SVGImage.SVG.Shapes.Filter
{
    public class Filter : Group
    {
        public Filter(global::SVGImage.SVG.SVG svg, XmlNode node, Shape parent)
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
