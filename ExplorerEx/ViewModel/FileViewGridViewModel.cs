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
using System.Windows.Input;
using System.Windows.Threading;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using ExplorerEx.View;
using ExplorerEx.View.Controls;
using ExplorerEx.Win32;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using static ExplorerEx.View.Controls.FileDataGrid;
using hc = HandyControl.Controls;

namespace ExplorerEx.ViewModel;

/// <summary>
/// 对应一个Tab
/// </summary>
public class FileViewGridViewModel : SimpleNotifyPropertyChanged, IDisposable {
	public FileTabControl OwnerTabControl { get; }

	public MainWindow OwnerWindow => OwnerTabControl.MainWindow;

	/// <summary>
	/// 当前的路径，如果是首页，就是“此电脑”
	/// </summary>
	public string FullPath { get; private set; }

	public string Header => PathType == PathTypes.Home ? FullPath : FullPath.Length <= 3 ? FullPath : Path.GetFileName(FullPath);

	public PathTypes PathType { get; private set; } = PathTypes.Home;

	public ViewTypes ViewType { get; private set; } = ViewTypes.Tile;

	public int ViewTypeIndex => ViewType switch {
		ViewTypes.Icon when ItemSize.Width > 100d && ItemSize.Height > 130d => 0,
		ViewTypes.Icon => 1,
		ViewTypes.List => 2,
		ViewTypes.Detail => 3,
		ViewTypes.Tile => 4,
		ViewTypes.Content => 5,
		_ => -1
	};

	public Lists DetailLists { get; private set; } = Lists.Type | Lists.AvailableSpace | Lists.TotalSpace | Lists.FillRatio | Lists.FileSystem;

	/// <summary>
	/// 当前文件夹内的文件列表
	/// </summary>
	public ObservableCollection<FileViewBaseItem> Items { get; } = new();

	public ObservableHashSet<FileViewBaseItem> SelectedItems { get; } = new();

	public SimpleCommand SelectionChangedCommand { get; }

	public bool CanGoBack => nextHistoryIndex > 1;

	public SimpleCommand GoBackCommand { get; }

	public bool CanGoForward => nextHistoryIndex < historyCount;

	public SimpleCommand GoForwardCommand { get; }

	public bool CanGoToUpperLevel => PathType != PathTypes.Home;

	public SimpleCommand GoToUpperLevelCommand { get; }

	public bool IsItemSelected => SelectedItems.Count > 0;

	public SimpleCommand CutCommand { get; }

	public SimpleCommand CopyCommand { get; }

	public SimpleCommand PasteCommand { get; }

	public SimpleCommand RenameCommand { get; }

	// public SimpleCommand ShareCommand { get; }

	public SimpleCommand DeleteCommand { get; }

	/// <summary>
	/// 改变文件视图模式
	/// </summary>
	public SimpleCommand SwitchViewCommand { get; }

	/// <summary>
	/// 文件项的大小
	/// </summary>
	public Size ItemSize { get; private set; } = new(0d, 70d);

	public SimpleCommand FileDropCommand { get; }

	public SimpleCommand ItemDoubleClickedCommand { get; }

	public bool CanPaste { get; private set; }

	public int FileItemsCount => Items.Count;

	public Visibility SelectedFileItemsCountVisibility => SelectedItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

	public int SelectedFileItemsCount => SelectedItems.Count;

	public Visibility SelectedFileItemsSizeVisibility { get; private set; }

	public string SelectedFileItemsSizeText { get; private set; }

	public long SelectedFilesSize {
		get {
			if (SelectedItems.Count == 0) {
				return -1L;
			}
			var size = 0L;
			foreach (var item in SelectedItems) {
				if (item.IsFolder) {
					return -1L;
				}
				size += item.FileSize;
			}
			return size;
		}
	}

	public string SearchPlaceholderText => $"{"Search".L()} {Header}";

	/// <summary>
	/// 如果不为null，就说明用户输入了搜索，OneWayToSource
	/// </summary>
	public string SearchText {
		set {
			if (string.IsNullOrWhiteSpace(value)) {
				if (searchText != null) {
					searchText = null;
					UpdateSearch(); // 这样如果用户输入了很多空格就不用更新了
				}
			} else {
				searchText = value;
				UpdateSearch();
			}
		}
	}

	private string searchText;

	private readonly List<string> historyPaths = new(128);

	private int nextHistoryIndex, historyCount;

