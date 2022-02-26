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
	}

	private void OpenButton_OnClick(object sender, RoutedEventArgs e) {
		App.Instance.OpenWindow(null);
	}

	private void ExitButton_OnClick(object sender, RoutedEventArgs e) {
		NotifyIconContextContent.Dispose();
		Application.Current.Shutdown();
	}
}