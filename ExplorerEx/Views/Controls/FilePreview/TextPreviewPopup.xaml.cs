using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using ExplorerEx.Utils;

namespace ExplorerEx.Views.Controls;

public sealed partial class TextPreviewPopup {
	public static TextPreviewPopup Instance { get; } = new();

	/// <summary>
	/// 文件名，ScrollViewer的Offset
	/// </summary>
	private readonly Dictionary<string, double> viewHistory = new();

	private readonly byte[] buffer = new byte[10240];

	static TextPreviewPopup() { }

	private TextPreviewPopup() {
		InitializeComponent();
	}

	public override void Load(string filePath) {
		if (!File.Exists(filePath)) {
			return;
		}
		if (FilePath != null) {
			viewHistory[FilePath] = ScrollViewer.VerticalOffset;
		}
		FilePath = filePath;
		StatusTextBlock.Text = Path.GetFileName(filePath);
		if (FileUtils.IsTextFile(out var encoding, filePath)) {
			TextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe6));
			using var fs = File.OpenRead(filePath);
			var length = fs.Read(buffer);
			TextBlock.Text = encoding.GetString(buffer, 0, length);
			if (viewHistory.TryGetValue(filePath, out var offsetY)) {
				ScrollViewer.ScrollToVerticalOffset(offsetY);
			} else {
				ScrollViewer.ScrollToHome();
			}
		} else {
			TextBlock.Foreground = Brushes.Yellow;
			TextBlock.Text = "#Unsupported_encoding".L();
		}
		IsOpen = true;
	}

	public override void Close() {
		IsOpen = false;
		TextBlock.Text = null;
		if (FilePath != null) {
			viewHistory[FilePath] = ScrollViewer.VerticalOffset;
			FilePath = null;
		}
	}

	public override void HandleMouseScroll(MouseWheelEventArgs e) {
		ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset - e.Delta);
	}
}