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
using ExplorerEx.Command;
using ExplorerEx.Model;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.View;
using ExplorerEx.View.Controls;
using ExplorerEx.Win32;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using static ExplorerEx.View.Controls.FileListView;
using hc = HandyControl.Controls;

namespace ExplorerEx.ViewModel;

/// <summary>
/// 对应一个Tab
/// </summary>
public class FileTabViewModel : SimpleNotifyPropertyChanged, IDisposable {
	public FileTabControl OwnerTabControl { get; }

	public MainWindow OwnerWindow => OwnerTabControl.MainWindow;

	public FileListView FileListView { get; set; }

	/// <summary>
	/// 当前路径文件夹
	/// </summary>
	public FolderItem Folder { get; private set; } = HomeFolderItem.Instance;

	/// <summary>
	/// 当前的路径，如果是首页，就是null
	/// </summary>
	public string FullPath => Folder.FullPath;

	public FileView FileView { get; } = new();

	public PathType PathType {
		get => FileView.PathType;
		private set => FileView.PathType = value;
	}

	/// <summary>
	/// 排序依据
	/// </summary>
	public DetailListType SortBy {
		get => FileView.SortBy;
		private set => FileView.SortBy = value;
	}

	/// <summary>
	/// 升序排列
	/// </summary>
	public bool IsAscending {
		get => FileView.IsAscending;
		set => FileView.IsAscending = value;
	}

	/// <summary>
	/// 分组依据
	/// </summary>
	public DetailListType? GroupBy {
		get => FileView.GroupBy;
		private set => FileView.GroupBy = value;
	}

	public Size ItemSize {
		get => FileView.ItemSize;
		set => FileView.ItemSize = value;
	}

	public FileViewType FileViewType {
		get => FileView.FileViewType;
		private set => FileView.FileViewType = value;
	}

	public List<DetailList> DetailLists { get; private set; }

	/// <summary>
	/// 当前文件夹内的文件列表
	/// </summary>
	public ObservableCollection<FileListViewItem> Items { get; } = new();

	/// <summary>
	/// 当前文件夹是否在加载
	/// </summary>
	public bool IsLoading {
		get => isLoading;
		set {
			if (isLoading != value) {
				isLoading = value;
				UpdateUI();
			}
		}
	}

	private bool isLoading;

	public ObservableHashSet<FileListViewItem> SelectedItems { get; } = new();

	public SimpleCommand SelectionChangedCommand { get; }

	public bool CanGoBack => nextHistoryIndex > 1;

	public SimpleCommand GoBackCommand { get; }

	public string GoBackButtonToolTip => CanGoBack ? string.Format("GoBackTo...".L(), HistoryList[nextHistoryIndex - 2].DisplayText) : null;

	public bool CanGoForward => nextHistoryIndex < historyCount;

	public SimpleCommand GoForwardCommand { get; }

	public string GoForwardButtonToolTip => CanGoForward ? string.Format("GoForwardTo...".L(), HistoryList[nextHistoryIndex].DisplayText) : null;

	/// <summary>
	/// 历史记录
	/// </summary>
	public ObservableCollection<FolderItem> HistoryList { get; } = new();

	public bool CanGoToUpperLevel => PathType != PathType.Home;

	public SimpleCommand GoToUpperLevelCommand { get; }

	public string GoToUpperLevelButtonToolTip {
		get {
			if (!CanGoToUpperLevel) {
				return null;
			}
			return string.Format("GoUpTo...".L(), ParentFolderName);
		}
	}

	private string ParentFolderName {
		get {
			switch (Folder) {
			case HomeFolderItem:
				return null;
			case DiskDriveItem:
				return "ThisPC".L();
			default:
				var path = Path.GetDirectoryName(FullPath)!;
				if (path.Length <= 3) {
					return path;
				}
				return Path.GetFileName(path);
			}
		}
	}

	public bool IsItemSelected => SelectedItems.Count > 0;

	public bool CanDeleteOrCut => IsItemSelected && SelectedItems.All(i => i is FileSystemItem);

	public FileItemCommand FileItemCommand { get; }

