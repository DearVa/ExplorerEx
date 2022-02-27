using System.Windows;

namespace ExplorerEx.View; 

/// <summary>
/// 显示一个托盘图标
/// </summary>
public partial class NotifyIconWindow {
	public NotifyIconWindow() {
		DataContext = this;
		
		InitializeComponent();
		NotifyIconContextContent.Init();
		NotifyIconContextContent.MouseDoubleClick += ShowWindow;
	}

	private void ShowWindow(object sender, RoutedEventArgs e) {
		MainWindow.ShowWindow();
		NotifyIconContextContent.CloseContextControl();
	}

	private void ExitButton_OnClick(object sender, RoutedEventArgs e) {
		NotifyIconContextContent.Dispose();
		Application.Current.Shutdown();
	}
}