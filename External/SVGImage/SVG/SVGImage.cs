using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using DotNetProjects.SVGImage.SVG.FileLoaders;

namespace SVGImage.SVG; 

/// <summary>
///     This is the SVG image view control.
///     The image control can either load the image from a file <see cref="SetImage(string)" /> or by
///     setting the <see cref="Drawing" /> object through <see cref="SetImage(Drawing)" />, which allows
///     multiple controls to share the same drawing instance.
/// </summary>
public class SVGImage : Control, IUriContext {
	/// <summary>
	///     This controls how the image is stretched to fill the control,
	/// </summary>
	public enum eSizeType {
		/// <summary>
		///     The image is not scaled. The image location is translated so the top left corner
		///     of the image bounding box is moved to the top left corner of the image control.
		/// </summary>
		None,

		/// <summary>
		///     The image is scaled to fit the control without any stretching.
		///     Either X or Y direction will be scaled to fill the entire width or height.
		/// </summary>
		ContentToSizeNoStretch,

		/// <summary>
		///     The image will be stretched to fill the entire width and height.
		/// </summary>
		ContentToSizeStretch,

		/// <summary>
		///     The control will be resized to fit the un-scaled image. If the image is larger than the
		///     maximum size for the control, the control is set to maximum size and the image is scaled.
		/// </summary>
		SizeToContent
	}

	/// <summary>
	///     Identifies the <see cref="UriSource" /> dependency property.
	/// </summary>
	public static readonly DependencyProperty UriSourceProperty =
		DependencyProperty.Register("UriSource", typeof(Uri), typeof(SVGImage),
			new FrameworkPropertyMetadata(null, OnUriSourceChanged));

	public static DependencyProperty SizeTypeProperty = DependencyProperty.Register("SizeType",
		typeof(eSizeType), typeof(SVGImage), new FrameworkPropertyMetadata(eSizeType.ContentToSizeNoStretch,
			FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
			OnSizeTypeChanged));

