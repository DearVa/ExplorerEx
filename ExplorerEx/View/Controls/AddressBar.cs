using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ExplorerEx.Command;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using HandyControl.Tools;

namespace ExplorerEx.View.Controls;

internal class AddressBar : TextBox {
	public static readonly DependencyProperty FullPathProperty = DependencyProperty.Register(
		"FullPath", typeof(string), typeof(AddressBar), new PropertyMetadata(null, FullPath_OnChanged));

	/// <summary>
	/// 地址路径
	/// </summary>
	public string FullPath {
		get => (string)GetValue(FullPathProperty);
		set => SetValue(FullPathProperty, value);
	}

	/// <summary>
	/// 路径上的每个文件夹
	/// </summary>
	public ObservableCollection<FolderItem> Items { get; } = new();

	public SimpleCommand ItemClickedCommand { get; }

	public event Action<FolderItem> PopupItemClicked;

	private ScrollViewer scrollViewer, contentHost;

	public AddressBar() {
		ItemClickedCommand = new SimpleCommand(Item_OnClicked);
		Items.Add(FolderItem.Home);  // index0一定是Home
	}

	private void Item_OnClicked(object args) {
		if (args is not FolderItem item) {
			item = (FolderItem)((MenuItem)((RoutedEventArgs)args).OriginalSource).DataContext;
			item.Parent.IsExpanded = false;
		}
		PopupItemClicked?.Invoke(item);
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		scrollViewer = (ScrollViewer)GetTemplateChild("ScrollViewer")!;
		scrollViewer.PreviewMouseWheel += ScrollViewer_OnPreviewMouseWheel;
		contentHost = (ScrollViewer)GetTemplateChild("PART_ContentHost");
	}

	private void ScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
		scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta / 6d);
	}

	protected override void OnGotFocus(RoutedEventArgs e) {
		base.OnGotFocus(e);
		scrollViewer.Visibility = Visibility.Collapsed;
		contentHost.Visibility = Visibility.Visible;
	}

	protected override void OnLostFocus(RoutedEventArgs e) {
		base.OnLostFocus(e);
		contentHost.Visibility = Visibility.Collapsed;
		scrollViewer.Visibility = Visibility.Visible;
		Text = FullPath ?? "ThisPC".L();
	}

	private static void FullPath_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var addressBar = (AddressBar)d;
		var items = addressBar.Items;
		var newPath = (string)e.NewValue;
		if (newPath == null) {  // 新路径是主页，就清空，只留第一个
			addressBar.Text = "ThisPC".L();
			while (items.Count > 1) {
				items.RemoveAt(items.Count - 1);
			}
			return;
		}
		addressBar.Text = newPath;
		if (newPath.Length < 3) {
			return;
		}
		var n = 1;
		for (var i = 0; i < newPath.Length; i++) {
			if (newPath[i] == '\\') {
				if (n < items.Count) {
					var fullPath = Path.GetFullPath(newPath[..(i + 1)]);
					if (items[n].FullPath != fullPath) {
						if (n == 1) {
							items[n] = new FolderItem(new DriveInfo(fullPath[..1]));
						} else {
							items[n] = new FolderItem(new DirectoryInfo(fullPath), items[n - 1]);
						}
					}
				} else {
					if (n == 1) {
						items.Add(new FolderItem(new DriveInfo(newPath[..1])));
					} else {
						items.Add(new FolderItem(new DirectoryInfo(newPath[..(i + 1)]), items[n - 1]));
					}
				}
				n++;
			}
		}
		while (items.Count > n) {
			items.RemoveAt(items.Count - 1);
		}
		if (newPath[^1] != '\\') {
			items.Add(new FolderItem(new DirectoryInfo(newPath), items[^1]));
		}
		addressBar.scrollViewer.ScrollToRightEnd();
	}
}