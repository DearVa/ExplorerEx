using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Xml;
using DotNetProjects.SVGImage.SVG;
using DotNetProjects.SVGImage.SVG.Animation;
using DotNetProjects.SVGImage.SVG.FileLoaders;
using SharpSvgImage.Svg.Shapes;

namespace SharpSvgImage.Svg;

/// <summary>
///     This is the class that creates the WPF Drawing object based on the information from the <see cref="Svg" /> class.
/// </summary>
/// <seealso href="http://www.w3.org/TR/SVGTiny12/" />
/// <seealso href="http://commons.oreilly.com/wiki/index.php/SVG_Essentials" />
public class SvgRender {
	private Dictionary<string, Brush> m_customBrushes;

	public SvgRender() : this(FileSystemLoader.Instance) { }

	public SvgRender(IExternalFileLoader fileLoader) {
		ExternalFileLoader = fileLoader ?? FileSystemLoader.Instance;
	}

	public Svg Svg { get; private set; }

	public bool UseAnimations { get; set; }

	public Color? OverrideColor { get; set; }

	public double? OverrideStrokeWidth { get; set; }

	public Dictionary<string, Brush> CustomBrushes {
		get => m_customBrushes;
		set {
			m_customBrushes = value;
			if (Svg != null) {
				Svg.CustomBrushes = value;
			}
		}
	}

	public IExternalFileLoader ExternalFileLoader { get; set; }

	public DrawingGroup LoadDrawing(string filename) {
		Svg = new Svg(filename, ExternalFileLoader);
		return CreateDrawing(Svg);
	}

	public DrawingGroup LoadXmlDrawing(string fileXml) {
		Svg = new Svg(ExternalFileLoader);
		Svg.LoadXml(fileXml);

		return CreateDrawing(Svg);
	}

	public DrawingGroup LoadDrawing(Uri fileUri) {
		Svg = new Svg(ExternalFileLoader);
		Svg.Load(fileUri);

		return CreateDrawing(Svg);
	}

	public DrawingGroup LoadDrawing(TextReader txtReader) {
		Svg = new Svg(ExternalFileLoader);
		Svg.Load(txtReader);

		return CreateDrawing(Svg);
	}

	public DrawingGroup LoadDrawing(XmlReader xmlReader) {
		Svg = new Svg(ExternalFileLoader);
		Svg.Load(xmlReader);

		return CreateDrawing(Svg);
	}

	public DrawingGroup LoadDrawing(Stream stream) {
		Svg = new Svg(stream, ExternalFileLoader);

		return CreateDrawing(Svg);
	}

	public DrawingGroup CreateDrawing(Svg svg) {
		return LoadGroup(svg.Elements, svg.ViewBox, false);
	}

	public DrawingGroup CreateDrawing(Shape shape) {
		return LoadGroup(new[] { shape }, null, false);
	}

