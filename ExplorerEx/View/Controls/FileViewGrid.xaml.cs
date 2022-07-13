using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using ExplorerEx.ViewModel;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 对应一个Tab，但是VisualTree中，这个Grid只有一个，切换Tab的时候ViewModel会随之切换
/// </summary>
public partial class FileViewGrid {
	public FileTabViewModel TabViewModel { get; private set; }

	public FileViewGrid() {
		DataContextChanged += DataContext_OnChanged;

		InitializeComponent();
	}

	private void DataContext_OnChanged(object sender, DependencyPropertyChangedEventArgs e) {
		TabViewModel = (FileTabViewModel)e.NewValue;
		if (TabViewModel != null) {
			TabViewModel.FileListView = FileListView;
		}
	}

	private async void AddressBar_OnPreviewKeyDown(object sender, KeyEventArgs e) {
		var addressBar = (AddressBar)sender;
		switch (e.Key) {
		case Key.Enter:
			await TabViewModel.LoadDirectoryAsync(addressBar.Text);
			TabViewModel.OwnerWindow.ClearTextBoxFocus();
			e.Handled = true;
			break;
		case Key.Escape:
			TabViewModel.OwnerWindow.ClearTextBoxFocus();
			e.Handled = true;
			break;
		}
	}

	private async void History_OnClick(object sender, RoutedEventArgs e) {
		try {
			await TabViewModel.LoadDirectoryAsync(((FileListViewItem)((MenuItem)sender).DataContext).FullPath);
		} catch (Exception ex) {
			Logger.Exception(ex);
		}
	}

	private async void AddressBar_OnPopupItemClicked(FolderOnlyItem onlyItem) {
		try {
			await TabViewModel.LoadDirectoryAsync(onlyItem.FullPath);
		} catch (Exception ex) {
			Logger.Exception(ex);
		}
	}
}