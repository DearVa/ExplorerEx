using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace ExplorerEx.Utils;

public static class SvgConverter {
	private const char CPrefixSeparator = '_';

	static SvgConverter() {
		//bringt leider nix? _nsManager.AddNamespace("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
		NsManager.AddNamespace("defns", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
		NsManager.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml");
	}

	internal static readonly XNamespace Nsx = "http://schemas.microsoft.com/winfx/2006/xaml";
	internal static readonly XNamespace NsDef = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
	internal static readonly XmlNamespaceManager NsManager = new(new NameTable());

	public static DrawingImage ConvertSvgToDrawingImage(string filePath, WpfDrawingSettings wpfDrawingSettings = null) {
		var dg = ConvertSvgToDrawingGroup(filePath, wpfDrawingSettings);
		return DrawingToImage(dg);
	}

	public static DrawingGroup ConvertSvgToDrawingGroup(string filePath, WpfDrawingSettings wpfDrawingSettings) {
		var dg = SvgFileToWpfObject(filePath, wpfDrawingSettings);
		SetSizeToGeometries(dg);
		RemoveObjectNames(dg);
		return dg;
	}

	internal static void SetSizeToGeometries(DrawingGroup dg) {
		var size = GetSizeFromDrawingGroup(dg);
		if (size.HasValue) {
			var geometries = GetPathGeometries(dg).ToList();
			geometries.ForEach(g => SizeGeometry(g, size.Value));
		}
	}

	public static IEnumerable<PathGeometry> GetPathGeometries(Drawing drawing) {
		var result = new List<PathGeometry>();

		void HandleDrawing(Drawing aDrawing) {
			switch (aDrawing) {
			case DrawingGroup drawingGroup: {
				foreach (var d in drawingGroup.Children) {
					HandleDrawing(d);
				}
				break;
			}
			case GeometryDrawing geometryDrawing:
				var geometry = geometryDrawing.Geometry;
				if (geometry is PathGeometry pathGeometry) {
					result.Add(pathGeometry);
				}
				break;
			}
		}

		HandleDrawing(drawing);

		return result;
	}

	public static void SizeGeometry(PathGeometry pg, Size size) {
		if (size.Height is > 0 and > 0) {
			PathFigure[] sizeFigures = {
				new(new Point(size.Width, size.Height), Enumerable.Empty<PathSegment>(), true),
				new(new Point(0,0), Enumerable.Empty<PathSegment>(), true)
			};

			var newGeo = new PathGeometry(sizeFigures.Concat(pg.Figures), pg.FillRule, null);//pg.Transform do not add transform here, it will recalculate all the Points
			pg.Clear();
			pg.AddGeometry(newGeo);
		}
	}

	internal static DrawingGroup SvgFileToWpfObject(string filePath, WpfDrawingSettings wpfDrawingSettings) {
		wpfDrawingSettings ??= new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = false, OptimizePath = true };
		var reader = new FileSvgReader(wpfDrawingSettings);

		//this is straight forward, but in this version of the dlls there is an error when name starts with a digit
		//var uri = new Uri(Path.GetFullPath(filePath));
		//reader.Read(uri); //accessing using the filename results is problems with the uri (if the dlls are packed in ressources)
		//return reader.Drawing;

		//this should be faster, but using CreateReader will loose text items like "JOG" ?!
		//using (var stream = File.OpenRead(Path.GetFullPath(filePath)))
		//{
		//    //workaround: error when Id starts with a number
		//    var doc = XDocument.Load(stream);
		//    ReplaceIdsWithNumbers(doc.Root); //id="3d-view-icon" -> id="_3d-view-icon"
		//    using (var xmlReader = doc.CreateReader())
		//    {
		//        reader.Read(xmlReader);
		//        return reader.Drawing;
		//    }
		//}

		//workaround: error when Id starts with a number
		var doc = XDocument.Load(Path.GetFullPath(filePath));
		FixIds(doc.Root); //id="3d-view-icon" -> id="_3d-view-icon"
		using var ms = new MemoryStream();
		doc.Save(ms);
		ms.Position = 0;
		reader.Read(ms);
		return reader.Drawing;
	}

	private static void FixIds(XElement root) {
		var idAttributesStartingWithDigit = root.DescendantsAndSelf()
			.SelectMany(d => d.Attributes())
			.Where(a => string.Equals(a.Name.LocalName, "Id", StringComparison.InvariantCultureIgnoreCase));
		foreach (var attr in idAttributesStartingWithDigit) {
			if (char.IsDigit(attr.Value.FirstOrDefault())) {
				attr.Value = "_" + attr.Value;
			}

			attr.Value = attr.Value.Replace("/", "_");
		}
	}


	internal static DrawingImage DrawingToImage(Drawing drawing) {
		return new DrawingImage(drawing);
	}