	private GeometryDrawing NewDrawingItem(Shape shape, Geometry geometry) {
		shape.geometryElement = geometry;
		var item = new GeometryDrawing();
		var stroke = shape.Stroke;
		if (stroke != null) {
			if (OverrideStrokeWidth.HasValue) {
				stroke.Width = OverrideStrokeWidth.Value;
			}
			var brush = stroke.StrokeBrush(Svg, this, shape, shape.Opacity, geometry.Bounds);
			if (OverrideColor != null) {
				brush = new SolidColorBrush(Color.FromArgb((byte)(255 * shape.Opacity), OverrideColor.Value.R, OverrideColor.Value.G, OverrideColor.Value.B));
			}
			item.Pen = new Pen(brush, stroke.Width);
			if (stroke.StrokeArray != null) {
				item.Pen.DashCap = PenLineCap.Flat;
				var ds = new DashStyle();
				var scale = 1 / stroke.Width;
				foreach (var dash in stroke.StrokeArray) {
					ds.Dashes.Add(dash * scale);
				}
				item.Pen.DashStyle = ds;
			}
			switch (stroke.LineCap) {
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
			item.Pen.LineJoin = stroke.LineJoin switch {
				Stroke.eLineJoin.round => PenLineJoin.Round,
				Stroke.eLineJoin.miter => PenLineJoin.Miter,
				Stroke.eLineJoin.bevel => PenLineJoin.Bevel,
				_ => item.Pen.LineJoin
			};
		}

		if (shape.Fill == null) {
			item.Brush = Brushes.Black;
			if (OverrideColor != null) {
				item.Brush = new SolidColorBrush(Color.FromArgb((byte)(255 * shape.Opacity), OverrideColor.Value.R, OverrideColor.Value.G, OverrideColor.Value.B));
			}
			var g = new GeometryGroup {
				FillRule = FillRule.Nonzero
			};
			g.Children.Add(geometry);
			geometry = g;
		} else if (shape.Fill != null) {
			item.Brush = shape.Fill.FillBrush(Svg, this, shape, shape.Opacity, geometry.Bounds);
			if (OverrideColor != null) {
				item.Brush = new SolidColorBrush(Color.FromArgb((byte)(255 * shape.Opacity), OverrideColor.Value.R, OverrideColor.Value.G, OverrideColor.Value.B));
			}
			var g = new GeometryGroup {
				FillRule = FillRule.Nonzero
			};
			if (shape.Fill.FillRule == Fill.eFillRule.evenodd) {
				g.FillRule = FillRule.EvenOdd;
			}
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

	internal DrawingGroup LoadGroup(IList<Shape> elements, Rect? viewBox, bool isSwitch) {
		var debugPoints = new List<ControlLine>();
		var grp = new DrawingGroup();
		if (viewBox.HasValue) {
			grp.ClipGeometry = new RectangleGeometry(viewBox.Value);
		}

		foreach (var shape in elements) {
			shape.RealParent = null;
			if (!shape.Display) {
				continue;
			}

			if (isSwitch) {
				if (grp.Children.Count > 0) {
					break;
				}
				if (!string.IsNullOrEmpty(shape.RequiredFeatures)) {
					if (!SvgFeatures.Features.Contains(shape.RequiredFeatures)) {
						continue;
					}
					if (!string.IsNullOrEmpty(shape.RequiredExtensions)) {
						continue;
					}
				}
			}

			switch (shape) {
			case AnimationBase: {
				if (UseAnimations) {
					switch (shape) {
					case AnimateTransform animateTransform: {
						if (animateTransform.Type == AnimateTransformType.Rotate) {
							var animation = new DoubleAnimation {
								From = double.Parse(animateTransform.From),
								To = double.Parse(animateTransform.To),
								Duration = animateTransform.Duration,
								RepeatBehavior = RepeatBehavior.Forever
							};
							var r = new RotateTransform();
							grp.Transform = r;
							r.BeginAnimation(RotateTransform.AngleProperty, animation);
						}
						break;
					}
					case Animate animate: {
						var target = Svg.GetShape(animate.hRef);
						var g = target.geometryElement;
						//todo : rework this all, generalize it!
						switch (animate.AttributeName) {
						case "r": {
							var animation = new DoubleAnimationUsingKeyFrames { Duration = animate.Duration };
							foreach (var d in animate.Values.Split(';').Select(x => new LinearDoubleKeyFrame(double.Parse(x)))) {
								animation.KeyFrames.Add(d);
							}
							animation.RepeatBehavior = RepeatBehavior.Forever;

							g.BeginAnimation(EllipseGeometry.RadiusXProperty, animation);
							g.BeginAnimation(EllipseGeometry.RadiusYProperty, animation);
							break;
						}
						case "cx": {
							var animation = new PointAnimationUsingKeyFrames { Duration = animate.Duration };
							foreach (var d in animate.Values.Split(';').Select(_ => new LinearPointKeyFrame(new Point(double.Parse(_), ((EllipseGeometry)g).Center.Y)))) {
								animation.KeyFrames.Add(d);
							}
							animation.RepeatBehavior = RepeatBehavior.Forever;
							g.BeginAnimation(EllipseGeometry.CenterProperty, animation);
							break;
						}
						case "cy": {
							var animation = new PointAnimationUsingKeyFrames { Duration = animate.Duration };
							foreach (var d in animate.Values.Split(';').Select(_ => new LinearPointKeyFrame(new Point(((EllipseGeometry)g).Center.X, double.Parse(_))))) {
								animation.KeyFrames.Add(d);
							}
							animation.RepeatBehavior = RepeatBehavior.Forever;
							g.BeginAnimation(EllipseGeometry.CenterProperty, animation);
							break;
						}
						}
						break;
					}
					}
				}

				continue;
			}
			case UseShape useShape: {
				var currentUsedShape = Svg.GetShape(useShape.hRef);
				if (currentUsedShape != null) {
					currentUsedShape.RealParent = useShape;
					var oldParent = currentUsedShape.Parent;
					DrawingGroup subgroup;
					if (currentUsedShape is Group group) {
						subgroup = LoadGroup(group.Elements, null, false);
					} else {
						subgroup = LoadGroup(new[] { currentUsedShape }, null, false);
					}
					if (currentUsedShape.Clip != null) {
						subgroup.ClipGeometry = currentUsedShape.Clip.ClipGeometry;
					}
					subgroup.Transform = new TranslateTransform(useShape.X, useShape.Y);
					if (useShape.Transform != null) {
						subgroup.Transform = new TransformGroup { Children = new TransformCollection { subgroup.Transform, useShape.Transform } };
					}
					grp.Children.Add(subgroup);
					currentUsedShape.Parent = oldParent;
				}
				continue;
			}
			case Clip clip: {
				var subgroup = LoadGroup(clip.Elements, null, false);
				if (clip.Transform != null) {
					subgroup.Transform = clip.Transform;
				}
				grp.Children.Add(subgroup);
				continue;
			}
			case Group groupShape: {
				var subgroup = LoadGroup(groupShape.Elements, null, groupShape.IsSwitch);
				AddDrawingToGroup(grp, groupShape, subgroup);
				continue;
			}
			case RectangleShape rectangleShape: {
				//RectangleGeometry rect = new RectangleGeometry(new Rect(r.X < 0 ? 0 : r.X, r.Y < 0 ? 0 : r.Y, r.X < 0 ? r.Width + r.X : r.Width, r.Y < 0 ? r.Height + r.Y : r.Height));
				var dx = rectangleShape.X;
				var dy = rectangleShape.Y;
				var width = rectangleShape.Width;
				var height = rectangleShape.Height;
				var rx = rectangleShape.RX;
				var ry = rectangleShape.RY;
				if (width <= 0 || height <= 0) {
					continue;
				}
				switch (rx) {
				case <= 0 when ry > 0:
					rx = ry;
					break;
				case > 0 when ry <= 0:
					ry = rx;
					break;
				}

				var rect = new RectangleGeometry(new Rect(dx, dy, width, height), rx, ry);
				var di = NewDrawingItem(rectangleShape, rect);
				AddDrawingToGroup(grp, rectangleShape, di);
				continue;
			}
			case LineShape lineShape: {
				var line = new LineGeometry(lineShape.P1, lineShape.P2);
				var di = NewDrawingItem(lineShape, line);
				AddDrawingToGroup(grp, lineShape, di);
				continue;
			}
			case PolylineShape polylineShape: {
				var path = new PathGeometry();
				var p = new PathFigure();
				path.Figures.Add(p);
				p.IsClosed = false;
				p.StartPoint = polylineShape.Points[0];
				for (var index = 1; index < polylineShape.Points.Length; index++) {
					p.Segments.Add(new LineSegment(polylineShape.Points[index], true));
				}
				var di = NewDrawingItem(polylineShape, path);
				AddDrawingToGroup(grp, polylineShape, di);
				continue;
			}
			case PolygonShape polygonShape: {
				var path = new PathGeometry();
				var p = new PathFigure();
				path.Figures.Add(p);
				p.IsClosed = true;
				p.StartPoint = polygonShape.Points[0];
				for (var index = 1; index < polygonShape.Points.Length; index++) {
					p.Segments.Add(new LineSegment(polygonShape.Points[index], true));
				}
				var di = NewDrawingItem(polygonShape, path);
				AddDrawingToGroup(grp, polygonShape, di);
				continue;
			}
			case CircleShape circleShape: {
				var c = new EllipseGeometry(new Point(circleShape.CX, circleShape.CY), circleShape.R, circleShape.R);
				var di = NewDrawingItem(circleShape, c);
				AddDrawingToGroup(grp, circleShape, di);
				continue;
			}
			case EllipseShape ellipseShape: {
				var c = new EllipseGeometry(new Point(ellipseShape.CX, ellipseShape.CY), ellipseShape.RX, ellipseShape.RY);
				var di = NewDrawingItem(ellipseShape, c);
				AddDrawingToGroup(grp, ellipseShape, di);
				continue;
			}
			case ImageShape imageShape: {
				var i = new ImageDrawing(imageShape.ImageSource, new Rect(imageShape.X, imageShape.Y, imageShape.Width, imageShape.Height));
				AddDrawingToGroup(grp, imageShape, i);
				continue;
			}
			case TextShape textShape: {
				var gp = TextRender.BuildTextGeometry(textShape);
				if (gp != null) {
					foreach (var gm in gp.Children) {
						var tSpan = TextRender.GetElement(gm);
						if (tSpan != null) {
							var di = NewDrawingItem(tSpan, gm);
							AddDrawingToGroup(grp, textShape, di);
						} else {
							var di = NewDrawingItem(textShape, gm);
							AddDrawingToGroup(grp, textShape, di);
						}
					}
				}
				continue;
			}
			case PathShape pathShape: {
				var svg = Svg;
				if (pathShape.Fill == null || pathShape.Fill.IsEmpty(svg)) {
					if (pathShape.Stroke == null || pathShape.Stroke.IsEmpty(svg)) {
						var fill = new Fill(svg) {
							PaintServerKey = Svg.PaintServers.Parse("black")
						};
						pathShape.Fill = fill;
					}
				}
				var path = PathGeometry.CreateFromGeometry(Geometry.Parse(pathShape.Data));
				var di = NewDrawingItem(pathShape, path);
				AddDrawingToGroup(grp, pathShape, di);
				break;
			}
			}
		}


		if (debugPoints != null) {
			foreach (var line in debugPoints) {
				grp.Children.Add(line.Draw());
			}
		}
		return grp;
	}

	private static void AddDrawingToGroup(DrawingGroup grp, Shape shape, Drawing drawing) {
		if (shape.Clip != null || shape.Transform != null || shape.Filter != null) {
			var subGroup = new DrawingGroup();
			if (shape.Clip != null) {
				subGroup.ClipGeometry = shape.Clip.ClipGeometry;
			}
			if (shape.Transform != null) {
				subGroup.Transform = shape.Transform;
			}
			if (shape.Filter != null) {
				subGroup.BitmapEffect = shape.Filter.GetBitmapEffect();
			}
			subGroup.Children.Add(drawing);
			grp.Children.Add(subGroup);
		} else {
			grp.Children.Add(drawing);
		}
	}


	private class ControlLine {
		public ControlLine(Point start, Point ctrl) {
			Start = start;
			Ctrl = ctrl;
		}

		public Point Ctrl { get; }

		public Point Start { get; }

		public GeometryDrawing Draw() {
			const double size = 0.2;
			var item = new GeometryDrawing {
				Brush = Brushes.Red
			};
			var g = new GeometryGroup();

			item.Pen = new Pen(Brushes.LightGray, size / 2);
			g.Children.Add(new LineGeometry(Start, Ctrl));

			g.Children.Add(new RectangleGeometry(new Rect(Start.X - size / 2, Start.Y - size / 2, size, size)));
			g.Children.Add(new EllipseGeometry(Ctrl, size, size));

			item.Geometry = g;
			return item;
		}
	}
}