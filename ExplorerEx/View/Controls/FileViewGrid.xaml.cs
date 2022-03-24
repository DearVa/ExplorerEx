using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExplorerEx.Command;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using ExplorerEx.ViewModel;
using hc = HandyControl.Controls;
using TextBox = HandyControl.Controls.TextBox;

namespace ExplorerEx.View.Controls;

/// <summary>
/// 对应一个Tab，但是VisualTree中，这个Grid只有一个，切换Tab的时候ViewModel会随之切换
/// </summary>
public partial class FileViewGrid {
	/// <summary>
	/// 创建文件或文件夹
	/// </summary>
	public SimpleCommand CreateCommand { get; }

	public FileGridViewModel GridViewModel { get; private set; }

	public FileViewGrid() {
		CreateCommand = new SimpleCommand(e => {
			if (e is MenuItem menuItem) {
				Create((CreateFileItem)menuItem.DataContext);
			}
		});
		DataContextChanged += DataContext_OnChanged;

		InitializeComponent();
	}

	private void DataContext_OnChanged(object sender, DependencyPropertyChangedEventArgs e) {
		GridViewModel = (FileGridViewModel)e.NewValue;
		if (GridViewModel != null) {
			GridViewModel.FileListView = FileGrid;
		}
	}

	/// <summary>
	/// 创建文件或文件夹
	/// </summary>
	public void Create(CreateFileItem item) {
		var viewModel = GridViewModel;
		if (viewModel.PathType == PathType.Home) {
			return;
		}
		try {
			FileGrid.StartRename(item.Create(viewModel.FullPath));
		} catch (Exception e) {
			hc.MessageBox.Error(e.Message, "Cannot_create".L());
		}
	}

	private async void AddressBar_OnKeyDown(object sender, KeyEventArgs e) {
		switch (e.Key) {
		case Key.Enter:
			await GridViewModel.LoadDirectoryAsync(((TextBox)sender).Text);
			break;
		}
	}

	private async void History_OnClick(object sender, RoutedEventArgs e) {
		await GridViewModel.LoadDirectoryAsync(((FileItem)((MenuItem)sender).DataContext).FullPath);
	}
}