	/// <summary>
	/// 改变文件视图模式
	/// </summary>
	public SimpleCommand SwitchViewCommand { get; }

	public SimpleCommand FileDropCommand { get; }

	public SimpleCommand ItemDoubleClickedCommand { get; }

	public bool CanPaste {
		get => canPaste && Folder is not HomeFolderItem;
		private set {
			if (canPaste != value) {
				canPaste = value;
				UpdateUI();
			}
		}
	}

	private bool canPaste;

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

	public string SearchPlaceholderText => $"{"Search".L()} {Folder.DisplayText}";

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

	private int nextHistoryIndex, historyCount;

	private readonly FileSystemWatcher watcher;

	private readonly Dispatcher dispatcher;

	private CancellationTokenSource cts;

	public FileTabViewModel(FileTabControl ownerTabControl) {
		OwnerTabControl = ownerTabControl;
		OwnerWindow.EverythingQueryReplied += (i, r) => _ = OnEverythingQueryReplied(i, r);

		GoBackCommand = new SimpleCommand(GoBackAsync);
		GoForwardCommand = new SimpleCommand(GoForwardAsync);
		GoToUpperLevelCommand = new SimpleCommand(GoToUpperLevelAsync);
		SelectionChangedCommand = new SimpleCommand(OnSelectionChanged);
		FileItemCommand = new FileItemCommand {
			TabControlProvider = () => OwnerTabControl,
			SelectedItemsProvider = () => SelectedItems
		};
		SwitchViewCommand = new SimpleCommand(OnSwitchView);
		FileDropCommand = new SimpleCommand(OnDrop);
		ItemDoubleClickedCommand = new SimpleCommand(FileListViewItem_OnDoubleClicked);

		dispatcher = Application.Current.Dispatcher;

		watcher = new FileSystemWatcher {
			NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
		};
		watcher.Changed += Watcher_OnChanged;
		watcher.Created += Watcher_OnCreated;
		watcher.Deleted += Watcher_OnDeleted;
		watcher.Renamed += Watcher_OnRenamed;
		watcher.Error += Watcher_OnError;

		DataObjectContent.ClipboardChanged += OnClipboardChanged;
	}

	private async void OnSwitchView(object e) {
		if (e is MenuItem { CommandParameter: string param } && int.TryParse(param, out var type)) {
			await SwitchViewType(type);
		}
	}

	private bool isLastViewTypeUseLargeIcon;
	/// <summary>
	/// 切换视图时，有的要使用大图标，有的要使用小图标，所以要运行一个Task去更改，取消这个来中断Task
	/// </summary>
	private CancellationTokenSource switchIconCts;

	public async Task SwitchViewType(int type) {
		switch (type) {
		case 0:  // 大图标
			FileViewType = FileViewType.Icons;
			ItemSize = new Size(120d, 170d);
			break;
		case 1:  // 小图标
			FileViewType = FileViewType.Icons;
			ItemSize = new Size(80d, 170d);
			break;
		case 2:  // 列表，size.Width为0代表横向填充
			FileViewType = FileViewType.List;
			ItemSize = new Size(260d, 30d);
			break;
		case 3:  // 详细信息
			FileViewType = FileViewType.Details;
			ItemSize = new Size(0d, 30d);
			break;
		case 4:  // 平铺
			FileViewType = FileViewType.Tiles;
			ItemSize = new Size(280d, 70d);
			break;
		case 5:  // 内容
			FileViewType = FileViewType.Content;
			ItemSize = new Size(0d, 70d);
			break;

		case 6:
			SortBy = DetailListType.Name;
			break;
		case 7:
			SortBy = DetailListType.DateModified;
			break;
		case 8:
			SortBy = DetailListType.Type;
			break;
		case 9:
			SortBy = DetailListType.FileSize;
			break;

		case 10:
			IsAscending = true;
			break;
		case 11:
			IsAscending = false;
			break;

		case 12:
			GroupBy = null;
			break;
		case 13:
			GroupBy = DetailListType.Name;
			break;
		case 14:
			GroupBy = DetailListType.DateModified;
			break;
		case 15:
			GroupBy = DetailListType.Type;
			break;
		case 16:
			GroupBy = DetailListType.FileSize;
			break;
		}

		FileView.CommitChange();
		if (type is >= 0 and <= 5) {
			await SaveViewToDbAsync(null);
			await LoadThumbnailsAsync();
		} else {
			await SaveViewToDbAsync(null);
		}
	}

