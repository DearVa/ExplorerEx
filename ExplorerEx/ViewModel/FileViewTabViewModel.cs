using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExplorerEx.Model;
using ExplorerEx.Selector;
using ExplorerEx.Utils;
using hc = HandyControl.Controls;

namespace ExplorerEx.ViewModel;

/// <summary>
/// 对应一个Tab
/// </summary>
internal class FileViewTabViewModel : ViewModelBase {
	public ContentPresenter FileViewContentPresenter { get; set; }

	public FileViewDataTemplateSelector.Type Type { get; private set; }

	public string FullPath { get; private set; }

	public string Header => Type == FileViewDataTemplateSelector.Type.Home ? FullPath : FullPath.Length <= 3 ? FullPath : Path.GetFileName(FullPath);

	/// <summary>
	/// 当前文件夹内的文件列表
	/// </summary>
	public ObservableCollection<FileViewBaseItem> Items { get; } = new();

	public ObservableCollection<CreateFileItem> CreateFileItems => CreateFileItem.Items;

	public ObservableHashSet<FileViewBaseItem> SelectedItems { get; } = new();

	public bool CanGoBack => nextHistoryIndex > 1;

	public bool CanGoForward => nextHistoryIndex < historyCount;

	public bool CanGoToUpperLevel => Type != FileViewDataTemplateSelector.Type.Home;

	public bool IsItemSelected => SelectedItems.Count > 0;

	public int FileItemsCount => Items.Count;

	public Visibility SelectedFileItemsCountVisibility => SelectedItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

	public int SelectedFileItemsCount => SelectedItems.Count;

	public Visibility SelectedFileItemsSizeVisibility { get; private set; }

	public string SelectedFileItemsSizeString { get; private set; }

	public long SelectedFilesSize {
		get {
			if (SelectedItems.Count == 0) {
				return -1L;
			}
			var size = 0L;
			foreach (var item in SelectedItems) {
				if (item.IsDirectory) {
					return -1L;
				}
				size += item.FileSize;
			}
			return size;
		}
	}

	private readonly List<string> historyPaths = new(128);

	private int nextHistoryIndex, historyCount;

	private CancellationTokenSource cts;

	public FileViewTabViewModel() : this(null) { }

	public FileViewTabViewModel(string path) {
		LoadDirectoryAsync(path);
	}

	private void AddHistory(string path) {
		if (historyPaths.Count == nextHistoryIndex) {
			historyPaths.Add(path);
		} else if (historyPaths.Count > nextHistoryIndex) {
			historyPaths[nextHistoryIndex] = path;
		} else {
			throw new ApplicationException("Error: History Record");
		}
		nextHistoryIndex++;
		historyCount = nextHistoryIndex;
	}

	private FileViewDataTemplateSelector.Type oldType;

