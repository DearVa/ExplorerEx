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

	public MainWindow() : this(null) { }

	public MainWindow(string path) {
		DataContext = viewModel = new MainWindowViewModel(this, path);
		InitializeComponent();
	}

	protected override void OnPreviewMouseUp(MouseButtonEventArgs e) {
		switch (e.ChangedButton) {
		case MouseButton.XButton1:  // 鼠标侧键返回
			viewModel.SelectedTab.GoBackAsync();
			break;
		case MouseButton.XButton2:
			viewModel.SelectedTab.GoForwardAsync();
			break;
		}
		base.OnPreviewMouseUp(e);
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e) {
		if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
			switch (e.Key) {
			case Key.Z:
				break;
			case Key.X:
				viewModel.SelectedTab.Copy(true);
				break;
			case Key.C:
				viewModel.SelectedTab.Copy(false);
				break;
			case Key.V:
				viewModel.SelectedTab.Paste();
				break;
			}
		} else {
			switch (e.Key) {
			case Key.Delete:
				viewModel.SelectedTab.Delete((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift);
				break;
			default:
				base.OnPreviewKeyDown(e);
				return;
			}
		}
		e.Handled = true;
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