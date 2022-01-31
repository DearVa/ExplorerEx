using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ExplorerEx.Selector;
using ExplorerEx.Utils;
using ExplorerEx.View;

namespace ExplorerEx.ViewModel; 

internal class MainWindowViewModel : ViewModelBase {
	public ObservableCollection<MenuItem> NewButtonItems { get; } = new();

	/// <summary>
	/// 标签页
	/// </summary>
	public ObservableCollection<FileViewTabViewModel> TabViewItems { get; } = new();

	public int TabViewSelectedIndex {
		get => tabViewSelectedIndex;
		set {
			if (TabViewItems.Count == 0) {
				return;
			}
			if (value < 0) {
				value = 0;
			} else if (value >= TabViewItems.Count) {
				value = TabViewItems.Count - 1;
			}
			if (tabViewSelectedIndex != value) {
				tabViewSelectedIndex = value;
				OnPropertyChanged();
			}
		}
	}

	private int tabViewSelectedIndex;

	public FileViewTabViewModel SelectedTab => TabViewItems[tabViewSelectedIndex];

	private readonly MainWindow mainWindow;

	public MainWindowViewModel(MainWindow mainWindow) {
		this.mainWindow = mainWindow;
		TabViewItems.Add(new FileViewTabViewModel());
	}
}