	/// <summary>
	/// 切换视图模式后可能需要重新加载缩略图
	/// </summary>
	/// <returns></returns>
	private async Task LoadThumbnailsAsync() {
		if (PathType == PathType.LocalFolder) {
			var useLargeIcon = FileViewType is FileViewType.Icons or FileViewType.Tiles or FileViewType.Content;
			if (useLargeIcon != isLastViewTypeUseLargeIcon) {
				switchIconCts?.Cancel();
				var list = Items.Where(item => item is FileItem).Cast<FileItem>().Where(item => item.UseLargeIcon != useLargeIcon).ToArray();
				var cts = switchIconCts = new CancellationTokenSource();
				await Task.Run((() => {
					foreach (var item in list) {
						item.UseLargeIcon = useLargeIcon;
						item.LoadIcon();
					}
				}), cts.Token);
				isLastViewTypeUseLargeIcon = useLargeIcon;
			}
		}
	}

	/// <summary>
	/// 存储到数据库
	/// </summary>
	/// <param name="fileView">为null表示新建，不为null就是修改，要确保是从Db里拿到的对象否则修改没有效果</param>
	/// <returns></returns>
	private async Task SaveViewToDbAsync(FileView fileView) {
		var fullPath = FullPath ?? "ThisPC".L();
		fileView ??= await FileViewDbContext.Instance.FolderViewDbSet.FirstOrDefaultAsync(v => v.FullPath == fullPath);
		if (fileView == null) {
			fileView = new FileView {
				FullPath = fullPath,
				SortBy = SortBy,
				IsAscending = IsAscending,
				GroupBy = GroupBy,
				FileViewType = FileViewType,
				ItemSize = ItemSize,
				DetailLists = DetailLists
			};
			await FileViewDbContext.Instance.FolderViewDbSet.AddAsync(fileView);
		} else {
			Debug.Assert(fileView.FullPath == fullPath);
			fileView.SortBy = SortBy;
			fileView.IsAscending = IsAscending;
			fileView.GroupBy = GroupBy;
			fileView.FileViewType = FileViewType;
			fileView.ItemSize = ItemSize;
			fileView.DetailLists = DetailLists;
		}
		await FileViewDbContext.Instance.SaveChangesAsync();
	}

	private void OnClipboardChanged() {
		CanPaste = DataObjectContent.Clipboard.Type != DataObjectType.Unknown;
	}

	private void AddHistory() {
		Debug.Assert(HistoryList.Count >= nextHistoryIndex);
		if (HistoryList.Count == nextHistoryIndex) {
			if (nextHistoryIndex > 0 && HistoryList[nextHistoryIndex - 1].FullPath == FullPath) {
				// 相同就不记录
				return;
			}
			HistoryList.Add(Folder);
		} else {
			HistoryList[nextHistoryIndex] = Folder;
			for (var i = HistoryList.Count - 1; i > nextHistoryIndex; i--) {
				HistoryList.RemoveAt(i);
			}
		}
		nextHistoryIndex++;
		historyCount = nextHistoryIndex;
	}

	public Task Refresh() {
		return LoadDirectoryAsync(FullPath, false);
	}

