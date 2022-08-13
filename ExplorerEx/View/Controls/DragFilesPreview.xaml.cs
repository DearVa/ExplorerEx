using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ExplorerEx.Utils;
using HandyControl.Controls;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 拖放文件时，这个显示拖放的文件缩略图和操作
/// </summary>
public partial class DragFilesPreview {
	public static DragFilesPreview Singleton { get; } = new();

	public static bool IsShown { get; private set; }

	/// <summary>
	/// 是否为程序本身的拖放
	/// </summary>
	public static bool IsInternalDrag { get; set; }

	private static readonly DragPreviewWindow DragPreviewWindow;

	public string? Destination {
		get => destination;
		set {
			if (destination != value) {
				destination = value;
				FormatOperation();
			}
		}
	}

	private string? destination;

	public DragDropEffects Icon {
		get => icon;
		set {
			if (icon != value) {
				icon = value;
				switch (value) {
				case DragDropEffects.Copy:
					CopyPath.Visibility = Visibility.Visible;
					MovePath.Visibility = Visibility.Collapsed;
					LinkPath.Visibility = Visibility.Collapsed;
					break;
				case DragDropEffects.Move:
					CopyPath.Visibility = Visibility.Collapsed;
					MovePath.Visibility = Visibility.Visible;
					LinkPath.Visibility = Visibility.Collapsed;
					break;
				case DragDropEffects.Link:
					CopyPath.Visibility = Visibility.Collapsed;
					MovePath.Visibility = Visibility.Collapsed;
					LinkPath.Visibility = Visibility.Visible;
					break;
				}
			}
		}
	}

	private DragDropEffects icon;

	public DragDropEffects DragDropEffect {
		get => dragDropEffect;
		set {
			if (dragDropEffect == value) {
				return;
			}
			Trace.WriteLine(value);
			Icon = value;
			switch (value) {
			case DragDropEffects.Copy:
				OperationText = "DragCopyTo".L();
				OperationBorder.Visibility = Visibility.Visible;
				break;
			case DragDropEffects.Move:
				OperationText = "DragMoveTo".L();
				OperationBorder.Visibility = Visibility.Visible;
				break;
			case DragDropEffects.Link:
				OperationText = "DragLinkTo".L();
				OperationBorder.Visibility = Visibility.Visible;
				break;
			case DragDropEffects.All:  // 自定义
				OperationBorder.Visibility = Visibility.Visible;
				break;
			case DragDropEffects.Scroll:
				return;
			case DragDropEffects.None:
				OperationBorder.Visibility = Visibility.Collapsed;
				break;
			}
			dragDropEffect = value;
		}
	}

	private DragDropEffects dragDropEffect;

	public string? OperationText {
		get => operationText;
		set {
			if (operationText != value) {
				operationText = value;
				FormatOperation();
			}
		}
	}

	private string? operationText;

	private DragFilesPreview() {
		InitializeComponent();
	}

	static DragFilesPreview() {
		DragPreviewWindow = new DragPreviewWindow(Singleton, new Point(50, 100), 0.8, false);
	}

	public void SetFilePaths(IList<string> filePaths) {
		DragCountTextBlock.Text = filePaths.Count.ToString();
		if (filePaths.Count < 3) {
			DragImage2Border.Visibility = Visibility.Collapsed;
			if (filePaths.Count < 2) {
				DragImage1Border.Visibility = Visibility.Collapsed;
			}
		}
		Task.Run(() => {
			var thumbnails = new ImageSource[filePaths.Count > 3 ? 3 : filePaths.Count];
			if (filePaths.Count > 0) {
				thumbnails[0] = GetPathThumbnail(filePaths[0]);
				Dispatcher.BeginInvoke(() => {
					DragImage0.Source = thumbnails[0];
					DragImage0.Visibility = Visibility.Visible;
				});
				if (filePaths.Count > 1) {
					thumbnails[1] = GetPathThumbnail(filePaths[1]);
					Dispatcher.BeginInvoke(() => {
						DragImage1.Source = thumbnails[1];
						DragImage1Border.Visibility = Visibility.Visible;
					});
					if (filePaths.Count > 2) {
						// 最多只显示三个缩略图
						thumbnails[2] = GetPathThumbnail(filePaths[2]);
						Dispatcher.BeginInvoke(() => {
							DragImage2.Source = thumbnails[2];
							DragImage2Border.Visibility = Visibility.Visible;
						});
					}
				}
			}
		});
	}

	private static ImageSource GetPathThumbnail(string path) {
		if (path.Length == 3) {
			return IconHelper.GetDriveThumbnail(path);
		}
		if (Directory.Exists(path)) {
			return IconHelper.EmptyFolderDrawingImage;
		}
		return IconHelper.GetPathThumbnail(path);
	}

	private static readonly Brush DestBrush = new SolidColorBrush(Color.FromRgb(13, 61, 206));
	private static readonly Brush OpBrush = new SolidColorBrush(Color.FromRgb(5, 5, 105));

	private void FormatOperation() {
		OperationTextBlock.Inlines.Clear();
		if (operationText == null) {
			return;
		}
		if (operationText[0] == '*') {
			OperationTextBlock.Inlines.Add(new Run(Destination) { Foreground = DestBrush });
			OperationTextBlock.Inlines.Add(new Run(operationText[1..]) { Foreground = OpBrush });
		} else if (operationText[^1] == '*') {
			OperationTextBlock.Inlines.Add(new Run(operationText[..^1]) { Foreground = OpBrush });
			OperationTextBlock.Inlines.Add(new Run(Destination) { Foreground = DestBrush });
		} else {
			for (var i = 0; i < operationText.Length; i++) {
				if (operationText[i] == '*') {
					OperationTextBlock.Inlines.Add(new Run(operationText[..i]) { Foreground = OpBrush });
					OperationTextBlock.Inlines.Add(new Run(Destination) { Foreground = DestBrush });
					OperationTextBlock.Inlines.Add(new Run(operationText[(i + 1)..]) { Foreground = OpBrush });
					break;
				}
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ShowPreview() {
		DragPreviewWindow.MoveWithCursor();
		DragPreviewWindow.Show();
		IsShown = true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void HidePreview() {
		Singleton.DragImage0.Visibility = Visibility.Collapsed;
		DragPreviewWindow.Hide();
		IsShown = false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void MoveWithCursor() {
		DragPreviewWindow.MoveWithCursor();
	}
}