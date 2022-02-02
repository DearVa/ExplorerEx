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
using System.Windows.Threading;
using ExplorerEx.Model;
using ExplorerEx.Selector;
using ExplorerEx.Utils;
using ExplorerEx.View;
using ExplorerEx.Win32;
using HandyControl.Data;
using Microsoft.VisualBasic.FileIO;
using hc = HandyControl.Controls;

namespace ExplorerEx.ViewModel;

/// <summary>
/// 对应一个Tab
/// </summary>
internal class FileViewTabViewModel : ViewModelBase, IDisposable {
	public MainWindowViewModel OwnerViewModel { get; }

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

	public SimpleCommand SelectionChangedCommand { get; }

	public bool CanGoBack => nextHistoryIndex > 1;

	public SimpleCommand GoBackCommand { get; }

	public bool CanGoForward => nextHistoryIndex < historyCount;

	public SimpleCommand GoForwardCommand { get; }

	public bool CanGoToUpperLevel => Type != FileViewDataTemplateSelector.Type.Home;

	public SimpleCommand GoToUpperLevelCommand { get; }

	public bool IsItemSelected => SelectedItems.Count > 0;

	public bool CanPaste => Type != FileViewDataTemplateSelector.Type.Home && canPaste;

	private bool canPaste;

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

	private readonly FileSystemWatcher watcher = new();

	private readonly Dispatcher dispatcher;

	private CancellationTokenSource cts;
	
	public FileViewTabViewModel(MainWindowViewModel ownerViewModel, string path) {
		OwnerViewModel = ownerViewModel;
		GoBackCommand = new SimpleCommand(_ => GoBackAsync());
		GoForwardCommand = new SimpleCommand(_ => GoForwardAsync());
		GoToUpperLevelCommand = new SimpleCommand(_ => GoToUpperLevelAsync());
		SelectionChangedCommand = new SimpleCommand(e => OnSelectionChanged(null, (SelectionChangedEventArgs)e));

		dispatcher = Application.Current.Dispatcher;

		watcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.DirectoryName |
							   NotifyFilters.FileName | NotifyFilters.LastAccess | NotifyFilters.LastWrite |
							   NotifyFilters.Security | NotifyFilters.Size;

		watcher.Changed += Watcher_OnChanged;
		watcher.Created += Watcher_OnCreated;
		watcher.Deleted += Watcher_OnDeleted;
		watcher.Renamed += Watcher_OnRenamed;
		watcher.Error += Watcher_OnError;

		MainWindow.ClipboardChanged += OnClipboardChanged;

		LoadDirectoryAsync(path);
	}

