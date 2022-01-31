using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Collections.Generic;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

using DotNetProjects.SVGImage.SVG;
using DotNetProjects.SVGImage.SVG.Animation;
using DotNetProjects.SVGImage.SVG.FileLoaders;

using SVGImage.SVG.Shapes;

namespace SVGImage.SVG
{
    /// <summary>
    /// This is the class that creates the WPF Drawing object based on the information from the <see cref="SVG"/> class.
    /// </summary>
    /// <seealso href="http://www.w3.org/TR/SVGTiny12/"/>
    /// <seealso href="http://commons.oreilly.com/wiki/index.php/SVG_Essentials"/>
    public class SVGRender
    {
        public SVGRender() : this(FileSystemLoader.Instance)
        {
        }

        public SVGRender(IExternalFileLoader fileLoader)
        {
            ExternalFileLoader = (fileLoader != null) ? fileLoader : FileSystemLoader.Instance;
        }

        public SVG SVG { get; private set; }

        public bool UseAnimations { get; set; }

        public Color? OverrideColor { get; set; }
       
        public double? OverrideStrokeWidth { get; set; }

        private Dictionary<string, Brush> m_customBrushes;

        public Dictionary<string, Brush> CustomBrushes
        {
            get => m_customBrushes;
            set
            {
                m_customBrushes = value;
                if (this.SVG != null)
                {
                    this.SVG.CustomBrushes = value;
                }
            }
        }

        public IExternalFileLoader ExternalFileLoader { get; set; }

        public DrawingGroup LoadDrawing(string filename)
        {
            this.SVG = new SVG(filename, ExternalFileLoader);
            return this.CreateDrawing(this.SVG);
        }

        public DrawingGroup LoadXmlDrawing(string fileXml)
        {
            this.SVG = new SVG(this.ExternalFileLoader);
            this.SVG.LoadXml(fileXml);

            return this.CreateDrawing(this.SVG);
        }

        public DrawingGroup LoadDrawing(Uri fileUri)
        {
            this.SVG = new SVG(this.ExternalFileLoader);
            this.SVG.Load(fileUri);

            return this.CreateDrawing(this.SVG);
        }

        public DrawingGroup LoadDrawing(TextReader txtReader)
        {
            this.SVG = new SVG(this.ExternalFileLoader);
            this.SVG.Load(txtReader);

            return this.CreateDrawing(this.SVG);
        }

        public DrawingGroup LoadDrawing(XmlReader xmlReader)
        {
            this.SVG = new SVG(this.ExternalFileLoader);
            this.SVG.Load(xmlReader);

            return this.CreateDrawing(this.SVG);
        }

        public DrawingGroup LoadDrawing(Stream stream)
        {
            this.SVG = new SVG(stream, ExternalFileLoader);

            return this.CreateDrawing(this.SVG);
        }

        public DrawingGroup CreateDrawing(SVG svg)
        {
            return this.LoadGroup(svg.Elements, svg.ViewBox, false);
        }

        public DrawingGroup CreateDrawing(Shape shape)
        {
            return this.LoadGroup(new Shape[] { shape }, null, false);
        }

