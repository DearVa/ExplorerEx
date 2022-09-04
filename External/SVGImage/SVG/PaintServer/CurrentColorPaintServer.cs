using System.Windows;
using System.Windows.Media;

namespace SharpSvgImage.Svg.PaintServer
{
    public class CurrentColorPaintServer : PaintServer
    {
        public CurrentColorPaintServer(PaintServerManager owner)
            : base(owner)
        {
        }

        public override Brush GetBrush(double opacity, Svg svg, SvgRender svgRender, Rect bounds)
        {
            return null;
        }
    }
}
