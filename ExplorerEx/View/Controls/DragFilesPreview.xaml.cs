using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ExplorerEx.Utils;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 拖放文件时，这个显示拖放的文件缩略图和操作
/// </summary>
public partial class DragFilesPreview {
	public string Destination {
		set {
			if (value == null) {
				OperationBorder.Visibility = Visibility.Collapsed;
			} else {
				OperationBorder.Visibility = Visibility.Visible;
				DestinationRun.Text = value;
			}
		}
	}

	public DragDropEffects DragDropEffect {
		set {
			switch (value) {
			case DragDropEffects.Copy:
				OperationTypeRun.Text = "Copy_to".L();
				CopyPath.Visibility = Visibility.Visible;
				MovePath.Visibility = Visibility.Collapsed;
				LinkPath.Visibility = Visibility.Collapsed;
				break;
			case DragDropEffects.Move:
				OperationTypeRun.Text = "Move_to".L();
				CopyPath.Visibility = Visibility.Collapsed;
				MovePath.Visibility = Visibility.Visible;
				LinkPath.Visibility = Visibility.Collapsed;
				break;
			case DragDropEffects.Link:
				OperationTypeRun.Text = "Link_to".L();
				CopyPath.Visibility = Visibility.Collapsed;
				MovePath.Visibility = Visibility.Collapsed;
				LinkPath.Visibility = Visibility.Visible;
				break;
			case DragDropEffects.All:  // 自定义
				OperationTypeRun.Text = CustomOperation;
				OperationBorder.Visibility = Visibility.Visible;
				CopyPath.Visibility = Visibility.Collapsed;
				MovePath.Visibility = Visibility.Collapsed;
				LinkPath.Visibility = Visibility.Collapsed;
				break;
			}
		}
	}

	public string CustomOperation { get; set; }

	public DragFilesPreview(IList<ImageSource> icons) {
		InitializeComponent();
		DragCountTextBlock.Text = icons.Count.ToString();
		if (icons.Count > 0) {
			DragImage0.Source = icons[0];
			if (icons.Count > 1) {
				DragImage1.Source = icons[1];
				DragImage1Border.Visibility = Visibility.Visible;
				if (icons.Count > 2) {
					DragImage2.Source = icons[2];  // 只显示三个缩略图
					DragImage2Border.Visibility = Visibility.Visible;
				}
			}
		}
		DragDropEffect = DragDropEffects.Copy;
	}
}