        private GeometryDrawing NewDrawingItem(Shape shape, Geometry geometry)
        {
            shape.geometryElement = geometry;
            GeometryDrawing item = new GeometryDrawing();
            Stroke stroke = shape.Stroke;
            if (stroke != null)
            {
                if(OverrideStrokeWidth.HasValue)
                {
                    stroke.Width = OverrideStrokeWidth.Value;
                }
                var brush = stroke.StrokeBrush(this.SVG, this, shape, shape.Opacity, geometry.Bounds);
                if (OverrideColor != null)
                    brush = new SolidColorBrush(Color.FromArgb((byte)(255 * shape.Opacity), OverrideColor.Value.R, OverrideColor.Value.G, OverrideColor.Value.B));
                item.Pen = new Pen(brush, stroke.Width);
                if (stroke.StrokeArray != null)
                {
                    item.Pen.DashCap = PenLineCap.Flat;
                    DashStyle ds = new DashStyle();
                    double scale = 1 / stroke.Width;
                    foreach (int dash in stroke.StrokeArray) ds.Dashes.Add(dash * scale);
                    item.Pen.DashStyle = ds;
                }
                switch (stroke.LineCap)
                {
                    case Stroke.eLineCap.butt:
                        item.Pen.StartLineCap = PenLineCap.Flat;
                        item.Pen.EndLineCap = PenLineCap.Flat;
                        break;
                    case Stroke.eLineCap.round:
                        item.Pen.StartLineCap = PenLineCap.Round;
                        item.Pen.EndLineCap = PenLineCap.Round;
                        break;
                    case Stroke.eLineCap.square:
                        item.Pen.StartLineCap = PenLineCap.Square;
                        item.Pen.EndLineCap = PenLineCap.Square;
                        break;
                }
                switch (stroke.LineJoin)
                {
                    case Stroke.eLineJoin.round:
                        item.Pen.LineJoin = PenLineJoin.Round;
                        break;
                    case Stroke.eLineJoin.miter:
                        item.Pen.LineJoin = PenLineJoin.Miter;
                        break;
                    case Stroke.eLineJoin.bevel:
                        item.Pen.LineJoin = PenLineJoin.Bevel;
                        break;
                }
            }

            if (shape.Fill == null)
            {
                item.Brush = Brushes.Black;
                if (OverrideColor != null)
                    item.Brush = new SolidColorBrush(Color.FromArgb((byte)(255 * shape.Opacity), OverrideColor.Value.R, OverrideColor.Value.G, OverrideColor.Value.B));
                GeometryGroup g = new GeometryGroup();
                g.FillRule = FillRule.Nonzero;
                g.Children.Add(geometry);
                geometry = g;
            }
            else if (shape.Fill != null)
            {
                item.Brush = shape.Fill.FillBrush(this.SVG, this, shape, shape.Opacity, geometry.Bounds);
                if (OverrideColor != null)
                    item.Brush = new SolidColorBrush(Color.FromArgb((byte)(255 * shape.Opacity), OverrideColor.Value.R, OverrideColor.Value.G, OverrideColor.Value.B));
                GeometryGroup g = new GeometryGroup();
                g.FillRule = FillRule.Nonzero;
                if (shape.Fill.FillRule == Fill.eFillRule.evenodd) g.FillRule = FillRule.EvenOdd;
                g.Children.Add(geometry);
                geometry = g;
            }
            //if (shape.Transform != null) geometry.Transform = shape.Transform;

            // for debugging, if neither stroke or fill is set then set default pen
            //if (shape.Fill == null && shape.Stroke == null)
            //	item.Pen = new Pen(Brushes.Blue, 1);

            item.Geometry = geometry;
            return item;
        }

        

        private class ControlLine
        {
            public Point Ctrl { get; private set; }

            public Point Start { get; private set; }

            public ControlLine(Point start, Point ctrl)
            {
                this.Start = start;
                this.Ctrl = ctrl;
            }

            public GeometryDrawing Draw()
            {
                double size = 0.2;
                GeometryDrawing item = new GeometryDrawing();
                item.Brush = Brushes.Red;
                GeometryGroup g = new GeometryGroup();

                item.Pen = new Pen(Brushes.LightGray, size / 2);
                g.Children.Add(new LineGeometry(this.Start, this.Ctrl));

                g.Children.Add(new RectangleGeometry(new Rect(this.Start.X - size / 2, this.Start.Y - size / 2, size, size)));
                g.Children.Add(new EllipseGeometry(this.Ctrl, size, size));

                item.Geometry = g;
                return item;
            }
        }

