﻿using System.Windows.Media.Effects;
using System.Xml;
using SVGImage.SVG;
using SVGImage.SVG.Shapes;

namespace DotNetProjects.SVGImage.SVG.Shapes.Filter
{
    public class FilterFeGaussianBlur : FilterBaseFe
    {
        public string In { get; set; }

        public double StdDeviationX { get; set; }

        public double StdDeviationY { get; set; }

        public FilterFeGaussianBlur(global::SVGImage.SVG.SVG svg, XmlNode node, Shape parent)
            : base(svg, node, parent)
        {
            StdDeviationX = StdDeviationY = XmlUtil.AttrValue(node, "stdDeviation", 0);
        }

        public override BitmapEffect GetBitmapEffect()
        {
            return new BlurBitmapEffect() {Radius = StdDeviationX};
        }
    }
}