	private void OnClipboardChanged() {
		canPaste = MainWindow.ClipboardContent.Type != ClipboardDataType.Unknown;
		OnPropertyChanged(nameof(CanPaste));
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
	public async Task LoadDirectoryAsync(string path, bool recordHistory = true, string selectedPath = null) {
		var isLoadRoot = string.IsNullOrWhiteSpace(path);  // 加载“此电脑”

		if (!isLoadRoot && !Directory.Exists(path)) {
			hc.MessageBox.Error("Check your input and try again.", "Cannot open path");
			return;
		}

		cts?.Cancel();
		cts = new CancellationTokenSource();

#if DEBUG
		var sw = Stopwatch.StartNew();
#endif

		Items.Clear();
		Type = isLoadRoot ? FileViewDataTemplateSelector.Type.Home : FileViewDataTemplateSelector.Type.Detail;

		if (Type != oldType && FileViewContentPresenter != null) {
			var selector = FileViewContentPresenter.ContentTemplateSelector;
			FileViewContentPresenter.ContentTemplateSelector = null;
			FileViewContentPresenter.ContentTemplateSelector = selector;
		}
		oldType = Type;

		var list = new List<FileViewBaseItem>();
		if (isLoadRoot) {
			watcher.EnableRaisingEvents = false;
			FullPath = "This_computer".L();
			OnPropertyChanged(nameof(Type));
			OnPropertyChanged(nameof(FullPath));
			OnPropertyChanged(nameof(Header));

			await Task.Run(() => {
				list.AddRange(DriveInfo.GetDrives().Select(drive => new DiskDriveItem(this, drive)));
			}, cts.Token);

		} else {
			if (path.Length > 3) {
				FullPath = path.TrimEnd('\\');
			} else {
				FullPath = path;
			}
			watcher.Path = FullPath;
			watcher.EnableRaisingEvents = true;
			OnPropertyChanged(nameof(Type));
			OnPropertyChanged(nameof(FullPath));
			OnPropertyChanged(nameof(Header));

			await Task.Run(() => {
				foreach (var directory in Directory.EnumerateDirectories(path)) {
					var item = new FileSystemItem(this, new DirectoryInfo(directory));
					list.Add(item);
					if (directory == selectedPath) {
						item.IsSelected = true;
						SelectedItems.Add(item);
					}
				}
				list.AddRange(Directory.EnumerateFiles(path).Select(filePath => new FileSystemItem(this, new FileInfo(filePath))));
			}, cts.Token);
		}

#if DEBUG
		Trace.WriteLine($"Enumerate {path} costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif

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

#if DEBUG
		Trace.WriteLine($"Update UI costs: {sw.ElapsedMilliseconds}ms");
		sw.Stop();
#endif
	}

	/// <summary>
	/// 回到上一页
	/// </summary>
	public async void GoBackAsync() {
		if (CanGoBack) {
			nextHistoryIndex--;
			await LoadDirectoryAsync(historyPaths[nextHistoryIndex - 1], false, historyPaths[nextHistoryIndex]);
		}
	}

	/// <summary>
	/// 前进一页
	/// </summary>
	public async void GoForwardAsync() {
		if (CanGoForward) {
			nextHistoryIndex++;
			await LoadDirectoryAsync(historyPaths[nextHistoryIndex - 1], false);
		}
	}

	/// <summary>
	/// 向上一级
	/// </summary>
	/// <returns></returns>
	public async void GoToUpperLevelAsync() {
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
	/// 将文件复制到剪贴板
	/// </summary>
	/// <param name="isCut"></param>
	public void Copy(bool isCut) {
		if (Type == FileViewDataTemplateSelector.Type.Home || SelectedItems.Count == 0) {
			return;
		}
		var data = new DataObject(DataFormats.FileDrop, SelectedItems.Where(item => item is FileSystemItem).Select(item => ((FileSystemItem)item).FullPath).ToArray());
		data.SetData("IsCut", isCut);
		Clipboard.SetDataObject(data);
	}

	/// <summary>
	/// 粘贴剪贴板中的文件或者文本、图片
	/// </summary>
	public void Paste() {
		if (Type == FileViewDataTemplateSelector.Type.Home) {
			return;
		}
		if (Clipboard.GetDataObject() is DataObject data) {
			var files = data.GetData(DataFormats.FileDrop);
			if (files is string[] f) {
				bool isCut;
				if (data.GetData("IsCut") is bool i) {
					isCut = i;
				} else {
					isCut = false;
				}
				foreach (var path in f) {
					if (File.Exists(path)) {
						if (isCut) {
							File.Move(path, Path.Combine(FullPath, Path.GetFileName(path)));
						} else {
							File.Copy(path, Path.Combine(FullPath, Path.GetFileName(path)));
						}
					} else if (Directory.Exists(path)) {
						if (isCut) {
							Directory.Move(path, Path.Combine(FullPath, Path.GetFileName(path)));
						} else {
							//Directory.(path, Path.Combine(FullPath, Path.GetFileName(path)));
						}
					}
				}
			}
		}
	}

	public void Delete(bool recycle) {
		if (Type == FileViewDataTemplateSelector.Type.Home || SelectedItems.Count == 0) {
			return;
		}
		if (recycle) {
			if (hc.MessageBox.Show(new MessageBoxInfo {
				    CheckBoxText = "Remember my choice and don't ask again".L(),
				    Message = "Are you sure to recycle these files?".L(),
				    Image = MessageBoxImage.Question,
				    Button = MessageBoxButton.YesNo
			    }) != MessageBoxResult.Yes) {
				return;
			}
		} else {
			if (hc.MessageBox.Show(new MessageBoxInfo {
				    CheckBoxText = "Remember my choice and don't ask again".L(),
				    Message = "Are you sure to delete these files Permanently?".L(),
				    Image = MessageBoxImage.Question,
				    Button = MessageBoxButton.YesNo
			    }) != MessageBoxResult.Yes) {
				return;
			}
		}
		foreach (var item in SelectedItems) {
			if (item is FileSystemItem fsi) {
				if (fsi.IsDirectory) {
					FileSystem.DeleteDirectory(fsi.FullPath, UIOption.OnlyErrorDialogs, recycle ? RecycleOption.SendToRecycleBin : RecycleOption.DeletePermanently);
				} else {
					FileSystem.DeleteFile(fsi.FullPath, UIOption.OnlyErrorDialogs, recycle ? RecycleOption.SendToRecycleBin : RecycleOption.DeletePermanently);
				}
			}
		}
	}

	/// <summary>
	/// 和文件夹相关的UI，和是否选中文件无关
	/// </summary>
	private void UpdateFolderUI() {
		OnPropertyChanged(nameof(CanPaste));
		OnPropertyChanged(nameof(CanGoBack));
		OnPropertyChanged(nameof(CanGoForward));
		OnPropertyChanged(nameof(CanGoToUpperLevel));
		OnPropertyChanged(nameof(FileItemsCount));
	}

	/// <summary>
	/// 和文件相关的UI，选择更改时更新
	/// </summary>
	private void UpdateFileUI() {
		if (Type == FileViewDataTemplateSelector.Type.Home) {
			SelectedFileItemsSizeVisibility = Visibility.Collapsed;
		} else {
			var size = SelectedFilesSize;
			if (size == -1) {
				SelectedFileItemsSizeVisibility = Visibility.Collapsed;
			} else {
				SelectedFileItemsSizeString = FileUtils.FormatByteSize(size);
				SelectedFileItemsSizeVisibility = Visibility.Visible;
			}
		}
		OnPropertyChanged(nameof(IsItemSelected));
		OnPropertyChanged(nameof(SelectedFileItemsCountVisibility));
		OnPropertyChanged(nameof(SelectedFileItemsCount));
		OnPropertyChanged(nameof(SelectedFileItemsSizeVisibility));
		OnPropertyChanged(nameof(SelectedFileItemsSizeString));
	}

	private FileViewBaseItem lastClickItem;
	private DateTimeOffset lastClickTime;

	public async Task Item_OnMouseUp(FileViewBaseItem item) {
		if (item == lastClickItem && DateTimeOffset.Now <= lastClickTime.AddMilliseconds(500)) {
			switch (item) {
			// 双击事件
			case DiskDriveItem ddi:
				await LoadDirectoryAsync(ddi.Driver.Name);
				break;
			case FileSystemItem fsi:
				await fsi.Open();
				break;
			}
		}
		lastClickItem = item;
		lastClickTime = DateTimeOffset.Now;
	}

	private bool isClearingSelection;

	public void ClearSelection() {
		isClearingSelection = true;
		foreach (var item in SelectedItems) {
			item.IsSelected = false;
		}
		SelectedItems.Clear();
		UpdateFileUI();
		isClearingSelection = false;
	}

	public void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
		if (isClearingSelection) {
			return;
		}
		foreach (FileViewBaseItem addedItem in e.AddedItems) {
			SelectedItems.Add(addedItem);
			addedItem.IsSelected = true;
		}
		foreach (FileViewBaseItem removedItem in e.RemovedItems) {
			SelectedItems.Remove(removedItem);
			removedItem.IsSelected = false;
		}
		UpdateFileUI();
	}

	private void Watcher_OnError(object sender, ErrorEventArgs e) {
		if (Type == FileViewDataTemplateSelector.Type.Home) {
			return;
		}
		throw new NotImplementedException();
	}

	private void Watcher_OnRenamed(object sender, RenamedEventArgs e) {
		if (Type == FileViewDataTemplateSelector.Type.Home) {
			return;
		}
		dispatcher.Invoke(async () => {
			for (var i = 0; i < Items.Count; i++) {
				if (((FileSystemItem)Items[i]).FullPath == e.OldFullPath) {
					if (Directory.Exists(e.FullPath)) {
						Items[i] = new FileSystemItem(this, new DirectoryInfo(e.FullPath));
					} else if (File.Exists(e.FullPath)) {
						var item = new FileSystemItem(this, new FileInfo(e.FullPath));
						Items[i] = item;
						await item.LoadIconAsync();
					}
					return;
				}
			}
		});
	}

	private void Watcher_OnDeleted(object sender, FileSystemEventArgs e) {
		if (Type == FileViewDataTemplateSelector.Type.Home) {
			return;
		}
		dispatcher.Invoke(() => {
			for (var i = 0; i < Items.Count; i++) {
				if (((FileSystemItem)Items[i]).FullPath == e.FullPath) {
					Items.RemoveAt(i);
					return;
				}
			}
		});
	}

	private void Watcher_OnCreated(object sender, FileSystemEventArgs e) {
		if (Type == FileViewDataTemplateSelector.Type.Home) {
			return;
		}
		dispatcher.Invoke(async () => {
			FileSystemItem item;
			if (Directory.Exists(e.FullPath)) {
				item = new FileSystemItem(this, new DirectoryInfo(e.FullPath));
			} else if (File.Exists(e.FullPath)) {
				item = new FileSystemItem(this, new FileInfo(e.FullPath));
			} else {
				return;
			}
			Items.Add(item);
			if (!item.IsDirectory) {
				await item.LoadIconAsync();
			}
		});
	}

	private void Watcher_OnChanged(object sender, FileSystemEventArgs e) {
		if (Type == FileViewDataTemplateSelector.Type.Home) {
			return;
		}
		dispatcher.Invoke(async () => {
			// ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
			foreach (FileSystemItem item in Items) {
				if (item.FullPath == e.FullPath) {
					await item.RefreshAsync();
					return;
				}
			}
		});
	}

	public void Dispose() {
		MainWindow.ClipboardChanged -= OnClipboardChanged;
		watcher?.Dispose();
		cts?.Dispose();
	}
}