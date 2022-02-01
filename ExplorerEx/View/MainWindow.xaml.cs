using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExplorerEx.Model;
using ExplorerEx.ViewModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ExplorerEx.View;

public sealed partial class MainWindow {
	private readonly MainWindowViewModel viewModel;

	public MainWindow() {
		DataContext = viewModel = new MainWindowViewModel(this);
		InitializeComponent();
		//var hwnd = WindowNative.GetWindowHandle(this);
		//var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
		//titleBar = AppWindow.GetFromWindowId(windowId).TitleBar;
		//titleBar.ExtendsContentIntoTitleBar = true;
		//SetTitleBar(TitleBarGrid);
	}

	protected override void OnPreviewMouseUp(MouseButtonEventArgs e) {
		switch (e.ChangedButton) {
		case MouseButton.XButton1:  // 鼠标侧键返回
			viewModel.SelectedTab.GoBackAsync(null, null);
			break;
		case MouseButton.XButton2:
			viewModel.SelectedTab.GoForwardAsync(null, null);
			break;
		}
		base.OnPreviewMouseUp(e);
	}

	private async void AddressBar_OnKeyDown(object sender, KeyEventArgs e) {
		switch (e.Key) {
		case Key.Enter:
			await viewModel.SelectedTab.LoadDirectoryAsync(((TextBox)sender).Text);
			break;
		}
	}

	private async void DataGrid_OnMouseUp(object sender, MouseButtonEventArgs e) {
		if (ItemsControl.ContainerFromElement((DataGrid)sender, (DependencyObject)e.OriginalSource) is DataGridRow row) {
			await viewModel.SelectedTab.Item_OnMouseUp((FileViewBaseItem)row.Item);
		} else {
			viewModel.SelectedTab.ClearSelection();
		}
	}

	private async void HomeListBox_OnMouseUp(object sender, MouseButtonEventArgs e) {
		if (ItemsControl.ContainerFromElement((ListBox)sender, (DependencyObject)e.OriginalSource) is ListBoxItem item) {
			await viewModel.SelectedTab.Item_OnMouseUp((FileViewBaseItem)item.Content);
		} else {
			viewModel.SelectedTab.ClearSelection();
		}
	}
}