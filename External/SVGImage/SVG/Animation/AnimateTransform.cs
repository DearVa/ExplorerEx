using System;
using System.Xml;
using SharpSvgImage.Svg;
using SharpSvgImage.Svg.Shapes;

namespace DotNetProjects.SVGImage.SVG.Animation
{
    public class AnimateTransform : AnimationBase
    {
        public string AttributeName { get; set; }

        public AnimateTransformType Type { get; set; }

        public string From { get; set; }

        public string To { get; set; }

        public string Values { get; set; }

        public string RepeatType { get; set; }

        public AnimateTransform(global::SharpSvgImage.Svg.Svg svg, XmlNode node, Shape parent)
            : base(svg, node, parent)
        {
            this.Type = (AnimateTransformType)Enum.Parse(typeof(AnimateTransformType), XmlUtil.AttrValue(node, "type", "translate"), true);
            this.From = XmlUtil.AttrValue(node, "from", null);
            this.To = XmlUtil.AttrValue(node, "to", null);
            this.AttributeName = XmlUtil.AttrValue(node, "attributeName", null);
            this.RepeatType = XmlUtil.AttrValue(node, "repeatCount", "indefinite");
            this.Values = XmlUtil.AttrValue(node, "values", null);
        }
    }
}
