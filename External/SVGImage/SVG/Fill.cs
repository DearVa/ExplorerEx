using System.Windows;
using System.Windows.Media;
using SharpSvgImage.Svg.PaintServer;
using SharpSvgImage.Svg.Shapes;

namespace SharpSvgImage.Svg
{
    public class Fill
    {
        public enum eFillRule
        {
            nonzero,
            evenodd
        }

        public eFillRule FillRule { get; set;}

        public string PaintServerKey {get; set;}

        public double Opacity {get; set;}

        public Fill(Svg svg)
        {
            this.FillRule = eFillRule.nonzero;
            this.Opacity = 100;
        }

        public bool IsEmpty(Svg svg)
        {
            if (svg == null) return true;

            if (!svg.PaintServers.ContainsServer(this.PaintServerKey))
            {
                return true;
            }
            return svg.PaintServers.GetServer(this.PaintServerKey) == null;
        }

        public Brush FillBrush(Svg svg, SvgRender svgRender, Shape shape, double elementOpacity, Rect bounds)
        {
            var paintServer = svg.PaintServers.GetServer(this.PaintServerKey);
            if(paintServer != null)
            {
                if(paintServer is CurrentColorPaintServer)
                {
                    var shapePaintServer = svg.PaintServers.GetServer(shape.PaintServerKey);
                    if(shapePaintServer != null)
                    {
                        return shapePaintServer.GetBrush(this.Opacity * elementOpacity, svg, svgRender, bounds);

                    }
                }
                if (paintServer is InheritPaintServer)
                {
                    var p = shape.RealParent ?? shape.Parent;
                    while (p != null)
                    {
                        if(p.Fill != null)
                        {
                            var checkPaintServer = svg.PaintServers.GetServer(p.Fill.PaintServerKey);
                            if(!(checkPaintServer is InheritPaintServer))
                            {
                                return checkPaintServer.GetBrush(this.Opacity * elementOpacity, svg, svgRender, bounds);
                            }
                        }
                        p = p.RealParent ?? p.Parent;
                    }
                    return null;
                }
                return paintServer.GetBrush(this.Opacity * elementOpacity, svg, svgRender, bounds);
            }
            return null;
        }
    }
}
