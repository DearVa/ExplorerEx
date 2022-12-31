using System.Windows;

namespace ExplorerEx.Views.Controls;

public class FileListViewBindingContext : DependencyObject {
	public static readonly DependencyProperty FileListViewProperty = DependencyProperty.Register(
		nameof(FileListView), typeof(FileListView), typeof(FileListViewBindingContext), new PropertyMetadata(default(FileListView)));

	public FileListView FileListView {
		get => (FileListView)GetValue(FileListViewProperty);
		set => SetValue(FileListViewProperty, value);
	}
}