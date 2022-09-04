using System.Xml;
using SharpSvgImage.Svg.Shapes;

namespace DotNetProjects.SVGImage.SVG.Animation
{
    public class AnimateMotion : AnimationBase
    {
        public AnimateMotion(global::SharpSvgImage.Svg.Svg svg, XmlNode node, Shape parent)
            : base(svg, node, parent)
        {
        }
    }
}
