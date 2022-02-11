using ExplorerEx.Utils;
using ExplorerEx.ViewModel;
using HandyControl.Data;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System;
using System.Windows.Input;
using HandyControl.Controls;
using MessageBox = HandyControl.Controls.MessageBox;
using TextBox = System.Windows.Controls.TextBox;

namespace ExplorerEx.View.Controls;

public partial class FileTabControl {
	/// <summary>
	/// 标签页
	/// </summary>
	public ObservableCollection<FileViewTabViewModel> TabItems { get; } = new();

	public new static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
		"SelectedIndex", typeof(int), typeof(FileTabControl), new PropertyMetadata(default(int)));

	public new int SelectedIndex {
		get => (int)GetValue(SelectedIndexProperty);
		set {
			if (TabItems.Count == 0) {
				return;
			}
			if (value < 0) {
				value = 0;
			} else if (value >= TabItems.Count) {
				value = TabItems.Count - 1;
			}
			SetValue(SelectedIndexProperty, value);
		}
	}

	public FileViewTabViewModel SelectedTab => TabItems[SelectedIndex];

	public static readonly DependencyProperty IsFileUtilsVisibleProperty = DependencyProperty.Register(
		"IsFileUtilsVisible", typeof(bool), typeof(FileTabControl), new PropertyMetadata(default(bool)));

	public bool IsFileUtilsVisible {
		get => (bool)GetValue(IsFileUtilsVisibleProperty);
		set => SetValue(IsFileUtilsVisibleProperty, value);
	}

	public SimpleCommand TabClosingCommand { get; }

	public SimpleCommand TabMovedCommand { get; }

	public SimpleCommand CreateNewTabCommand { get; }

	public MainWindow MainWindow { get; }

	public SplitGrid OwnerSplitGrid { get; set; }

	public FileTabControl(MainWindow mainWindow, SplitGrid ownerSplitGrid, FileViewTabViewModel tab) {
		MainWindow = mainWindow;
		OwnerSplitGrid = ownerSplitGrid;
		DataContext = this;
		TabClosingCommand = new SimpleCommand(OnTabClosing);
		TabMovedCommand = new SimpleCommand(OnTabMoved);
		CreateNewTabCommand = new SimpleCommand(OnCreateNewTab);
		InitializeComponent();

		TabItems.Add(tab ?? new FileViewTabViewModel(this));
	}

	public async Task StartUpLoad(string path) {
		if (!await SelectedTab.LoadDirectoryAsync(path)) {
			MainWindow.Close();
		}
	}

	public void CloseAllTabs() {
		foreach (var tab in TabItems) {
			tab.Dispose();
		}
	}

	public async Task OpenPathInNewTabAsync(string path) {
		var newTabIndex = SelectedIndex + 1;
		var item = new FileViewTabViewModel(this);
		TabItems.Insert(newTabIndex, item);
		SelectedIndex = newTabIndex;
		if (!await SelectedTab.LoadDirectoryAsync(path)) {
			if (TabItems.Count > 1) {
				TabItems.Remove(item);
			} else {
				MainWindow.Close();
			}
		}
	}

	protected override void OnPreviewMouseUp(MouseButtonEventArgs e) {
		switch (e.ChangedButton) {
		case MouseButton.XButton1:  // 鼠标侧键返回
			SelectedTab.GoBackAsync();
			break;
		case MouseButton.XButton2:
			SelectedTab.GoForwardAsync();
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
				SelectedTab.Copy(true);
				break;
			case Key.C:
				SelectedTab.Copy(false);
				break;
			case Key.V:
				SelectedTab.Paste();
				break;
			}
		} else {
			switch (e.Key) {
			case Key.Delete:
				SelectedTab.Delete((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift);
				break;
			default:
				base.OnPreviewKeyDown(e);
				return;
			}
		}
		e.Handled = true;
	}

	private void OnTabMoved(object args) {
		if (TabItems.Count == 0) {
			if (OwnerSplitGrid.AnyOtherTabs) {
				OwnerSplitGrid.CancelSplit();
			} else {  // 说明就剩这一个Tab了
				MainWindow.Close();
			}
		} else {
			if (SelectedIndex == 0) {
				SelectedIndex++;
			} else {
				SelectedIndex--;
			}
		}
	}

	private async void OnTabClosing(object args) {
		var e = (CancelRoutedEventArgs)args;
		if (TabItems.Count <= 1) {
			if (OwnerSplitGrid.AnyOtherTabs) {
				OwnerSplitGrid.CancelSplit();
			} else {  // 说明就剩这一个Tab了
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
			}
		} else {
			SelectedTab.Dispose();
			if (SelectedIndex == 0) {
				SelectedIndex++;
			} else {
				SelectedIndex--;
			}
		}
		GC.Collect();
	}

	private async void OnCreateNewTab(object args) {
		await OpenPathInNewTabAsync(null);
	}

	private async void AddressBar_OnKeyDown(object sender, KeyEventArgs e) {
		switch (e.Key) {
		case Key.Enter:
			await SelectedTab.LoadDirectoryAsync(((TextBox)sender).Text);
			break;
		}
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
		base.OnRenderSizeChanged(sizeInfo);
		if (sizeInfo.WidthChanged) {
			IsFileUtilsVisible = sizeInfo.NewSize.Width > 700d;
		}
	}
}