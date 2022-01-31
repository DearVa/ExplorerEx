using System.Xml;
using SVGImage.SVG.Shapes;

namespace DotNetProjects.SVGImage.SVG.Animation
{
    public class AnimateMotion : AnimationBase
    {
        public AnimateMotion(global::SVGImage.SVG.SVG svg, XmlNode node, Shape parent)
            : base(svg, node, parent)
        {
        }
    }
}