	/// <summary>
	/// 加载一个文件夹路径
	/// </summary>
	/// <param name="path">如果为null或者WhiteSpace，就加载“此电脑”</param>
	/// <param name="recordHistory"></param>
	/// <returns></returns>
	public async Task LoadDirectoryAsync(string path, bool recordHistory = true) {
		var isLoadRoot = string.IsNullOrWhiteSpace(path);  // 加载“此电脑”

		if (!isLoadRoot && !Directory.Exists(path)) {
			hc.MessageBox.Error("Check your input and try again.", "Cannot open path");
			return;
		}

		cts?.Cancel();
		cts = new CancellationTokenSource();

		var sw = Stopwatch.StartNew();

		Items.Clear();
		Type = isLoadRoot ? FileViewDataTemplateSelector.Type.Home : FileViewDataTemplateSelector.Type.Detail;

		if (Type != oldType) {
			var selector = FileViewContentPresenter.ContentTemplateSelector;
			FileViewContentPresenter.ContentTemplateSelector = null;
			FileViewContentPresenter.ContentTemplateSelector = selector;
			oldType = Type;
		}

		var list = new List<FileViewBaseItem>();
		if (isLoadRoot) {
			FullPath = "This_computer".L();
			OnPropertyChanged(nameof(Type));
			OnPropertyChanged(nameof(FullPath));
			OnPropertyChanged(nameof(Header));

			await Task.Run(() => {
				list.AddRange(DriveInfo.GetDrives().Select(drive => new DiskDriveItem(drive)));
			}, cts.Token);

		} else {
			if (path.Length > 3) {
				FullPath = path.TrimEnd('\\');
			} else {
				FullPath = path;
			}
			OnPropertyChanged(nameof(Type));
			OnPropertyChanged(nameof(FullPath));
			OnPropertyChanged(nameof(Header));

			await Task.Run(() => {
				list.AddRange(Directory.EnumerateDirectories(path).Select(path => new FileSystemItem(new DirectoryInfo(path))));
				list.AddRange(Directory.EnumerateFiles(path).Select(filePath => new FileSystemItem(new FileInfo(filePath))));
			}, cts.Token);
		}

		Trace.WriteLine($"Enumerate {path} costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();

		foreach (var fileViewBaseItem in list) {
			Items.Add(fileViewBaseItem);
		}

		if (recordHistory) {
			AddHistory(path);
		}
		UpdateFolderUI();
		UpdateFileUI();

		foreach (var fileViewBaseItem in list.Where(item => !item.IsDirectory)) {
			await fileViewBaseItem.LoadIconAsync();
		}

		Trace.WriteLine($"Update UI costs: {sw.ElapsedMilliseconds}ms");
		sw.Stop();
	}

	/// <summary>
	/// 回到上一页
	/// </summary>
	public async void GoBackAsync(object sender, RoutedEventArgs e) {
		if (CanGoBack) {
			nextHistoryIndex--;
			await LoadDirectoryAsync(historyPaths[nextHistoryIndex - 1], false);
		}
	}

	/// <summary>
	/// 前进一页
	/// </summary>
	public async void GoForwardAsync(object sender, RoutedEventArgs e) {
		if (CanGoForward) {
			await LoadDirectoryAsync(historyPaths[nextHistoryIndex], false);
			nextHistoryIndex++;
		}
	}

	/// <summary>
	/// 向上一级
	/// </summary>
	/// <returns></returns>
	public async void GoToUpperLevelAsync(object sender, RoutedEventArgs e) {
		if (CanGoToUpperLevel) {
			if (FullPath.Length == 3) {
				await LoadDirectoryAsync(null);
			} else {
				var lastIndexOfSlash = FullPath.LastIndexOf('\\');
				switch (lastIndexOfSlash) {
				case -1:
					await LoadDirectoryAsync(null);
					break;
				case 2: // 例如F:\，此时需要保留最后的\
					await LoadDirectoryAsync(FullPath[..3]);
					break;
				default:
					await LoadDirectoryAsync(FullPath[..lastIndexOfSlash]);
					break;
				}
			}
		}
	}

	/// <summary>
	/// 和文件夹相关的UI，和是否选中文件无关
	/// </summary>
	private void UpdateFolderUI() {
		OnPropertyChanged(nameof(CanGoBack));
		OnPropertyChanged(nameof(CanGoForward));
		OnPropertyChanged(nameof(CanGoToUpperLevel));
		OnPropertyChanged(nameof(FileItemsCount));
	}

	/// <summary>
	/// 和文件相关的UI，选择更改时更新
	/// </summary>
	private void UpdateFileUI() {
		var size = SelectedFilesSize;
		if (size == -1) {
			SelectedFileItemsSizeVisibility = Visibility.Collapsed;
		} else {
			SelectedFileItemsSizeString = FileUtils.FormatByteSize(size);
			SelectedFileItemsSizeVisibility = Visibility.Visible;
		}
		OnPropertyChanged(nameof(IsItemSelected));
		OnPropertyChanged(nameof(SelectedFileItemsCountVisibility));
		OnPropertyChanged(nameof(SelectedFileItemsCount));
		OnPropertyChanged(nameof(SelectedFileItemsSizeVisibility));
		OnPropertyChanged(nameof(SelectedFileItemsSizeString));
	}

	private FileViewBaseItem lastClickItem;
	private DateTimeOffset lastClickTime;

	//public async void DiskDriveItemClicked(object sender, ItemClickEventArgs e) {
	//	if (e.ClickedItem == lastClickItem && DateTimeOffset.Now <= lastClickTime.AddMilliseconds(500)) {
	//		// 双击事件
	//		await LoadDirectoryAsync(((DiskDriveItem)e.ClickedItem).Driver.Name);
	//	}
	//	lastClickItem = e.ClickedItem;
	//	lastClickTime = DateTimeOffset.Now;
	//}

	public async Task Item_OnMouseUp(FileViewBaseItem item) {
		if (item == lastClickItem && DateTimeOffset.Now <= lastClickTime.AddMilliseconds(500)) {
			switch (item) {
			// 双击事件
			case DiskDriveItem ddi:
				await LoadDirectoryAsync(ddi.Driver.Name);
				break;
			case FileSystemItem { IsDirectory: true } fsi:
				await LoadDirectoryAsync(fsi.FullPath);
				break;
			case FileSystemItem { IsDirectory: false } fsi:
				try {
					Process.Start(new ProcessStartInfo {
						FileName = fsi.FullPath,
						UseShellExecute = true
					});
				} catch (Exception e) {
					Debugger.Break();
				}
				break;
			}
		}
		lastClickItem = item;
		lastClickTime = DateTimeOffset.Now;
	}

	public void ClearSelection() {
		SelectedItems.Clear();
		UpdateFileUI();
	}

	public void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
		foreach (FileViewBaseItem addedItem in e.AddedItems) {
			SelectedItems.Add(addedItem);
		}
		foreach (FileViewBaseItem removedItem in e.RemovedItems) {
			SelectedItems.Remove(removedItem);
		}
		UpdateFileUI();
	}
}