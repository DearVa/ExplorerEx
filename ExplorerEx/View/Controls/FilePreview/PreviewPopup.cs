using System.IO;
using HandyControl.Controls;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ExplorerEx.View.Controls; 

/// <summary>
/// 视频、音乐和文档预览的弹出窗口
/// </summary>
public abstract class PreviewPopup : Popup {
	public static readonly DependencyProperty FilePathProperty = DependencyProperty.Register(
		nameof(FilePath), typeof(string), typeof(PreviewPopup), new PropertyMetadata(default(string)));

	public string FilePath {
		get => (string)GetValue(FilePathProperty);
		set => SetValue(FilePathProperty, value);
	}

	protected PreviewPopup() {
		SetValue(BlurPopup.EnabledProperty, true);
		PopupAnimation = PopupAnimation.Fade;
		AllowsTransparency = true;
		Focusable = false;
		IsHitTestVisible = false;
		Placement = PlacementMode.Relative;
	}

	public static PreviewPopup? ChoosePopup(string filePath) {
		if (filePath is { Length: > 5 }) {  // 驱动器占3个长度
			switch (Path.GetExtension(filePath).ToLower()) {
			case ".mp3" or ".wav" or ".flac":
				return MusicPreviewPopup.Instance;
			case ".avi" or ".wmv" or ".mpeg" or ".mp4" or ".m4v" or ".mov":
				return VideoPreviewPopup.Instance;
			case ".txt" or ".ini" or ".bash" or ".sh" or ".bat" or ".cmd" or ".cmake" or
				".c" or ".i" or ".cpp" or ".h" or ".hpp" or ".cs" or ".css" or ".cu" or ".cuh" or
				".diff" or ".dockerfile" or ".fs" or ".go" or ".hlsl" or ".fx" or ".cginc" or
				".compute" or ".vsh" or ".psh" or ".html" or ".htm" or ".shtml" or ".xhtml" or
				".jsp" or ".aspx" or ".asp" or ".jshtm" or ".gitignore" or ".gitignore_global" or
				".java" or ".js" or ".json" or ".kt" or ".kts" or ".tex" or ".ltx" or ".lua" or
				".md" or ".m" or ".mm" or ".php" or ".ps1" or ".py" or ".r" or ".reg" or ".rb" or
				".rs" or ".shader" or ".cg" or ".smali" or ".sql" or ".swift" or ".ts" or ".vb" or
				".vbs" or ".vba" or ".xml" or "xaml" or ".csproj" or ".sln" or ".yml" or ".config" or ".log" or
				".vsconfig" or ".inf":
				return TextPreviewPopup.Instance;
			case ".jpg" or ".jpeg" or ".bmp" or ".png" or ".svg" or ".gif":
				return ImagePreviewPopup.Instance;
			}
		}
		return null;
	}

	public abstract void Load(string filePath);

	public abstract void Close();

	public abstract void HandleMouseScroll(MouseWheelEventArgs e);
}