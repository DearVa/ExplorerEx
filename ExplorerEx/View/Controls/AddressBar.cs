using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ExplorerEx.Command;
using ExplorerEx.Model;
using ExplorerEx.Utils;

namespace ExplorerEx.View.Controls;

internal class AddressBar : TextBox {
	public static readonly DependencyProperty FullPathProperty = DependencyProperty.Register(
		nameof(FullPath), typeof(string), typeof(AddressBar), new PropertyMetadata(null, FullPath_OnChanged));

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
	public ObservableCollection<FolderOnlyItem> Items { get; } = new();

	public SimpleCommand ItemClickedCommand { get; }

	public event Action<FolderOnlyItem>? PopupItemClicked;

	private ScrollViewer scrollViewer = null!, contentHost = null!;

	public AddressBar() {
		ItemClickedCommand = new SimpleCommand(Item_OnClicked);
		Items.Add(FolderOnlyItem.Home);  // index0一定是Home
		Text = "ThisPC".L();
	}

	private void Item_OnClicked(object? args) {
		switch (args) {
		case FolderOnlyItem item:
			PopupItemClicked?.Invoke(item);
			break;
		case RoutedEventArgs { OriginalSource: MenuItem { DataContext: FolderOnlyItem item } }:
			item.Parent.IsExpanded = false;
			PopupItemClicked?.Invoke(item);
			break;
		}
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		scrollViewer = (ScrollViewer)GetTemplateChild("ScrollViewer")!;
		contentHost = (ScrollViewer)GetTemplateChild("PART_ContentHost")!;
	}

	protected override void OnGotFocus(RoutedEventArgs e) {
		base.OnGotFocus(e);
		scrollViewer.Visibility = Visibility.Collapsed;
		contentHost.Visibility = Visibility.Visible;
		Dispatcher.BeginInvoke(SelectAll);
	}

	protected override void OnLostFocus(RoutedEventArgs e) {
		base.OnLostFocus(e);
		contentHost.Visibility = Visibility.Collapsed;
		scrollViewer.Visibility = Visibility.Visible;
		Text = FullPath;
	}

	private static void FullPath_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var newPath = (string?)e.NewValue;
		if (newPath == null) {
			return;
		}
		var addressBar = (AddressBar)d;
		var items = addressBar.Items;
		if (newPath == "$Home") {  // 新路径是主页，就清空，只留第一个
			addressBar.Text = "ThisPC".L();
			while (items.Count > 1) {
				items.RemoveAt(items.Count - 1);
			}
			return;
		}
		if (newPath.Length < 3) {
			return;
		}
		newPath = newPath.Replace('/', '\\');
		if (newPath.Length > 3) {
			newPath = newPath.TrimEnd('\\');
		}
		addressBar.Text = newPath;
		var zipPathIndex = -1;
		var n = 1;
		for (var i = 0; i < newPath.Length; i++) {
			if (newPath[i] == '\\') {
				var fullPath = n == 1 ? newPath[..(i + 1)] : newPath[..i];
				if (n < items.Count) {
					if (items[n].FullPath != fullPath) {
						if (n == 1) {
							items[n] = new FolderOnlyItem(new DriveInfo(fullPath[..1]));
						} else if (zipPathIndex != -1) {
							items[n] = new FolderOnlyItem(fullPath[..zipPathIndex], fullPath[(zipPathIndex + 1)..], items[n - 1]);
						} else if (fullPath.EndsWith(".zip")) {
							items[n] = new FolderOnlyItem(fullPath, string.Empty, items[n - 1]);
							zipPathIndex = i;
						} else {
							items[n] = new FolderOnlyItem(new DirectoryInfo(fullPath), items[n - 1]);
						}
					} else if (fullPath.EndsWith(".zip")) {
						zipPathIndex = i;
					}
				} else {
					if (n == 1) {
						items.Add(new FolderOnlyItem(new DriveInfo(fullPath[..1])));
					} else if (zipPathIndex != -1) {
						items.Add(new FolderOnlyItem(fullPath[..zipPathIndex], fullPath[(zipPathIndex + 1)..], items[n - 1]));
					} else if (fullPath.EndsWith(".zip")) {
						items.Add(new FolderOnlyItem(fullPath, string.Empty, items[n - 1]));
						zipPathIndex = i;
					} else {
						items.Add(new FolderOnlyItem(new DirectoryInfo(fullPath), items[n - 1]));
					}
				}
				n++;
			}
		}
		while (items.Count > n) {
			items.RemoveAt(items.Count - 1);
		}
		if (newPath.Length > 3) {
			if (zipPathIndex != -1) {
				items.Add(new FolderOnlyItem(newPath[..zipPathIndex], newPath[(zipPathIndex + 1)..], items[n - 1]));
			} else if (newPath.EndsWith(".zip") && File.Exists(newPath)) {
				items.Add(new FolderOnlyItem(newPath, string.Empty, items[^1]));
			} else if (Directory.Exists(newPath)) {
				items.Add(new FolderOnlyItem(new DirectoryInfo(newPath), items[^1]));
			}
		}
		// Template may not applied yet.
		addressBar.Dispatcher.BeginInvoke(() => addressBar.scrollViewer.ScrollToRightEnd(), DispatcherPriority.Loaded);
	}
}