	/// <summary>
	/// 添加单个项目，这将会验证文件是否存在，之后将其添加，这在创建新文件时很有用
	/// </summary>
	/// <param name="name">文件或文件夹名，不包含路径</param>
	/// <returns>成功返回添加的项，失败返回null</returns>
	public FileListViewItem AddSingleItem(string name) {
		if (Folder is HomeFolderItem) {
			return null;
		}
		var fullPath = Path.Combine(FullPath, name);
		FileListViewItem item;
		lock (Items) {
			foreach (var fileListViewItem in Items) {
				if (fileListViewItem.Name == name) {
					return fileListViewItem;
				}
			}
			if (File.Exists(fullPath)) {
				item = new FileItem(new FileInfo(fullPath)) {
					UseLargeIcon = FileViewType is FileViewType.Icons or FileViewType.Tiles or FileViewType.Content
				};
			} else if (Directory.Exists(fullPath)) {
				item = new FolderItem(fullPath);
			} else {
				return null;
			}
			Items.Add(item);
		}
		UpdateFolderUI();
		Task.Run(() => {
			item.LoadAttributes();
			item.LoadIcon();
		});
		return item;
	}

	/// <summary>
	/// 加载一个文件夹路径
	/// </summary>
	/// <param name="path">如果为null或者WhiteSpace，就加载“此电脑”</param>
	/// <param name="recordHistory">是否记录历史，返回、前进就为false</param>
	/// <param name="selectedPath">如果是返回，那就把这个设为返回前选中的那一项</param>
	/// <returns></returns>
	public async Task<bool> LoadDirectoryAsync(string path, bool recordHistory = true, string selectedPath = null) {
		watcher.EnableRaisingEvents = false;
		IsLoading = true;
		switchIconCts?.Cancel();
		SelectedItems.Clear();
		cts?.Cancel();

		if (Folder is IDisposable disposable) {
			disposable.Dispose();
		}

		try {
			(Folder, FileItemCommand.CurrentFullPath, PathType) = FolderItem.ParsePath(path);
		} catch (Exception e) {
			hc.MessageBox.Error(e.Message, "CannotOpenPath".L());
		}

		if (Folder == null) {
			var location = PathType == PathType.LocalFile ? path : FileUtils.FindFileLocation(path);
			if (location != null) {
				try {
					Process.Start(new ProcessStartInfo(location) {
						UseShellExecute = true
					});
				} catch (Exception ex) {
					Logger.Exception(ex);
				}
			} else {
				hc.MessageBox.Error("#InvalidPath", "CannotOpenPath".L());
			}

			if (nextHistoryIndex > 0) {
				await GoBackAsync();
			} else {
				await LoadDirectoryAsync(null, false, path);
			}
			return false;
		}

		new Task(Folder.LoadIcon).Start();
		if (Folder.GetType() == typeof(FolderItem) || Folder is DiskDriveItem) {
			try {
				watcher.Path = Folder.FullPath;
				watcher.EnableRaisingEvents = true;
			} catch {
				hc.MessageBox.Error("Error watcher");
			}
		}

		Items.Clear();

#if DEBUG
		var sw = Stopwatch.StartNew();
#endif

		try {
			UpdateUI(nameof(Folder));
			UpdateUI(nameof(FullPath));
		} catch (Exception e) {
			Logger.Exception(e, false);
			hc.MessageBox.Error(string.Format("#ExplorerExCannotFind...".L(), path), "Error".L());
			if (nextHistoryIndex > 0) {
				await GoBackAsync();
			} else {
				await LoadDirectoryAsync(null, false, path);
			}
			return false;
		}

		cts = new CancellationTokenSource();
		var token = cts.Token;
		if (token.IsCancellationRequested) {
			IsLoading = false;
			return false;
		}

#if DEBUG
		Trace.WriteLine($"PropertyUpdateUI costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif

		// 查找数据库看有没有存储当前目录
		FileView savedView;
		try {
			savedView = await FileViewDbContext.Instance.FolderViewDbSet.FirstOrDefaultAsync(v => v.FullPath == FullPath, token);
			if (savedView != null) { // 如果存储了，那就获取用户定义的视图模式
				SortBy = savedView.SortBy;
				IsAscending = savedView.IsAscending;
				FileViewType = savedView.FileViewType;
				if (FileViewType == FileViewType.Details) {
					DetailLists = savedView.DetailLists;
					ItemSize = new Size(0d, 30d);
				} else {
					ItemSize = savedView.ItemSize;
				}
			}
		} catch {
			savedView = null;
		}

		if (token.IsCancellationRequested) {
			IsLoading = false;
			return false;
		}

		if (savedView == null) {
			SortBy = DetailListType.Name;
			IsAscending = true;
			FileViewType = FileViewType.Details;  // 没有保存，默认使用详细信息
			DetailLists = null;
			ItemSize = new Size(0d, 30d);
		}
		GroupBy = null;

#if DEBUG
		Trace.WriteLine($"Query Database costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif

		if (token.IsCancellationRequested) {
			IsLoading = false;
			return false;
		}

		List<FileListViewItem> fileListViewItems;
		FileListViewItem scrollIntoItem;

		try {
			(fileListViewItems, scrollIntoItem) = await Task.Run(() => {
				var items = Folder.EnumerateItems(selectedPath, out var selectedItem, token);
				return (items, selectedItem);
			}, token);
		} catch (Exception e) {
			Logger.Exception(e);
			if (nextHistoryIndex > 0) {
				await GoBackAsync();
			} else {
				await LoadDirectoryAsync(null, false, path);
			}
			return false;
		}

		if (token.IsCancellationRequested) {
			IsLoading = false;
			return false;
		}

		if (scrollIntoItem != null) {
			SelectedItems.Add(scrollIntoItem);
		}

#if DEBUG
		Trace.WriteLine($"Enumerate {path} costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif
		FileView.CommitChange();  // 一旦调用这个，模板就会改变，所以要在清空之后，不然会导致排版混乱和绑定失败

		if (fileListViewItems.Count > 0) {
			foreach (var fileListViewItem in fileListViewItems) {
				Items.Add(fileListViewItem);
			}
			_ = dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => {
				GroupBy = savedView?.GroupBy;  // Loaded之后再执行，不然会非常卡QAQ
				FileListView.ScrollIntoView(scrollIntoItem);
				IsLoading = false;
			});
		}

		if (recordHistory) {
			AddHistory();
		}
		UpdateFolderUI();
		UpdateFileUI();
		if (token.IsCancellationRequested) {
			return false;
		}

#if DEBUG
		Trace.WriteLine($"Update UI costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif

		try {
			if (fileListViewItems.Count > 0) {
				await Parallel.ForEachAsync(fileListViewItems, token, (item, token) => {
					if (token.IsCancellationRequested) {
						return ValueTask.FromCanceled(token);
					}
					item.LoadAttributes();
					return ValueTask.CompletedTask;
				});

				var useLargeIcon = FileViewType is FileViewType.Icons or FileViewType.Tiles or FileViewType.Content;
				await Task.Run(() => {
					foreach (var item in fileListViewItems) {
						if (token.IsCancellationRequested) {
							return;
						}
						if (item is FileItem fileItem) {
							fileItem.UseLargeIcon = useLargeIcon;
						}
						item.LoadIcon();
					}
				}, token);
			}
		} catch (Exception e) {
			Logger.Exception(e);
		}

#if DEBUG
		Trace.WriteLine($"Async load costs: {sw.ElapsedMilliseconds}ms");
		sw.Stop();
#endif

		return !token.IsCancellationRequested;
	}

	/// <summary>
	/// 回到上一页
	/// </summary>
	public Task GoBackAsync() {
		if (CanGoBack) {
			nextHistoryIndex--;
			return LoadDirectoryAsync(HistoryList[nextHistoryIndex - 1].FullPath, false, HistoryList[nextHistoryIndex].FullPath);
		}
		return Task.FromResult(false);
	}

	/// <summary>
	/// 前进一页
	/// </summary>
	public Task GoForwardAsync() {
		if (CanGoForward) {
			nextHistoryIndex++;
			return LoadDirectoryAsync(HistoryList[nextHistoryIndex - 1].FullPath, false, nextHistoryIndex >= HistoryList.Count ? null : HistoryList[nextHistoryIndex].FullPath);
		}
		return Task.FromResult(false);
	}

	/// <summary>
	/// 向上一级
	/// </summary>
	/// <returns></returns>
	public Task GoToUpperLevelAsync() {
		if (CanGoToUpperLevel) {
			if (FullPath.Length == 3) {
				return LoadDirectoryAsync(null);
			}
			var lastIndexOfSlash = FullPath.LastIndexOf('\\');
			return lastIndexOfSlash switch {
				-1 => LoadDirectoryAsync(null),
				2 => LoadDirectoryAsync(FullPath[..3]),  // 例如F:\，此时需要保留最后的\
				_ => LoadDirectoryAsync(FullPath[..lastIndexOfSlash])
			};
		}
		return Task.FromResult(false);
	}

	/// <summary>
	/// 和文件夹相关的UI，和是否选中文件无关
	/// </summary>
	private void UpdateFolderUI() {
		UpdateUI(nameof(CanPaste));
		UpdateUI(nameof(CanGoBack));
		UpdateUI(nameof(CanGoForward));
		UpdateUI(nameof(GoBackButtonToolTip));
		UpdateUI(nameof(GoForwardButtonToolTip));
		UpdateUI(nameof(CanGoToUpperLevel));
		UpdateUI(nameof(GoToUpperLevelButtonToolTip));
		UpdateUI(nameof(FileItemsCount));
		UpdateUI(nameof(SearchPlaceholderText));
	}

	/// <summary>
	/// 和文件相关的UI，选择更改时更新
	/// </summary>
	private void UpdateFileUI() {
		if (PathType == PathType.Home) {
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
		UpdateUI(nameof(IsItemSelected));
		UpdateUI(nameof(CanDeleteOrCut));
		UpdateUI(nameof(SelectedFileItemsCountVisibility));
		UpdateUI(nameof(SelectedFileItemsCount));
		UpdateUI(nameof(SelectedFileItemsSizeVisibility));
		UpdateUI(nameof(SelectedFileItemsSizeText));
	}

	public async void FileListViewItem_OnDoubleClicked(object args) {
		var isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
		var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
		var isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
		switch (((ItemClickEventArgs)args).Item) {
		// 双击事件
		case DiskDriveItem ddi:
			if (isCtrlPressed) {
				await OwnerTabControl.OpenPathInNewTabAsync(ddi.Drive.Name);
			} else if (isShiftPressed) {
				new MainWindow(ddi.Drive.Name).Show();
			} else if (isAltPressed) {
				Shell32Interop.ShowFileProperties(ddi.Drive.Name);
			} else {
				await LoadDirectoryAsync(ddi.Drive.Name);
			}
			break;
		case FileSystemItem fsi:
			if (isAltPressed) {
				Shell32Interop.ShowFileProperties(fsi.FullPath);
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
					FileItemCommand.OpenFile(fsi, isCtrlPressed || isShiftPressed);
				}
			}
			break;
		}
	}

	private void OnSelectionChanged(object args) {
		var e = (SelectionChangedEventArgs)args;
		foreach (FileListViewItem addedItem in e.AddedItems) {
			SelectedItems.Add(addedItem);
			addedItem.IsSelected = true;
		}
		foreach (FileListViewItem removedItem in e.RemovedItems) {
			SelectedItems.Remove(removedItem);
			removedItem.IsSelected = false;
		}
		UpdateFileUI();
	}


	private void OnDrop(object args) {
		var e = (FileDropEventArgs)args;
		var path = e.Path ?? FullPath;
		FileUtils.HandleDrop(e.Content, path, e.DragEventArgs.Effects.GetFirstEffect());
	}

	private uint everythingQueryId;

	/// <summary>
	/// 当用户更改SearchTextBox时触发
	/// </summary>
	private void UpdateSearch() {
		everythingReplyCts?.Cancel();
		if (string.IsNullOrEmpty(searchText)) {
			LoadDirectoryAsync(FullPath).Wait();
			return;
		}
		if (EverythingInterop.IsAvailable) {
			Items.Clear();  // 清空文件列表，进入搜索模式

			PathType = PathType.Search;
			FileView.CommitChange();
			// EverythingInterop.Reset();
			EverythingInterop.Search = PathType == PathType.LocalFolder ? FullPath + ' ' + searchText : searchText;
			EverythingInterop.Max = 999;
			// EverythingInterop.SetRequestFlags(EverythingInterop.RequestType.FullPathAndFileName | EverythingInterop.RequestType.DateModified | EverythingInterop.RequestType.Size);
			// EverythingInterop.SetSort(EverythingInterop.SortMode.NameAscending);
			var mainWindow = OwnerTabControl.MainWindow;
			mainWindow.UnRegisterEverythingQuery(everythingQueryId);
			everythingQueryId = mainWindow.RegisterEverythingQuery();
			EverythingInterop.Query(false);
		} else {
			hc.MessageBox.Error("EverythingIsNotAvailable".L());
		}
	}

	private CancellationTokenSource everythingReplyCts;

	private async Task OnEverythingQueryReplied(uint id, EverythingInterop.QueryReply reply) {
		if (id != everythingQueryId) {
			return;
		}
		PathType = PathType.Home;
		everythingReplyCts?.Cancel();
		everythingReplyCts = new CancellationTokenSource();
		var token = everythingReplyCts.Token;
		List<FileSystemItem> list = null;
		await Task.Run(() => {
			list = new List<FileSystemItem>(reply.FullPaths.Length);
			foreach (var fullPath in reply.FullPaths) {
				if (token.IsCancellationRequested) {
					return;
				}
				try {
					if (Directory.Exists(fullPath)) {
						list.Add(new FolderItem(fullPath));
					} else if (File.Exists(fullPath)) {
						list.Add(new FileItem(new FileInfo(fullPath)));
					}
				} catch (Exception e) {
					Logger.Exception(e, false);
					break;
				}
			}
		}, token);
		if (token.IsCancellationRequested) {
			return;
		}

		Items.Clear();
		foreach (var fileListViewItem in list) {
			Items.Add(fileListViewItem);
		}

		UpdateFolderUI();
		UpdateFileUI();

		await Task.Run(() => {
			foreach (var fileViewBaseItem in list.Where(item => !item.IsFolder)) {
				if (token.IsCancellationRequested) {
					return;
				}
				fileViewBaseItem.LoadIcon();
			}
		}, token);
	}

	private static void Watcher_OnError(object sender, ErrorEventArgs e) {
		Logger.Error(e.GetException().Message);
	}

	private void Watcher_OnRenamed(object sender, RenamedEventArgs e) {
		dispatcher.Invoke(() => {
			for (var i = 0; i < Items.Count; i++) {
				if (((FileSystemItem)Items[i]).FullPath == e.OldFullPath) {
					if (Directory.Exists(e.FullPath)) {
						var item = new FolderItem(e.FullPath);
						Items[i] = item;
						Task.Run(() => {
							item.LoadAttributes();
							item.LoadIcon();
						});
					} else if (File.Exists(e.FullPath)) {
						var item = new FileItem(new FileInfo(e.FullPath));
						Items[i] = item;
						Task.Run(() => {
							item.LoadAttributes();
							item.LoadIcon();
						});
					}
					return;
				}
			}
		});
	}

	private void Watcher_OnDeleted(object sender, FileSystemEventArgs e) {
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
		dispatcher.Invoke(() => {
			if (Items.Any(i => i.Name == e.Name)) {
				return;
			}
			FileSystemItem item;
			if (Directory.Exists(e.FullPath)) {
				item = new FolderItem(e.FullPath);
			} else if (File.Exists(e.FullPath)) {
				item = new FileItem(new FileInfo(e.FullPath));
			} else {
				return;
			}
			Items.Add(item);
			Task.Run(() => {
				item.LoadAttributes();
				item.LoadIcon();
			});
		});
	}

	private void Watcher_OnChanged(object sender, FileSystemEventArgs e) {
		dispatcher.Invoke(() => {
			// ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
			foreach (FileSystemItem item in Items) {
				if (item.FullPath == e.FullPath) {
					Task.Run(item.Refresh);
					return;
				}
			}
		});
	}

	public void Dispose() {
		DataObjectContent.ClipboardChanged -= OnClipboardChanged;
		Items.Clear();
		watcher?.Dispose();
		cts?.Dispose();
		everythingReplyCts?.Dispose();
		GC.SuppressFinalize(this);
	}
}