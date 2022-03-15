using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HandyControl.Controls;
using SharpVectors.Renderers.Wpf;
using SvgConverter;

namespace ExplorerEx.View.Controls;

public partial class ImagePreviewPopup {
	public static ImagePreviewPopup Instance { get; } = new();

	static ImagePreviewPopup() { }

	private ImagePreviewPopup() {
		InitializeComponent();
	}

	public override void Load(string filePath) {
		if (!File.Exists(filePath)) {
			return;
		}
		FilePath = filePath;
		switch (Path.GetExtension(filePath).ToLower()) {
		case ".gif":
			Image.Visibility = Visibility.Collapsed;
			GifImage.Uri = new Uri(filePath, UriKind.Absolute);
			GifImage.Visibility = Visibility.Visible;
			break;
		case ".svg":
			GifImage.Visibility = Visibility.Collapsed;
			Image.Source = (DrawingImage)ConverterLogic.ConvertSvgToObject(filePath, ResultMode.DrawingImage, new WpfDrawingSettings(), out _, new ResKeyInfo());
			Image.Visibility = Visibility.Visible;
			break;
		default:
			GifImage.Visibility = Visibility.Collapsed;
			Image.Source = new BitmapImage(new Uri(filePath, UriKind.Absolute));
			Image.Visibility = Visibility.Visible;
			break;
		}
		IsOpen = true;
	}

	public override void Close() {
		IsOpen = false;
		Image.Source = null;
		Image.Visibility = Visibility.Collapsed;
		GifImage.Uri = null;
		GifImage.Visibility = Visibility.Collapsed;
		FilePath = null;
	}

	public override void HandleMouseScroll(MouseWheelEventArgs e) { }
}