	private readonly FileSystemWatcher watcher = new();

	private readonly Dispatcher dispatcher;

	private CancellationTokenSource cts;

	public FileViewGridViewModel(FileTabControl ownerTabControl) {
		OwnerTabControl = ownerTabControl;
		OwnerWindow.EverythingQueryReplied += OnEverythingQueryReplied;

		GoBackCommand = new SimpleCommand(_ => GoBackAsync());
		GoForwardCommand = new SimpleCommand(_ => GoForwardAsync());
		GoToUpperLevelCommand = new SimpleCommand(_ => GoToUpperLevelAsync());
		SelectionChangedCommand = new SimpleCommand(e => OnSelectionChanged((SelectionChangedEventArgs)e));

		CutCommand = new SimpleCommand(_ => Copy(true));
		CopyCommand = new SimpleCommand(_ => Copy(false));
		PasteCommand = new SimpleCommand(_ => Paste());
		RenameCommand = new SimpleCommand(_ => Rename());
		//ShareCommand = new SimpleCommand(_ => Copy(false));
		DeleteCommand = new SimpleCommand(_ => Delete(true));

		SwitchViewCommand = new SimpleCommand(OnSwitchView);

		FileDropCommand = new SimpleCommand(e => OnDrop((FileDropEventArgs)e));
		// ReSharper disable once AsyncVoidLambda
		ItemDoubleClickedCommand = new SimpleCommand(async e => await Item_OnDoubleClicked((ItemClickEventArgs)e));

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
	}

	private void OnSwitchView(object e) {
		if (e is MenuItem menuItem) {
			SwitchViewType(int.Parse((string)menuItem.CommandParameter));
		}
	}

	private bool isLastViewTypeUseLargeIcon;
	/// <summary>
	/// 切换视图时，有的要使用大图标，有的要使用小图标，所以要运行一个Task去更改，取消这个来中断Task
	/// </summary>
	private CancellationTokenSource switchIconCts;

	private async void SwitchViewType(int type) {
		switch (type) {
		case 0:  // 大图标
			ViewType = ViewTypes.Icon;
			ItemSize = new Size(120d, 170d);
			break;
		case 1:  // 小图标
			ViewType = ViewTypes.Icon;
			ItemSize = new Size(80d, 170d);
			break;
		case 2:  // 列表，size.Width为0代表横向填充
			ViewType = ViewTypes.List;
			ItemSize = new Size(0d, 30d);
			break;
		case 3:  // 详细信息
			ViewType = ViewTypes.Detail;
			ItemSize = new Size(0d, 30d);
			break;
		case 4:  // 平铺
			ViewType = ViewTypes.Tile;
			ItemSize = new Size(0d, 70d);
			break;
		case 5:  // 内容
			ViewType = ViewTypes.Content;
			ItemSize = new Size(0d, 70d);
			break;
		}
		PropertyUpdateUI(nameof(ViewType));
		PropertyUpdateUI(nameof(ItemSize));
		PropertyUpdateUI(nameof(ViewTypeIndex));
		if (PathType == PathTypes.Normal) {
			var useLargeIcon = type is 0 or 1 or 4 or 5;
			if (useLargeIcon != isLastViewTypeUseLargeIcon) {
				switchIconCts?.Cancel();
				var list = Items.Where(item => item is FileSystemItem && !item.IsFolder).Cast<FileSystemItem>().Where(item => item.UseLargeIcon != useLargeIcon).ToArray();
				var cts = switchIconCts = new CancellationTokenSource();
				foreach (var item in list) {
					if (cts.IsCancellationRequested) {
						return;
					}
					item.UseLargeIcon = useLargeIcon;
					await item.LoadIconAsync();
				}
				isLastViewTypeUseLargeIcon = useLargeIcon;
			}
		}
	}