	public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source",
		typeof(string), typeof(SVGImage), new FrameworkPropertyMetadata(null,
			FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
			OnSourceChanged));

	public static readonly DependencyProperty FileSourceProperty = DependencyProperty.Register("FileSource",
		typeof(string), typeof(SVGImage), new PropertyMetadata(OnFileSourceChanged));

	public static DependencyProperty ImageSourcePoperty = DependencyProperty.Register("ImageSource",
		typeof(Drawing), typeof(SVGImage), new FrameworkPropertyMetadata(null,
			FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
			OnImageSourceChanged));

	public static readonly DependencyProperty OverrideStrokeWidthProperty = DependencyProperty.Register(nameof(OverrideStrokeWidth),
		typeof(double?), typeof(SVGImage), new FrameworkPropertyMetadata(default,
			FrameworkPropertyMetadataOptions.AffectsRender, OverrideStrokeWidthPropertyChanged));

	public static readonly DependencyProperty OverrideColorProperty =
		DependencyProperty.Register("OverrideColor",
			typeof(Color?),
			typeof(SVGImage),
			new FrameworkPropertyMetadata(default, FrameworkPropertyMetadataOptions.AffectsRender, OverrideColorPropertyChanged));

	public static readonly DependencyProperty CustomBrushesProperty = DependencyProperty.Register(nameof(CustomBrushes),
		typeof(Dictionary<string, Brush>), typeof(SVGImage), new FrameworkPropertyMetadata(default,
			FrameworkPropertyMetadataOptions.AffectsRender, CustomBrushesPropertyChanged));

	public static readonly DependencyProperty UseAnimationsProperty = DependencyProperty.Register("UseAnimations",
		typeof(bool), typeof(SVGImage), new PropertyMetadata(true));

	public static readonly DependencyProperty ExternalFileLoaderProperty = DependencyProperty.Register("ExternalFileLoader",
		typeof(IExternalFileLoader), typeof(SVGImage), new PropertyMetadata(FileSystemLoader.Instance));

	private Action<SVGRender> _loadImage;
	private SVGRender _render;

	private Drawing m_drawing;
	private readonly TranslateTransform m_offsetTransform;

	private readonly ScaleTransform m_scaleTransform;

	static SVGImage() {
		DefaultStyleKeyProperty.OverrideMetadata(typeof(SVGImage), new FrameworkPropertyMetadata(typeof(SVGImage)));
		ClipToBoundsProperty.OverrideMetadata(typeof(SVGImage), new FrameworkPropertyMetadata(true));
		SnapsToDevicePixelsProperty.OverrideMetadata(typeof(SVGImage), new FrameworkPropertyMetadata(true));
	}

	public SVGImage() {
		ClipToBounds = true;
		SnapsToDevicePixels = true;

		m_offsetTransform = new TranslateTransform();
		m_scaleTransform = new ScaleTransform();
	}

	public SVG SVG => _render?.SVG;

	public eSizeType SizeType {
		get => (eSizeType)GetValue(SizeTypeProperty);
		set => SetValue(SizeTypeProperty, value);
	}

	public string Source {
		get => (string)GetValue(SourceProperty);
		set => SetValue(SourceProperty, value);
	}

	public string FileSource {
		get => (string)GetValue(FileSourceProperty);
		set => SetValue(FileSourceProperty, value);
	}

	public Drawing ImageSource {
		get => (Drawing)GetValue(ImageSourcePoperty);
		set => SetValue(ImageSourcePoperty, value);
	}

	public bool UseAnimations {
		get => (bool)GetValue(UseAnimationsProperty);
		set => SetValue(UseAnimationsProperty, value);
	}

	public Color? OverrideColor {
		get => (Color?)GetValue(OverrideColorProperty);
		set => SetValue(OverrideColorProperty, value);
	}

	public double? OverrideStrokeWidth {
		get => (double?)GetValue(OverrideStrokeWidthProperty);
		set => SetValue(OverrideStrokeWidthProperty, value);
	}

	public Dictionary<string, Brush> CustomBrushes {
		get => (Dictionary<string, Brush>)GetValue(CustomBrushesProperty);
		set => SetValue(CustomBrushesProperty, value);
	}

	public IExternalFileLoader ExternalFileLoader {
		get => (IExternalFileLoader)GetValue(ExternalFileLoaderProperty);
		set => SetValue(ExternalFileLoaderProperty, value);
	}

	/// <summary>
	///     Gets or sets the path to the SVG file to load into this <see cref="Canvas" />.
	/// </summary>
	/// <value>
	///     A <see cref="System.Uri" /> specifying the path to the SVG source file.
	///     The file can be located on a computer, network or assembly resources.
	///     Settings this to <see langword="null" /> will close any opened diagram.
	/// </value>
	/// <remarks>
	///     This is the same as the <see cref="Source" /> property, and added for consistency.
	/// </remarks>
	/// <seealso cref="UriSource" />
	/// <seealso cref="StreamSource" />
	public Uri UriSource {
		get => (Uri)GetValue(UriSourceProperty);
		set => SetValue(UriSourceProperty, value);
	}

	/// <summary>
	///     Gets or sets the base URI of the current application context.
	/// </summary>
	/// <value>
	///     The base URI of the application context.
	/// </value>
	public Uri BaseUri { get; set; }

	public void ReRenderSvg() {
		if (_render != null) {
			SetImage(_render.CreateDrawing(_render.SVG));
		} else if (IsInitialized && _loadImage != null) {
			_render = new SVGRender();
			_render.ExternalFileLoader = ExternalFileLoader;
			_render.OverrideColor = OverrideColor;
			_render.CustomBrushes = CustomBrushes;
			_render.OverrideStrokeWidth = OverrideStrokeWidth;
			_render.UseAnimations = UseAnimations;

			_loadImage(_render);
			_loadImage = null;
		}
	}

	public void SetImage(string svgFilename) {
		_loadImage = render => { SetImage(render.LoadDrawing(svgFilename)); };

		if (IsInitialized || DesignerProperties.GetIsInDesignMode(this)) {
			_render = new SVGRender();
			_render.ExternalFileLoader = ExternalFileLoader;
			_render.UseAnimations = false;
			_render.OverrideColor = OverrideColor;
			_render.CustomBrushes = CustomBrushes;
			_render.OverrideStrokeWidth = OverrideStrokeWidth;

			_loadImage(_render);
			_loadImage = null;
		}
	}

	public void SetImage(Stream stream) {
		_loadImage = render => { SetImage(render.LoadDrawing(stream)); };

		if (IsInitialized || DesignerProperties.GetIsInDesignMode(this)) {
			_render = new SVGRender();
			_render.ExternalFileLoader = ExternalFileLoader;
			_render.OverrideColor = OverrideColor;
			_render.CustomBrushes = CustomBrushes;
			_render.OverrideStrokeWidth = OverrideStrokeWidth;
			_render.UseAnimations = false;

			_loadImage(_render);
			_loadImage = null;
		}
	}

	public void SetImage(Uri uriSource) {
		_loadImage = render => {
			var svgUri = ResolveUri(uriSource);
			SetImage(LoadDrawing(svgUri));
		};

		if (IsInitialized || DesignerProperties.GetIsInDesignMode(this)) {
			_render = new SVGRender();
			_render.ExternalFileLoader = ExternalFileLoader;
			_render.OverrideColor = OverrideColor;
			_render.CustomBrushes = CustomBrushes;
			_render.OverrideStrokeWidth = OverrideStrokeWidth;
			_render.UseAnimations = false;

			_loadImage(_render);
			_loadImage = null;
		}
	}

	public void SetImage(Drawing drawing) {
		m_drawing = drawing;
		InvalidateVisual();
		if (m_drawing != null && SizeType == eSizeType.SizeToContent) {
			InvalidateMeasure();
		}
		RecalcImage();
		InvalidateVisual();
	}

	protected override void OnInitialized(EventArgs e) {
		base.OnInitialized(e);

		if (_loadImage != null) {
			_render = new SVGRender();
			_render.ExternalFileLoader = ExternalFileLoader;
			_render.OverrideColor = OverrideColor;
			_render.CustomBrushes = CustomBrushes;
			_render.OverrideStrokeWidth = OverrideStrokeWidth;
			_render.UseAnimations = UseAnimations;

			_loadImage(_render);
			_loadImage = null;
			var brushesFromSVG = new Dictionary<string, Brush>();
			foreach (var server in _render.SVG.PaintServers.GetServers()) {
				brushesFromSVG[server.Key] = server.Value.GetBrush();
			}
			CustomBrushes = brushesFromSVG;
		}
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
		base.OnRenderSizeChanged(sizeInfo);
		RecalcImage();
		InvalidateVisual();
	}

	// Notice TemplateBinding BackGround must be removed from the Border in the default template (or remove Border from the template)
	// Border renders the background AFTER the child render has been called
	// http://social.msdn.microsoft.com/Forums/en-US/wpf/thread/1575d2af-8e86-4085-81b8-a8bf24268e51/?prof=required
	protected override void OnRender(DrawingContext dc) {
		//base.OnRender(dc);
		if (Background != null) {
			dc.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
		}
		if (m_drawing == null) {
			return;
		}
		dc.PushTransform(m_offsetTransform);
		dc.PushTransform(m_scaleTransform);
		dc.DrawDrawing(m_drawing);
		dc.Pop();
		dc.Pop();
	}

	/// <summary>
	/// 转换为BitmapImage，操作较为耗时
	/// </summary>
	/// <returns></returns>
	//public BitmapImage ToBitmapImage() {
	//	var drawingVisual = new DrawingVisual();
	//	using (var dc = drawingVisual.RenderOpen()) {
	//		OnRender(dc);
	//	}
	//	return drawingVisual;
	//}

	protected override Size MeasureOverride(Size constraint) {
		var result = base.MeasureOverride(constraint);
		if (SizeType == eSizeType.SizeToContent) {
			if (m_drawing is {Bounds: {Size: {IsEmpty: false}}}) {
				result = m_drawing.Bounds.Size;
			}
		}
		if (constraint.Width > 0 && constraint.Width < result.Width) {
			result.Width = constraint.Width;
		}
		if (constraint.Height > 0 && constraint.Height < result.Height) {
			result.Height = constraint.Height;
		}

		// Check for empty size...
		if (result.IsEmpty) {
			if (m_drawing != null) {
				result = m_drawing.Bounds.Size;
			}
		}

		return result;
	}

	protected override Size ArrangeOverride(Size arrangeBounds) {
		var result = base.ArrangeOverride(arrangeBounds);
		if (SizeType == eSizeType.SizeToContent) {
			if (m_drawing != null && !m_drawing.Bounds.Size.IsEmpty) {
				result = m_drawing.Bounds.Size;
			}
		}
		if (arrangeBounds.Width > 0 && arrangeBounds.Width < result.Width) {
			result.Width = arrangeBounds.Width;
		}
		if (arrangeBounds.Height > 0 && arrangeBounds.Height < result.Height) {
			result.Height = arrangeBounds.Height;
		}

		return result;
	}

	private void RecalcImage() {
		if (m_drawing == null) {
			return;
		}

		var r = m_drawing.Bounds;
		if (SizeType == eSizeType.None) {
			m_scaleTransform.ScaleX = 1;
			m_scaleTransform.ScaleY = 1;
			switch (HorizontalContentAlignment) {
			case HorizontalAlignment.Center:
				m_offsetTransform.X = ActualWidth / 2 - r.Width / 2 - r.Left;
				break;
			case HorizontalAlignment.Right:
				m_offsetTransform.X = ActualWidth - r.Right;
				break;
			default:
				m_offsetTransform.X = -r.Left; // move to left by default
				break;
			}
			switch (VerticalContentAlignment) {
			case VerticalAlignment.Center:
				m_offsetTransform.Y = ActualHeight / 2 - r.Height / 2;
				break;
			case VerticalAlignment.Bottom:
				m_offsetTransform.Y = ActualHeight - r.Height - r.Top;
				break;
			default:
				m_offsetTransform.Y = -r.Top; // move to top by default
				break;
			}
			return;
		}
		if (SizeType == eSizeType.ContentToSizeNoStretch) {
			SizeToContentNoStretch(HorizontalContentAlignment, VerticalContentAlignment);
			return;
		}
		if (SizeType == eSizeType.ContentToSizeStretch) {
			var xscale = ActualWidth / r.Width;
			var yscale = ActualHeight / r.Height;
			m_scaleTransform.CenterX = r.Left;
			m_scaleTransform.CenterY = r.Top;
			m_scaleTransform.ScaleX = xscale;
			m_scaleTransform.ScaleY = yscale;

			m_offsetTransform.X = -r.Left;
			m_offsetTransform.Y = -r.Top;
			return;
		}
		if (SizeType == eSizeType.SizeToContent) {
			if (r.Width > ActualWidth || r.Height > ActualHeight) {
				SizeToContentNoStretch(HorizontalAlignment.Left, VerticalAlignment.Top);
			} else {
				m_scaleTransform.CenterX = r.Left;
				m_scaleTransform.CenterY = r.Top;
				m_scaleTransform.ScaleX = 1;
				m_scaleTransform.ScaleY = 1;

				m_offsetTransform.X = -r.Left; // move to left by default
				m_offsetTransform.Y = -r.Top; // move to top by default
			}
		}
	}

	private void SizeToContentNoStretch(HorizontalAlignment hAlignment, VerticalAlignment vAlignment) {
		var r = m_drawing.Bounds;
		var xscale = ActualWidth / r.Width;
		var yscale = ActualHeight / r.Height;
		var scale = xscale;
		if (scale > yscale) {
			scale = yscale;
		}

		m_scaleTransform.CenterX = r.Left;
		m_scaleTransform.CenterY = r.Top;
		m_scaleTransform.ScaleX = scale;
		m_scaleTransform.ScaleY = scale;

		m_offsetTransform.X = -r.Left;
		if (scale < xscale) {
			switch (HorizontalContentAlignment) {
			case HorizontalAlignment.Center:
				var width = r.Width * scale;
				m_offsetTransform.X = ActualWidth / 2 - width / 2 - r.Left;
				break;
			case HorizontalAlignment.Right:
				m_offsetTransform.X = ActualWidth - r.Right * scale;
				break;
			}
		}
		m_offsetTransform.Y = -r.Top;
		if (scale < yscale) {
			switch (VerticalContentAlignment) {
			case VerticalAlignment.Center:
				var height = r.Height * scale;
				m_offsetTransform.Y = ActualHeight / 2 - height / 2 - r.Top;
				break;
			case VerticalAlignment.Bottom:
				m_offsetTransform.Y = ActualHeight - r.Height * scale - r.Top;
				break;
			}
		}
	}

	private Uri ResolveUri(Uri svgSource) {
		if (svgSource == null) {
			return null;
		}

		if (svgSource.IsAbsoluteUri) {
			return svgSource;
		}

		// Try getting a local file in the same directory....
		var svgPath = svgSource.ToString();
		if (svgPath[0] == '\\' || svgPath[0] == '/') {
			svgPath = svgPath.Substring(1);
		}
		svgPath = svgPath.Replace('/', '\\');

		var assembly = Assembly.GetExecutingAssembly();
		var localFile = Path.Combine(Path.GetDirectoryName(assembly.Location), svgPath);

		if (File.Exists(localFile)) {
			return new Uri(localFile);
		}

		// Try getting it as resource file...
		if (BaseUri != null) {
			return new Uri(BaseUri, svgSource);
		}

		var asmName = assembly.GetName().Name;
		var uriString = string.Format("pack://application:,,,/{0};component/{1}",
			asmName, svgPath);

		return new Uri(uriString);
	}

	/// <summary>
	///     This converts the SVG resource specified by the Uri to <see cref="DrawingGroup" />.
	/// </summary>
	/// <param name="svgSource">A <see cref="Uri" /> specifying the source of the SVG resource.</param>
	/// <returns>A <see cref="DrawingGroup" /> of the converted SVG resource.</returns>
	private DrawingGroup LoadDrawing(Uri svgSource) {
		string scheme = null;
		// A little hack to display preview in design mode: The design mode Uri is not absolute.
		var designTime = DesignerProperties.GetIsInDesignMode(new DependencyObject());
		if (designTime && svgSource.IsAbsoluteUri == false) {
			scheme = "pack";
		} else {
			scheme = svgSource.Scheme;
		}
		if (string.IsNullOrWhiteSpace(scheme)) {
			return null;
		}

		switch (scheme) {
		case "file":
		//case "ftp":
		case "https":
		case "http":
			using (var reader = new FileSvgReader(OverrideColor)) {
				var drawGroup = reader.Read(svgSource, _render);

				if (drawGroup != null) {
					return drawGroup;
				}
			}
			break;
		case "pack":
			StreamResourceInfo svgStreamInfo = null;
			if (svgSource.ToString().IndexOf("siteoforigin", StringComparison.OrdinalIgnoreCase) >= 0) {
				svgStreamInfo = Application.GetRemoteStream(svgSource);
			} else {
				svgStreamInfo = Application.GetResourceStream(svgSource);
			}

			var svgStream = svgStreamInfo != null ? svgStreamInfo.Stream : null;

			if (svgStream != null) {
				var fileExt = Path.GetExtension(svgSource.ToString());
				var isCompressed = !string.IsNullOrWhiteSpace(fileExt) &&
				                   string.Equals(fileExt, ".svgz", StringComparison.OrdinalIgnoreCase);

				if (isCompressed) {
					using (svgStream) {
						using (var zipStream = new GZipStream(svgStream, CompressionMode.Decompress)) {
							using (var reader = new FileSvgReader(OverrideColor)) {
								var drawGroup = reader.Read(zipStream, _render);

								if (drawGroup != null) {
									return drawGroup;
								}
							}
						}
					}
				} else {
					using (svgStream) {
						using (var reader = new FileSvgReader(OverrideColor)) {
							var drawGroup = reader.Read(svgStream, _render);

							if (drawGroup != null) {
								return drawGroup;
							}
						}
					}
				}
			}
			break;
		case "data":
			var sourceData = svgSource.OriginalString.Replace(" ", "");

			var nColon = sourceData.IndexOf(":", StringComparison.OrdinalIgnoreCase);
			var nSemiColon = sourceData.IndexOf(";", StringComparison.OrdinalIgnoreCase);
			var nComma = sourceData.IndexOf(",", StringComparison.OrdinalIgnoreCase);

			var sMimeType = sourceData.Substring(nColon + 1, nSemiColon - nColon - 1);
			var sEncoding = sourceData.Substring(nSemiColon + 1, nComma - nSemiColon - 1);

			if (string.Equals(sMimeType.Trim(), "image/svg+xml", StringComparison.OrdinalIgnoreCase)
			    && string.Equals(sEncoding.Trim(), "base64", StringComparison.OrdinalIgnoreCase)) {
				var sContent = FileSvgReader.RemoveWhitespace(sourceData.Substring(nComma + 1));
				var imageBytes = Convert.FromBase64CharArray(sContent.ToCharArray(),
					0, sContent.Length);
				var isGZiped = sContent.StartsWith(FileSvgReader.GZipSignature, StringComparison.Ordinal);
				if (isGZiped) {
					using (var stream = new MemoryStream(imageBytes)) {
						using (var zipStream = new GZipStream(stream, CompressionMode.Decompress)) {
							using (var reader = new FileSvgReader(OverrideColor)) {
								var drawGroup = reader.Read(zipStream, _render);
								if (drawGroup != null) {
									return drawGroup;
								}
							}
						}
					}
				} else {
					using (var stream = new MemoryStream(imageBytes)) {
						using (var reader = new FileSvgReader(OverrideColor)) {
							var drawGroup = reader.Read(stream, _render);
							if (drawGroup != null) {
								return drawGroup;
							}
						}
					}
				}
			}
			break;
		}

		return null;
	}

	private static void OnUriSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args) {
		var svgImage = obj as SVGImage;
		if (svgImage == null) {
			return;
		}

		var sourceUri = (Uri)args.NewValue;
		svgImage.SetImage(sourceUri);
	}

	private static void OnSizeTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var ctrl = d as SVGImage;
		ctrl.RecalcImage();
		ctrl.InvalidateVisual();
	}

	private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var resource = e.NewValue != null ? Application.GetResourceStream(new Uri(e.NewValue.ToString(), UriKind.Relative)) : null;
		((SVGImage)d).SetImage(resource != null ? resource.Stream : null);
	}

	private static void OnFileSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		((SVGImage)d).SetImage(new FileStream(e.NewValue.ToString(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
	}

	private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		((SVGImage)d).SetImage(e.NewValue as Drawing);
	}

	private static void OverrideColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if (d is SVGImage svgImage && e.NewValue is Color newColor && svgImage._render != null) {
			svgImage._render.OverrideColor = newColor;
			svgImage.InvalidateVisual();
			svgImage.ReRenderSvg();
		}
	}

	private static void OverrideStrokeWidthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if (d is SVGImage svgImage && e.NewValue is double newStrokeWidth && svgImage._render != null) {
			svgImage._render.OverrideStrokeWidth = newStrokeWidth;
			svgImage.InvalidateVisual();
			svgImage.ReRenderSvg();
		}
	}

	private static void CustomBrushesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if (d is SVGImage svgImage && e.NewValue is Dictionary<string, Brush> newBrushes) {
			if (svgImage._render != null) {
				if (svgImage._render.CustomBrushes != null) {
					var newCustomBrushes = new Dictionary<string, Brush>(svgImage._render.CustomBrushes);
					foreach (var brush in newBrushes) {
						newCustomBrushes[brush.Key] = brush.Value;
					}
					svgImage._render.CustomBrushes = newCustomBrushes;
				} else {
					svgImage._render.CustomBrushes = newBrushes;
				}
			}
			svgImage.InvalidateVisual();
			svgImage.ReRenderSvg();
		}
	}
}