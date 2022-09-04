using System.Windows;
using System.Windows.Media;

namespace SharpSvgImage.Svg.PaintServer
{
    public class InheritPaintServer : PaintServer
    {
        public InheritPaintServer(PaintServerManager owner)
            : base(owner)
        {
        }

        public override Brush GetBrush(double opacity, Svg svg, SvgRender svgRender, Rect bounds)
        {
            return null;
        }
    }
}