	public static void RemoveObjectNames(DrawingGroup drawingGroup) {
		if (drawingGroup.GetValue(FrameworkElement.NameProperty) != null) {
			drawingGroup.SetValue(FrameworkElement.NameProperty, null);
		}
		foreach (var child in drawingGroup.Children.OfType<DependencyObject>()) {
			if (child.GetValue(FrameworkElement.NameProperty) != null) {
				child.SetValue(FrameworkElement.NameProperty, null);
			}
			if (child is DrawingGroup dg) {
				RemoveObjectNames(dg);
			}
		}
	}

	internal static string BuildGeometryName(string name, int? no, ResKeyInfo resKeyInfo) {
		var rawName = no.HasValue
			? $"{name}Geometry{no.Value}"
			: $"{name}Geometry"; //dont add number if only one Geometry
		return BuildResKey(rawName, resKeyInfo);
	}

	internal static string BuildResKey(string name, ResKeyInfo resKeyInfo) {
		if (resKeyInfo.UseComponentResKeys) {
			return $"{{x:Static {resKeyInfo.NameSpaceName}:{resKeyInfo.XamlName}.{ValidateName(name)}Key}}";
		}
		var result = name;
		if (resKeyInfo.Prefix != null) {
			result = resKeyInfo.Prefix + CPrefixSeparator + name;
		}
		result = ValidateName(result);
		return result;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="refName">ist der schon komplett fertige name mit prefix oder Reskey</param>
	/// <param name="dynamic"></param>
	/// <returns></returns>
	internal static string BuildResKeyReference(string refName, bool dynamic = false) {
		var resourceIdent = dynamic ? "DynamicResource" : "StaticResource";
		return $"{{{resourceIdent} {refName}}}";
	}

	internal static string GetElemNameFromResKey(string name, ResKeyInfo resKeyInfo) {
		if (resKeyInfo.UseComponentResKeys) {   //{x:Static NameSpaceName:XamlName.ElementName}
			var p1 = name.IndexOf(".", StringComparison.Ordinal);
			var p2 = name.LastIndexOf("}", StringComparison.Ordinal);
			string result;
			if (p1 < p2) {
				result = name.Substring(p1 + 1, p2 - p1 - 1);
			} else {
				result = name;
			}
			if (result.EndsWith("Key", StringComparison.InvariantCulture)) {
				result = result[..^3];
			}
			return result;
		}
		if (resKeyInfo.Prefix == null) {
			return name;
		}
		var prefixWithSeparator = resKeyInfo.Prefix + CPrefixSeparator;
		if (name.StartsWith(resKeyInfo.Prefix + CPrefixSeparator, StringComparison.OrdinalIgnoreCase)) {
			name = name.Remove(0, prefixWithSeparator.Length);
		}
		return name;
	}

	internal static string ValidateName(string name) {
		var result = Regex.Replace(name, @"[^[0-9a-zA-Z]]*", "_");
		if (Regex.IsMatch(result, "^[0-9].*")) {
			result = "_" + result;
		}
		return result;
	}

	internal static XElement GetClipElement(XElement drawingGroupElement, out Rect rect) {
		rect = default;
		if (drawingGroupElement == null) {
			return null;
		}
		//<DrawingGroup x:Key="cloud_3_icon_DrawingGroup">
		//   <DrawingGroup>
		//       <DrawingGroup.ClipGeometry>
		//           <RectangleGeometry Rect="0,0,512,512" />
		//       </DrawingGroup.ClipGeometry>
		var clipElement = drawingGroupElement.XPathSelectElement(".//defns:DrawingGroup.ClipGeometry", NsManager);
		var rectangleElement = clipElement?.Element(NsDef + "RectangleGeometry");
		var rectAttr = rectangleElement?.Attribute("Rect");
		if (rectAttr != null) {
			rect = Rect.Parse(rectAttr.Value);
			return clipElement;
		}
		return null;
	}

	internal static Size? GetSizeFromDrawingGroup(DrawingGroup drawingGroup) {
		//<DrawingGroup x:Key="cloud_3_icon_DrawingGroup">
		//   <DrawingGroup>
		//       <DrawingGroup.ClipGeometry>
		//           <RectangleGeometry Rect="0,0,512,512" />
		//       </DrawingGroup.ClipGeometry>
		var subGroup = drawingGroup?.Children
			.OfType<DrawingGroup>()
			.FirstOrDefault(c => c.ClipGeometry != null);
		return subGroup?.ClipGeometry.Bounds.Size;
	}
}

public class ResKeyInfo {
	public string Name { get; set; }
	public string XamlName { get; set; }
	public string Prefix { get; set; }
	public bool UseComponentResKeys { get; set; }
	public string NameSpace { get; set; }
	public string NameSpaceName { get; set; }
}
