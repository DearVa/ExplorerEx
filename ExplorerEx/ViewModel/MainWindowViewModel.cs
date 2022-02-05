using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using ExplorerEx.Utils;
using ExplorerEx.View;
using HandyControl.Data;
using MessageBox = HandyControl.Controls.MessageBox;

namespace ExplorerEx.ViewModel;

public class MainWindowViewModel : ViewModelBase {
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

	public SimpleCommand TabClosingCommand { get; }

	public SimpleCommand NewTabCommand { get; }

	private readonly MainWindow mainWindow;

	public MainWindowViewModel(MainWindow mainWindow) {
		this.mainWindow = mainWindow;

		TabClosingCommand = new SimpleCommand(OnTabClosing);
		NewTabCommand = new SimpleCommand(OnNewTab);

		TabViewItems.Add(new FileViewTabViewModel(this));
	}

	public async Task StartUpLoad(string path) {
		if (!await SelectedTab.LoadDirectoryAsync(path)) {
			mainWindow.Close();
		}
	}

	public async Task OpenPathInNewTabAsync(string path) {
		var newTabIndex = mainWindow.MainTabControl.SelectedIndex + 1;
		var item = new FileViewTabViewModel(this);
		TabViewItems.Insert(newTabIndex, item);
		TabViewSelectedIndex = newTabIndex;
		if (!await SelectedTab.LoadDirectoryAsync(path)) {
			if (TabViewItems.Count > 1) {
				TabViewItems.Remove(item);
			} else {
				mainWindow.Close();
			}
		}
	}

	private async void OnTabClosing(object args) {
		var e = (CancelRoutedEventArgs)args;
		if (TabViewItems.Count <= 1) {
			e.Cancel = true;
			e.Handled = true;
			switch (ConfigHelper.LoadInt("LastTabClosed")) {
			case 1:
				Application.Current.Shutdown();
				break;
			case 2:
				await SelectedTab.LoadDirectoryAsync(null);
				break;
			default:
				var msi = new MessageBoxInfo {
					Button = MessageBoxButton.OKCancel,
					OkButtonText = "Exit_application".L(),
					CancelButtonText = "Back_to_home".L(),
					Message = "You_closed_the_last_tab_what_do_you_want?".L(),
					CheckBoxText = "Remember_my_choice_and_dont_ask_again".L(),
					IsChecked = false,
					Image = MessageBoxImage.Question
				};
				var result = MessageBox.Show(msi);
				if (msi.IsChecked) {
					ConfigHelper.Save("LastTabClosed", result == MessageBoxResult.OK ? 1 : 2);
				}
				if (result == MessageBoxResult.OK) {
					Application.Current.Shutdown();
				} else {
					await SelectedTab.LoadDirectoryAsync(null);
				}
				break;
			}
		} else {
			if (tabViewSelectedIndex == 0) {
				TabViewSelectedIndex++;
			} else {
				TabViewSelectedIndex--;
			}
		}
		GC.Collect();
	}

	private async void OnNewTab(object args) {
		await OpenPathInNewTabAsync(null);
	}
}