	private void OnClipboardChanged() {
		CanPaste = MainWindow.DataObjectContent.Type != DataObjectType.Unknown;
		PropertyUpdateUI(nameof(CanPaste));
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

	/// <summary>
	/// 加载一个文件夹路径
	/// </summary>
	/// <param name="path">如果为null或者WhiteSpace，就加载“此电脑”</param>
	/// <param name="recordHistory"></param>
	/// <param name="selectedPath"></param>
	/// <returns></returns>
	public async Task<bool> LoadDirectoryAsync(string path, bool recordHistory = true, string selectedPath = null) {
		switchIconCts?.Cancel();
		var isLoadRoot = string.IsNullOrWhiteSpace(path) || path == "This_computer".L();  // 加载“此电脑”

		if (!isLoadRoot && !Directory.Exists(path)) {
			hc.MessageBox.Error("Check your input and try again.", "Cannot open path");
			return false;
		}

		cts?.Cancel();
		cts = new CancellationTokenSource();

#if DEBUG
		var sw = Stopwatch.StartNew();
#endif

		var list = new List<FileViewBaseItem>();
		if (isLoadRoot) {
			watcher.EnableRaisingEvents = false;
			FullPath = "This_computer".L();
			PropertyUpdateUI(nameof(FullPath));

			await Task.Run(() => {
				list.AddRange(DriveInfo.GetDrives().Select(drive => new DiskDriveItem(drive)));
			}, cts.Token);

		} else {
			if (path.Length > 3) {
				FullPath = path.TrimEnd('\\');
			} else {
				FullPath = path;
			}
			PropertyUpdateUI(nameof(FullPath));

			try {
				watcher.Path = FullPath;
				watcher.EnableRaisingEvents = true;
			} catch {
				hc.MessageBox.Error("Access_denied".L(), "Cannot_access_directory".L());
				if (nextHistoryIndex > 0) {
					await LoadDirectoryAsync(historyPaths[nextHistoryIndex - 1], false, path);
				}
				return false;
			}
			await Task.Run(() => {
				foreach (var directory in Directory.EnumerateDirectories(path)) {
					var item = new FileSystemItem(new DirectoryInfo(directory));
					list.Add(item);
					if (directory == selectedPath) {
						item.IsSelected = true;
						SelectedItems.Add(item);
					}
				}
				list.AddRange(Directory.EnumerateFiles(path).Select(filePath => new FileSystemItem(new FileInfo(filePath))));
			}, cts.Token);
		}

#if DEBUG
		Trace.WriteLine($"Enumerate {path} costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif

		Items.Clear();
		PathType = isLoadRoot ? PathTypes.Home : PathTypes.Normal;  // TODO: 网络驱动器、OneDrive等
																	// 一旦调用这个，模板就会改变，所以要在清空之后，不然会导致排版混乱和绑定失败
		PropertyUpdateUI(nameof(PathType));
		if (PathType == PathTypes.Home) {  // TODO
			DetailLists = Lists.Type | Lists.AvailableSpace | Lists.TotalSpace | Lists.FillRatio | Lists.FileSystem;
		} else {
			DetailLists = Lists.ModificationDate | Lists.Type | Lists.FileSize;
		}
		PropertyUpdateUI(nameof(DetailLists));
		PropertyUpdateUI(nameof(Header));
		SwitchViewType(isLoadRoot ? 4 : 3);  // TODO: 此处暂时设为默认值，要根据文件夹记录在数据库里

		foreach (var fileViewBaseItem in list) {
			Items.Add(fileViewBaseItem);
		}

		if (recordHistory) {
			AddHistory(path);
		}
		UpdateFolderUI();
		UpdateFileUI();

#if DEBUG
		Trace.WriteLine($"Update UI costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif

		foreach (var fileViewBaseItem in list.Where(item => item is DiskDriveItem || !item.IsFolder)) {
			await fileViewBaseItem.LoadIconAsync();
		}

#if DEBUG
		Trace.WriteLine($"Async load Icon costs: {sw.ElapsedMilliseconds}ms");
		sw.Stop();
#endif
		return true;
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
		if (PathType == PathTypes.Home || SelectedItems.Count == 0) {
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
		if (PathType == PathTypes.Home) {
			return;
		}
		if (Clipboard.GetDataObject() is DataObject data) {
			if (data.GetData(DataFormats.FileDrop) is string[] filePaths) {
				bool isCut;
				if (data.GetData("IsCut") is bool i) {
					isCut = i;
				} else {
					isCut = false;
				}
				var destPaths = filePaths.Select(path => Path.Combine(FullPath, Path.GetFileName(path))).ToList();
				try {
					FileUtils.FileOperation(isCut ? Win32Interop.FileOpType.Move : Win32Interop.FileOpType.Copy, filePaths, destPaths);
				} catch (Exception e) {
					Logger.Exception(e);
				}
			}
		}
	}

	/// <summary>
	/// 重命名
	/// </summary>
	public void Rename() {
		if (SelectedItems.Count == 0) {
			return;
		}
		if (PathType == PathTypes.Home) {
			SelectedItems.First().BeginRename();
		} else if (SelectedItems.Count == 1) {
			SelectedItems.First().BeginRename();
		} else {
			// TODO: 批量重命名
		}
	}

	public void Delete(bool recycle) {
		if (PathType == PathTypes.Home || SelectedItems.Count == 0) {
			return;
		}
		if (recycle) {
			if (!MessageBoxHelper.AskWithDefault("Recycle", "Are_you_sure_to_recycle_these_files?".L())) {
				return;
			}
			try {
				FileUtils.FileOperation(Win32Interop.FileOpType.Delete, SelectedItems.Where(item => item is FileSystemItem)
					.Cast<FileSystemItem>().Select(item => item.FullPath).ToArray());
			} catch (Exception e) {
				Logger.Exception(e);
			}
		} else {
			if (!MessageBoxHelper.AskWithDefault("Delete", "Are_you_sure_to_delete_these_files_Permanently?".L())) {
				return;
			}
			var failedFiles = new List<string>();
			foreach (var item in SelectedItems) {
				if (item is FileSystemItem fsi) {
					try {
						if (fsi.IsFolder) {
							Directory.Delete(fsi.FullPath, true);
						} else {
							File.Delete(fsi.FullPath);
						}
					} catch (Exception e) {
						Logger.Exception(e, false);
						failedFiles.Add(fsi.FullPath);
					}
				}
			}
			if (failedFiles.Count > 0) {
				hc.MessageBox.Error(string.Join('\n', failedFiles), "The_following_files_failed_to_delete".L());
			}
		}
	}

	/// <summary>
	/// 和文件夹相关的UI，和是否选中文件无关
	/// </summary>
	private void UpdateFolderUI() {
		PropertyUpdateUI(nameof(CanPaste));
		PropertyUpdateUI(nameof(CanGoBack));
		PropertyUpdateUI(nameof(CanGoForward));
		PropertyUpdateUI(nameof(CanGoToUpperLevel));
		PropertyUpdateUI(nameof(FileItemsCount));
		PropertyUpdateUI(nameof(SearchPlaceholderText));
	}

	/// <summary>
	/// 和文件相关的UI，选择更改时更新
	/// </summary>
	private void UpdateFileUI() {
		if (PathType == PathTypes.Home) {
			SelectedFileItemsSizeVisibility = Visibility.Collapsed;
		} else {
			var size = SelectedFilesSize;
			if (size == -1) {
				SelectedFileItemsSizeVisibility = Visibility.Collapsed;
			} else {
				SelectedFileItemsSizeText = FileUtils.FormatByteSize(size);
				SelectedFileItemsSizeVisibility = Visibility.Visible;
			}
		}
		PropertyUpdateUI(nameof(IsItemSelected));
		PropertyUpdateUI(nameof(SelectedFileItemsCountVisibility));
		PropertyUpdateUI(nameof(SelectedFileItemsCount));
		PropertyUpdateUI(nameof(SelectedFileItemsSizeVisibility));
		PropertyUpdateUI(nameof(SelectedFileItemsSizeText));
	}

	public async Task Item_OnDoubleClicked(ItemClickEventArgs e) {
		var isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
		var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
		var isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
		switch (e.Item) {
		// 双击事件
		case DiskDriveItem ddi:
			if (isCtrlPressed) {
				await OwnerTabControl.OpenPathInNewTabAsync(ddi.Driver.Name);
			} else if (isShiftPressed) {
				new MainWindow(ddi.Driver.Name).Show();
			} else if (isAltPressed) {
				Win32Interop.ShowFileProperties(ddi.Driver.Name);
			} else {
				await LoadDirectoryAsync(ddi.Driver.Name);
			}
			break;
		case FileSystemItem fsi:
			if (isAltPressed) {
				Win32Interop.ShowFileProperties(fsi.FullPath);
			} else {
				if (fsi.IsFolder) {
					if (isCtrlPressed) {
						await OwnerTabControl.OpenPathInNewTabAsync(fsi.FullPath);
					} else if (isShiftPressed) {
						new MainWindow(fsi.FullPath).Show();
					} else {
						await LoadDirectoryAsync(fsi.FullPath);
					}
				} else {
					await fsi.OpenAsync(isCtrlPressed || isShiftPressed);
				}
			}
			break;
		}
	}

	private void OnSelectionChanged(SelectionChangedEventArgs e) {
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


	private void OnDrop(FileDropEventArgs e) {
		var path = e.Path ?? FullPath;
		FileUtils.HandleDrop(e.Content, path, e.DragEventArgs.Effects.GetFirstEffect());
	}

	private uint everythingQueryId;

	/// <summary>
	/// 当用户更改SearchTextBox时触发
	/// </summary>
	private async void UpdateSearch() {
		everythingReplyCts?.Cancel();
		if (searchText == null) {
			await LoadDirectoryAsync(FullPath);
		} else {
			if (EverythingInterop.IsAvailable) {
				Items.Clear();  // 清空文件列表，进入搜索模式

				// EverythingInterop.Reset();
				EverythingInterop.Search = searchText;
				EverythingInterop.Max = 999;
				// EverythingInterop.SetRequestFlags(EverythingInterop.RequestType.FullPathAndFileName | EverythingInterop.RequestType.DateModified | EverythingInterop.RequestType.Size);
				// EverythingInterop.SetSort(EverythingInterop.SortMode.NameAscending);
				var mainWindow = OwnerTabControl.MainWindow;
				mainWindow.UnRegisterEverythingQuery(everythingQueryId);
				everythingQueryId = mainWindow.RegisterEverythingQuery();
				EverythingInterop.Query(false);
			} else {
				hc.MessageBox.Error("Everything_is_not_available".L());
			}
		}
	}

	private CancellationTokenSource everythingReplyCts;

	private async void OnEverythingQueryReplied(uint id, EverythingInterop.QueryReply reply) {
		if (id != everythingQueryId) {
			return;
		}
		PathType = PathTypes.Home;
		everythingReplyCts?.Cancel();
		everythingReplyCts = new CancellationTokenSource();
		List<FileSystemItem> list = null;
		await Task.Run(() => {
			list = new List<FileSystemItem>(reply.FullPaths.Length);
			foreach (var fullPath in reply.FullPaths) {
				try {
					if (Directory.Exists(fullPath)) {
						list.Add(new FileSystemItem(new DirectoryInfo(fullPath)));
					} else if (File.Exists(fullPath)) {
						list.Add(new FileSystemItem(new FileInfo(fullPath)));
					}
				} catch (Exception e) {
					Logger.Exception(e, false);
					break;
				}
			}
		}, everythingReplyCts.Token);

		Items.Clear();

		foreach (var item in list) {
			Items.Add(item);
		}

		UpdateFolderUI();
		UpdateFileUI();

		foreach (var fileViewBaseItem in list.Where(item => !item.IsFolder)) {
			await fileViewBaseItem.LoadIconAsync();
		}
	}

	private void Watcher_OnError(object sender, ErrorEventArgs e) {
		if (PathType == PathTypes.Home) {
			return;
		}
		throw new NotImplementedException();
	}

	private void Watcher_OnRenamed(object sender, RenamedEventArgs e) {
		if (PathType == PathTypes.Home) {
			return;
		}
		dispatcher.Invoke(async () => {
			for (var i = 0; i < Items.Count; i++) {
				if (((FileSystemItem)Items[i]).FullPath == e.OldFullPath) {
					if (Directory.Exists(e.FullPath)) {
						Items[i] = new FileSystemItem(new DirectoryInfo(e.FullPath));
					} else if (File.Exists(e.FullPath)) {
						var item = new FileSystemItem(new FileInfo(e.FullPath));
						Items[i] = item;
						await item.LoadIconAsync();
					}
					return;
				}
			}
		});
	}

	private void Watcher_OnDeleted(object sender, FileSystemEventArgs e) {
		if (PathType == PathTypes.Home) {
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
		if (PathType == PathTypes.Home) {
			return;
		}
		dispatcher.Invoke(async () => {
			FileSystemItem item;
			if (Directory.Exists(e.FullPath)) {
				item = new FileSystemItem(new DirectoryInfo(e.FullPath));
			} else if (File.Exists(e.FullPath)) {
				item = new FileSystemItem(new FileInfo(e.FullPath));
			} else {
				return;
			}
			Items.Add(item);
			if (!item.IsFolder) {
				await item.LoadIconAsync();
			}
		});
	}

	private void Watcher_OnChanged(object sender, FileSystemEventArgs e) {
		if (PathType == PathTypes.Home) {
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
		Items.Clear();
		watcher?.Dispose();
		cts?.Dispose();
		everythingReplyCts?.Dispose();
	}
}