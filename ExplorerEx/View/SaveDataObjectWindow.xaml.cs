using System;
using System.Windows;
using ExplorerEx.ViewModel;

namespace ExplorerEx.View; 

/// <summary>
/// 将文本、链接或者图像存成文件
/// </summary>
public partial class SaveDataObjectWindow {
	public SaveDataObjectWindow(string basePath, string textOrLink, Point startupLocation) {
		DataContext = new SaveDataObjectViewModel(this, basePath, textOrLink);
		InitializeComponent();
		Left = Math.Min(SystemParameters.PrimaryScreenWidth - Width, Math.Max(0, startupLocation.X));
		Top = Math.Min(SystemParameters.PrimaryScreenHeight - Height, Math.Max(0, startupLocation.Y));
	}
}