        internal DrawingGroup LoadGroup(IList<Shape> elements, Rect? viewBox, bool isSwitch)
        {
            List<ControlLine> debugPoints = new List<ControlLine>();
            DrawingGroup grp = new DrawingGroup();
            if (viewBox.HasValue) grp.ClipGeometry = new RectangleGeometry(viewBox.Value);

            foreach (Shape shape in elements)
            {
                shape.RealParent = null;
                if (!shape.Display)
                {
                    continue;
                }

                if (isSwitch)
                {
                    if (grp.Children.Count > 0)
                    {
                        break;
                    }
                    if (!string.IsNullOrEmpty(shape.RequiredFeatures))
                    {
                        if (!SVGFeatures.Features.Contains(shape.RequiredFeatures))
                        {
                            continue;
                        }
                        if (!string.IsNullOrEmpty(shape.RequiredExtensions))
                        {
                            continue;
                        }
                    }
                }

                if (shape is AnimationBase)
                {
                    if (UseAnimations)
                    {
                        if (shape is AnimateTransform animateTransform)
                        {
                            if (animateTransform.Type == AnimateTransformType.Rotate)
                            {
                                var animation = new DoubleAnimation
                                {
                                    From = double.Parse(animateTransform.From),
                                    To = double.Parse(animateTransform.To),
                                    Duration = animateTransform.Duration
                                };
                                animation.RepeatBehavior = RepeatBehavior.Forever;
                                var r = new RotateTransform();
                                grp.Transform = r;
                                r.BeginAnimation(RotateTransform.AngleProperty, animation);
                            }
                        }
                        else if (shape is Animate animate)
                        {
                            var target = this.SVG.GetShape(animate.hRef);
                            var g = target.geometryElement;
                            //todo : rework this all, generalize it!
                            if (animate.AttributeName == "r")
                            {
                                var animation = new DoubleAnimationUsingKeyFrames() { Duration = animate.Duration };
                                foreach (var d in animate.Values.Split(';').Select(x => new LinearDoubleKeyFrame(double.Parse(x))))
                                {
                                    animation.KeyFrames.Add(d);
                                }
                                animation.RepeatBehavior = RepeatBehavior.Forever;

                                g.BeginAnimation(EllipseGeometry.RadiusXProperty, animation);
                                g.BeginAnimation(EllipseGeometry.RadiusYProperty, animation);
                            }
                            else if (animate.AttributeName == "cx")
                            {
                                var animation = new PointAnimationUsingKeyFrames() { Duration = animate.Duration };
                                foreach (var d in animate.Values.Split(';').Select(_ => new LinearPointKeyFrame(new Point(double.Parse(_), ((EllipseGeometry)g).Center.Y))))
                                {
                                    animation.KeyFrames.Add(d);
                                }
                                animation.RepeatBehavior = RepeatBehavior.Forever;
                                g.BeginAnimation(EllipseGeometry.CenterProperty, animation);
                            }
                            else if (animate.AttributeName == "cy")
                            {
                                var animation = new PointAnimationUsingKeyFrames() { Duration = animate.Duration };
                                foreach (var d in animate.Values.Split(';').Select(_ => new LinearPointKeyFrame(new Point(((EllipseGeometry)g).Center.X, double.Parse(_)))))
                                {
                                    animation.KeyFrames.Add(d);
                                }
                                animation.RepeatBehavior = RepeatBehavior.Forever;
                                g.BeginAnimation(EllipseGeometry.CenterProperty, animation);
                            }

                        }
                    }

                    continue;
                }

                if (shape is UseShape)
                {
                    UseShape useshape = shape as UseShape;
                    Shape currentUsedShape = this.SVG.GetShape(useshape.hRef);
                    if (currentUsedShape != null)
                    {
                        currentUsedShape.RealParent = useshape;
                        Shape oldparent = currentUsedShape.Parent;
                        DrawingGroup subgroup;
                        if (currentUsedShape is Group)
                            subgroup = this.LoadGroup(((Group)currentUsedShape).Elements, null, false);
                        else
                            subgroup = this.LoadGroup(new[]{ currentUsedShape }, null, false);
                        if (currentUsedShape.Clip != null)
                            subgroup.ClipGeometry = currentUsedShape.Clip.ClipGeometry;
                        subgroup.Transform = new TranslateTransform(useshape.X, useshape.Y);
                        if (useshape.Transform != null)
                            subgroup.Transform = new TransformGroup() {Children = new TransformCollection() { subgroup.Transform, useshape.Transform } };
                        grp.Children.Add(subgroup);
                        currentUsedShape.Parent = oldparent;
                    }
                    continue;
                }
                if (shape is Clip)
                {
                    DrawingGroup subgroup = this.LoadGroup((shape as Clip).Elements, null, false);
                    if (shape.Transform != null)
                        subgroup.Transform = shape.Transform;
                    grp.Children.Add(subgroup);
                    continue;
                }
                if (shape is Group groupShape)
                {
                    DrawingGroup subgroup = this.LoadGroup((shape as Group).Elements, null, groupShape.IsSwitch);
                    AddDrawingToGroup(grp, shape, subgroup);
                    continue;
                }
                if (shape is RectangleShape)
                {
                    RectangleShape r = shape as RectangleShape;
                    //RectangleGeometry rect = new RectangleGeometry(new Rect(r.X < 0 ? 0 : r.X, r.Y < 0 ? 0 : r.Y, r.X < 0 ? r.Width + r.X : r.Width, r.Y < 0 ? r.Height + r.Y : r.Height));
                    double dx     = r.X;
                    double dy     = r.Y;
                    double width  = r.Width;
                    double height = r.Height;
                    double rx     = r.RX;
                    double ry     = r.RY;
                    if (width <= 0 || height <= 0)
                    {
                        continue;
                    }
                    if (rx <= 0 && ry > 0)
                    {
                        rx = ry;
                    }
                    else if (rx > 0 && ry <= 0)
                    {
                        ry = rx;
                    }

                    RectangleGeometry rect = new RectangleGeometry(new Rect(dx, dy, width, height), rx, ry);
                    var di = this.NewDrawingItem(shape, rect);
                    AddDrawingToGroup(grp, shape, di);
                    continue;
                }
                if (shape is LineShape)
                {
                    LineShape r = shape as LineShape;
                    LineGeometry line = new LineGeometry(r.P1, r.P2);
                    var di = this.NewDrawingItem(shape, line);
                    AddDrawingToGroup(grp, shape, di);
                    continue;
                }
                if (shape is PolylineShape)
                {
                    PolylineShape r = shape as PolylineShape;
                    PathGeometry path = new PathGeometry();
                    PathFigure p = new PathFigure();
                    path.Figures.Add(p);
                    p.IsClosed = false;
                    p.StartPoint = r.Points[0];
                    for (int index = 1; index < r.Points.Length; index++)
                    {
                        p.Segments.Add(new LineSegment(r.Points[index], true));
                    }
                    var di = this.NewDrawingItem(shape, path);
                    AddDrawingToGroup(grp, shape, di);
                    continue;
                }
                if (shape is PolygonShape)
                {
                    PolygonShape r = shape as PolygonShape;
                    PathGeometry path = new PathGeometry();
                    PathFigure p = new PathFigure();
                    path.Figures.Add(p);
                    p.IsClosed = true;
                    p.StartPoint = r.Points[0];
                    for (int index = 1; index < r.Points.Length; index++)
                    {
                        p.Segments.Add(new LineSegment(r.Points[index], true));
                    }
                    var di = this.NewDrawingItem(shape, path);
                    AddDrawingToGroup(grp, shape, di);
                    continue;
                }
                if (shape is CircleShape)
                {
                    CircleShape r = shape as CircleShape;
                    EllipseGeometry c = new EllipseGeometry(new Point(r.CX, r.CY), r.R, r.R);
                    var di = this.NewDrawingItem(shape, c);
                    AddDrawingToGroup(grp, shape, di);
                    continue;
                }
                if (shape is EllipseShape)
                {
                    EllipseShape r = shape as EllipseShape;
                    EllipseGeometry c = new EllipseGeometry(new Point(r.CX, r.CY), r.RX, r.RY);
                    var di = this.NewDrawingItem(shape, c);
                    AddDrawingToGroup(grp, shape, di);
                    continue;
                }
                if (shape is ImageShape)
                {
                    ImageShape image = shape as ImageShape;
                    ImageDrawing i = new ImageDrawing(image.ImageSource, new Rect(image.X, image.Y, image.Width, image.Height));
                    AddDrawingToGroup(grp, shape, i);
                    continue;
                }
                if (shape is TextShape)
                {
                    GeometryGroup gp = TextRender.BuildTextGeometry(shape as TextShape);
                    if (gp != null)
                    {
                        foreach (Geometry gm in gp.Children)
                        {
                            TextShape.TSpan.Element tspan = TextRender.GetElement(gm);
                            if (tspan != null)
                            {
                                var di = this.NewDrawingItem(tspan, gm);
                                AddDrawingToGroup(grp, shape, di);
                            }
                            else
                            {
                                var di = this.NewDrawingItem(shape, gm);
                                AddDrawingToGroup(grp, shape, di);
                            }
                        }
                    }
                    continue;
                }
                if (shape is PathShape)
                {
                    PathShape r = shape as PathShape;
//                    PathFigure p = null;
                    Point lastPoint = new Point(0, 0);

//                    PathShape.CurveTo lastc = null;
//                    PathShape.QuadraticCurveTo lastq = null;
                    Point lastcirPoint = new Point(0, 0);
                    PathGeometry path = PathGeometry.CreateFromGeometry(PathGeometry.Parse(r.Data));
                    //PathGeometry path = new PathGeometry();

                    /*
                    foreach (PathShape.PathElement element in r.Elements)
                    {
                        bool isRelative = element.IsRelative;
                        if (element is PathShape.MoveTo)
                        {
                            p = new PathFigure();
                            p.IsClosed = r.ClosePath;
                            if (isRelative) p.StartPoint = lastPoint + (Vector)((PathShape.MoveTo)element).Point;
                            else p.StartPoint = ((PathShape.MoveTo)element).Point;
                            lastPoint = p.StartPoint;
                            path.Figures.Add(p);
                            continue;
                        }
                        if (element is PathShape.LineTo)
                        {
                            PathShape.LineTo lineto = element as PathShape.LineTo;
                            foreach (Point point in lineto.Points)
                            {
                                if (isRelative)
                                {
                                    Point newpoint = lastPoint + (Vector)point;
                                    lastPoint = newpoint;
                                    p.Segments.Add(new LineSegment(newpoint, true));
                                }
                                else
                                {
                                    if (lineto.PositionType == PathShape.LineTo.eType.Point) lastPoint = point;
                                    if (lineto.PositionType == PathShape.LineTo.eType.Horizontal) lastPoint = new Point(point.X, lastPoint.Y);
                                    if (lineto.PositionType == PathShape.LineTo.eType.Vertical) lastPoint = new Point(lastPoint.X, point.Y);
                                    p.Segments.Add(new LineSegment(lastPoint, true));
                                }
                            }
                            continue;
                        }
                        if (element is PathShape.CurveTo)
                        {
                            PathShape.CurveTo c = element as PathShape.CurveTo;
                            Point startPoint = lastPoint;
                            BezierSegment s = new BezierSegment();
                            if (isRelative)
                            {
                                s.Point1 = lastPoint + (Vector)c.CtrlPoint1;

                                if (c.Command == 's')
                                {
                                    // first control point is a mirrored point of last end control point
                                    //s.Point1 = lastPoint + new Vector(lastc.Point.X - dx, lastc.Point.Y - dy);
                                    //s.Point1 = new Point(lastctrlpoint.X+2, lastctrlpoint.Y+2);

                                    double dx = lastc.CtrlPoint2.X - lastc.Point.X;
                                    double dy = lastc.CtrlPoint2.Y - lastc.Point.Y;
                                    s.Point1 = new Point(lastcirPoint.X - dx, lastcirPoint.Y - dy);
                                    //s.Point1 = lastctrlpoint;
                                }

                                s.Point2 = lastPoint + (Vector)c.CtrlPoint2;
                                s.Point3 = lastPoint + (Vector)c.Point;
                            }
                            else
                            {
                                if (c.Command == 's')
                                {
                                    // first control point is a mirrored point of last end control point
                                    //s.Point1 = lastPoint + new Vector(lastc.Point.X - dx, lastc.Point.Y - dy);
                                    //s.Point1 = new Point(lastctrlpoint.X+2, lastctrlpoint.Y+2);

                                    double dx = lastc.CtrlPoint2.X - lastc.Point.X;
                                    double dy = lastc.CtrlPoint2.Y - lastc.Point.Y;
                                    s.Point1 = new Point(lastcirPoint.X - dx, lastcirPoint.Y - dy);
                                }
                                else s.Point1 = c.CtrlPoint1;
                                s.Point2 = c.CtrlPoint2;
                                s.Point3 = c.Point;
                            }
                            lastPoint = s.Point3;
                            p.Segments.Add(s);

                            lastc = c;
                            lastcirPoint = s.Point3;

                            //debugPoints.Add(new ControlLine(startPoint, s.Point1));
                            //debugPoints.Add(new ControlLine(s.Point3, s.Point2));
                            continue;
                        }
                        if (element is PathShape.QuadraticCurveTo)
                        {
                            PathShape.QuadraticCurveTo c = element as PathShape.QuadraticCurveTo;
                            Point startPoint = lastPoint;
                            QuadraticBezierSegment s = new QuadraticBezierSegment();
                            if (isRelative)
                            {
                                s.Point1 = lastPoint + (Vector)c.CtrlPoint1;

                                if (c.Command == 'q' && lastq != null) // fix for horse svg! needed ?? or is it wrong in SVG?
                                {
                                    // first control point is a mirrored point of last end control point
                                    //s.Point1 = lastPoint + new Vector(lastc.Point.X - dx, lastc.Point.Y - dy);
                                    //s.Point1 = new Point(lastctrlpoint.X+2, lastctrlpoint.Y+2);

                                    double dx = lastq.CtrlPoint1.X - lastq.Point.X;
                                    double dy = lastq.CtrlPoint1.Y - lastq.Point.Y;
                                    s.Point1 = new Point(lastcirPoint.X - dx, lastcirPoint.Y - dy);
                                    //s.Point1 = lastctrlpoint;
                                }

                                s.Point2 = lastPoint + (Vector)c.Point;
                            }
                            else
                            {
                                if (c.Command == 'q')
                                {
                                    // first control point is a mirrored point of last end control point
                                    //s.Point1 = lastPoint + new Vector(lastc.Point.X - dx, lastc.Point.Y - dy);
                                    //s.Point1 = new Point(lastctrlpoint.X+2, lastctrlpoint.Y+2);

                                    double dx = lastq.CtrlPoint1.X - lastq.Point.X;
                                    double dy = lastq.CtrlPoint1.Y - lastq.Point.Y;
                                    s.Point1 = new Point(lastcirPoint.X - dx, lastcirPoint.Y - dy);
                                }
                                else s.Point1 = c.CtrlPoint1;
                                s.Point2 = c.Point;
                            }
                            lastPoint = s.Point2;
                            p.Segments.Add(s);

                            lastq = c;
                            lastcirPoint = s.Point2;

                            //debugPoints.Add(new ControlLine(startPoint, s.Point1));
                            //debugPoints.Add(new ControlLine(s.Point3, s.Point2));
                            continue;
                        }
                        if (element is PathShape.EllipticalArcTo)
                        {
                            PathShape.EllipticalArcTo c = element as PathShape.EllipticalArcTo;
                            ArcSegment s = new ArcSegment();
                            if (isRelative) s.Point = lastPoint + new Vector(c.X, c.Y);
                            else s.Point = new Point(c.X, c.Y);

                            s.Size = new Size(c.RX, c.RY);
                            s.RotationAngle = c.AxisRotation;
                            s.SweepDirection = SweepDirection.Counterclockwise;
                            if (c.Clockwise) s.SweepDirection = SweepDirection.Clockwise;
                            s.IsLargeArc = c.LargeArc;
                            lastPoint = s.Point;
                            p.Segments.Add(s);
                            continue;
                        }
                    }
                    */
                    /*
                    if (r.Transform != null)
                        path.Transform = r.Transform;
                    */
                    var di = this.NewDrawingItem(shape, path);
                    AddDrawingToGroup(grp, shape, di);
                    //}
                }
            }


            if (debugPoints != null)
            {
                foreach (ControlLine line in debugPoints)
                {
                    grp.Children.Add(line.Draw());
                }
            }
            return grp;
        }

        private void AddDrawingToGroup(DrawingGroup grp, Shape shape, Drawing drawing)
        {
            if (shape.Clip != null || shape.Transform != null || shape.Filter != null)
            {
                var subgrp = new DrawingGroup();
                if (shape.Clip != null)
                    subgrp.ClipGeometry = shape.Clip.ClipGeometry;
                if (shape.Transform != null)
                    subgrp.Transform = shape.Transform;
                if (shape.Filter != null)
                    subgrp.BitmapEffect = shape.Filter.GetBitmapEffect();
                subgrp.Children.Add(drawing);
                grp.Children.Add(subgrp);
            }
            else
                grp.Children.Add(drawing);
        }
    }
}
