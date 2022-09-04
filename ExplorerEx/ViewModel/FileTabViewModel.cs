using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using ExplorerEx.Model.Enums;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.Utils.Collections;
using ExplorerEx.View;
using ExplorerEx.View.Controls;
using ExplorerEx.Win32;
using static ExplorerEx.Model.FileListViewItem;
using static ExplorerEx.View.Controls.FileListView;
using hc = HandyControl.Controls;

namespace ExplorerEx.ViewModel;

/// <summary>
/// 对应一个Tab
/// </summary>
public class FileTabViewModel : NotifyPropertyChangedBase, IDisposable {
	public FileTabControl OwnerTabControl { get; }

	public MainWindow OwnerWindow => OwnerTabControl.MainWindow;

	public FileListView FileListView { get; set; } = null!;

	/// <summary>
	/// 当前路径文件夹
	/// </summary>
	public FolderItem Folder { get; private set; } = HomeFolderItem.Singleton;

	/// <summary>
	/// 当前的路径，如果是首页，就是null
	/// </summary>
	public string FullPath => Folder.FullPath;

	public FileView FileView { get; } = new() { StageChangeEnabled = true };

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

	public List<DetailList>? DetailLists {
		get => FileView.DetailLists;
		private set => FileView.DetailLists = value;
	}

	/// <summary>
	/// 当前文件夹内的文件列表
	/// </summary>
	public ConcurrentObservableCollection<FileListViewItem> Items { get; } = new() { UseHashSet = true };

	/// <summary>
	/// 当前文件夹是否在加载
	/// </summary>
	public bool IsLoading {
		get => isLoading;
		set {
			if (isLoading != value) {
				isLoading = value;
				OnPropertyChanged();
			}
		}
	}

	private bool isLoading;

	public HashSet<FileListViewItem> SelectedItems { get; } = new();

	public bool CanGoBack => nextHistoryIndex > 1;

	public SimpleCommand GoBackCommand { get; }

	public string? GoBackButtonToolTip => CanGoBack ? string.Format("GoBackTo...".L(), HistoryList[nextHistoryIndex - 2].DisplayText) : null;

	public bool CanGoForward => nextHistoryIndex < historyCount;

	public SimpleCommand GoForwardCommand { get; }

	public string? GoForwardButtonToolTip => CanGoForward ? string.Format("GoForwardTo...".L(), HistoryList[nextHistoryIndex].DisplayText) : null;

	/// <summary>
	/// 历史记录
	/// </summary>
	public ObservableCollection<FolderItem> HistoryList { get; } = new();

	public bool CanGoToUpperLevel => PathType != PathType.Home;

	public SimpleCommand GoToUpperLevelCommand { get; }

	public string? GoToUpperLevelButtonToolTip {
		get {
			if (!CanGoToUpperLevel) {
				return null;
			}
			return string.Format("GoUpTo...".L(), ParentFolderName);
		}
	}

	private string? ParentFolderName {
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

	/// <summary>
	/// 文件项的Command
	/// </summary>
	public FileItemCommand FileItemCommand { get; }

	/// <summary>
	/// 创建文件或文件夹
	/// </summary>
	public SimpleCommand CreateCommand { get; }

	/// <summary>
	/// 改变文件视图模式
	/// </summary>
	public SimpleCommand SwitchViewCommand { get; }

	public SimpleCommand ItemDoubleClickedCommand { get; }

	public bool CanPaste {
		get => canPaste && !Folder.IsReadonly;
		private set {
			if (canPaste != value) {
				canPaste = value;
				OnPropertyChanged();
			}
		}
	}

	private bool canPaste;

	public Visibility SelectedFileItemsCountVisibility => SelectedItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

	public int SelectedFileItemsCount => SelectedItems.Count;

	public string? SelectedFileItemsSizeText { get; private set; }

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
	public string? SearchText {
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

	private string? searchText;

	private int nextHistoryIndex, historyCount;

	private readonly ThreadedFileSystemWatcher watcher;

	private readonly Dispatcher dispatcher;

	private CancellationTokenSource cts = new();

	private readonly LoadDetailsOptions loadDetailsOptions = new();

	/// <summary>
	/// 给FileTabItem使用
	/// </summary>
	internal bool playTabAnimation = true;

	//private int totalLoadedFiles;

	//private const int GcThreshold = 3000;

	public FileTabViewModel(FileTabControl ownerTabControl) {
		OwnerTabControl = ownerTabControl;
		OwnerWindow.EverythingQueryReplied += (i, r) => _ = OnEverythingQueryReplied(i, r);

		GoBackCommand = new SimpleCommand(GoBackAsync);
		GoForwardCommand = new SimpleCommand(GoForwardAsync);
		GoToUpperLevelCommand = new SimpleCommand(GoToUpperLevelAsync);
		FileItemCommand = new FileItemCommand {
			TabControlProvider = () => OwnerTabControl,
			SelectedItemsProvider = () => SelectedItems
		};
		CreateCommand = new SimpleCommand(OnCreate);
		SwitchViewCommand = new SimpleCommand(OnSwitchView);
		ItemDoubleClickedCommand = new SimpleCommand(FileListViewItem_OnDoubleClicked);

		dispatcher = Application.Current.Dispatcher;

		watcher = new ThreadedFileSystemWatcher {
			NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
		};
		watcher.Changed += Watcher_OnChanged;
		watcher.Error += Watcher_OnError;

		DataObjectContent.ClipboardChanged += OnClipboardChanged;
	}

	private void OnCreate(object? param) {
		if (PathType == PathType.Home) {
			return;
		}
		if (param is CreateFileItem item) {
			try {
				var fileName = item.GetCreateName(FullPath);
				var newName = OwnerWindow.StartRename("Create".L(), fileName, item != CreateFolderItem.Singleton);
				if (newName != null) {
					if (item.Create(FullPath, newName)) {
						var newItem = AddSingleItem(newName);
						if (newItem == null) {
							return;
						}
						newItem.IsSelected = true;
						FileListView.ScrollIntoView(newItem);
					} else {
						hc.MessageBox.Error("", "CannotCreate".L());
					}
				}
			} catch (Exception e) {
				hc.MessageBox.Error(e.Message, "CannotCreate".L());
			}
		}
	}

	/// <summary>
	/// 添加单个项目，这将会验证文件是否存在，之后将其添加，这在创建新文件时很有用
	/// </summary>
	/// <param name="name">文件或文件夹名，不包含路径</param>
	/// <returns>成功返回添加的项，失败返回null</returns>
	public FileListViewItem? AddSingleItem(string name) {
		if (Folder is HomeFolderItem) {
			return null;
		}
		var fullPath = Path.Combine(FullPath, name);
		FileListViewItem item;
		foreach (var fileListViewItem in Items) {
			if (fileListViewItem.Name == name) {
				return fileListViewItem;
			}
		}
		if (File.Exists(fullPath)) {
			item = new FileItem(new FileInfo(fullPath));
		} else if (Directory.Exists(fullPath)) {
			item = new FolderItem(new DirectoryInfo(fullPath));
		} else {
			return null;
		}
		Items.Add(item);
		UpdateFolderUI();
		Task.Run(() => {
			item.LoadAttributes(loadDetailsOptions);
			item.LoadIcon(loadDetailsOptions);
		});
		return item;
	}

	public void StartRename(string? fileName) {
		if (fileName == null) {
			return;
		}
		var item = Items.FirstOrDefault(item => item.Name == fileName);
		if (item == null) {
			if ((item = AddSingleItem(fileName)) == null) {
				return;
			}
		}
		StartRename(item);
	}

	public void StartRename(FileListViewItem item) {
		FileListView.ScrollIntoView(item);
		item.IsSelected = true;
		var originalName = item.GetRenameName();
		if (originalName == null) {
			return;
		}
		var newName = OwnerWindow.StartRename("Rename".L(), originalName, item.IsFolder);
		if (newName != null && newName != originalName) {
			try {
				item.Rename(newName);
				Items.Remove(item);
			} catch (Exception e) {
				ContentDialog.Error(e.Message, null, OwnerWindow);
			}
		}
	}

	private async void OnSwitchView(object? e) {
		if (e is ViewSortGroup type) {
			await SwitchViewType(type);
		}
	}

	/// <summary>
	/// 切换视图时，有的要使用大图标，有的要使用小图标，所以要运行一个Task去更改，取消这个来中断Task
	/// </summary>
	private CancellationTokenSource? switchIconCts;

	public async Task SwitchViewType(ViewSortGroup type) {
		switch (type) {
		case ViewSortGroup.LargeIcons:  // 大图标
			FileViewType = FileViewType.Icons;
			ItemSize = new Size(180d, 240d);
			break;
		case ViewSortGroup.MediumIcons:  // 中图标
			FileViewType = FileViewType.Icons;
			ItemSize = new Size(120d, 170d);
			break;
		case ViewSortGroup.SmallIcons:  // 小图标
			FileViewType = FileViewType.Icons;
			ItemSize = new Size(80d, 130d);
			break;
		case ViewSortGroup.List:  // 列表，size.Width为0代表横向填充
			FileViewType = FileViewType.List;
			ItemSize = new Size(260d, 30d);
			break;
		case ViewSortGroup.Details:  // 详细信息
			FileViewType = FileViewType.Details;
			ItemSize = new Size(0d, 30d);
			break;
		case ViewSortGroup.Tiles:  // 平铺
			FileViewType = FileViewType.Tiles;
			ItemSize = new Size(280d, 70d);
			break;
		case ViewSortGroup.Content:  // 内容
			FileViewType = FileViewType.Content;
			ItemSize = new Size(0d, 70d);
			break;

		case ViewSortGroup.SortByName:
			SortBy = DetailListType.Name;
			break;
		case ViewSortGroup.SortByDateModified:
			SortBy = DetailListType.DateModified;
			break;
		case ViewSortGroup.SortByType:
			SortBy = DetailListType.Type;
			break;
		case ViewSortGroup.SortByFileSize:
			SortBy = DetailListType.FileSize;
			break;

		case ViewSortGroup.Ascending:
			IsAscending = true;
			break;
		case ViewSortGroup.Descending:
			IsAscending = false;
			break;

		case ViewSortGroup.GroupByNone:
			GroupBy = null;
			break;
		case ViewSortGroup.GroupByName:
			GroupBy = DetailListType.Name;
			break;
		case ViewSortGroup.GroupByDateModified:
			GroupBy = DetailListType.DateModified;
			break;
		case ViewSortGroup.GroupByType:
			GroupBy = DetailListType.Type;
			break;
		case ViewSortGroup.GroupByFileSize:
			GroupBy = DetailListType.FileSize;
			break;
		}

		FileView.CommitChange();
		await SaveViewToDbAsync(null);

		var useLargeIcon = FileViewType is FileViewType.Icons or FileViewType.Tiles or FileViewType.Content;
		if (PathType == PathType.LocalFolder && useLargeIcon != loadDetailsOptions.UseLargeIcon) {
			loadDetailsOptions.UseLargeIcon = useLargeIcon;
			await LoadThumbnailsAsync();
		}
	}

	/// <summary>
	/// 切换视图模式后可能需要重新加载缩略图
	/// </summary>
	/// <returns></returns>
	private Task LoadThumbnailsAsync() {
		switchIconCts?.Cancel();
		var list = Items.Where(item => item is FileItem).Cast<FileItem>().ToImmutableArray();
		var cts = switchIconCts = new CancellationTokenSource();
		return Task.Run(() => {
			foreach (var item in list) {
				item.LoadIcon(loadDetailsOptions);
			}
		}, cts.Token);
	}

	/// <summary>
	/// 存储到数据库
	/// </summary>
	/// <param name="fileView">为null表示新建，不为null就是修改，要确保是从Db里拿到的对象否则修改没有效果</param>
	/// <returns></returns>
	private Task SaveViewToDbAsync(FileView? fileView) {
		var fullPath = FullPath;
		var dbCtx = App.FileViewDbContext;
		fileView ??= dbCtx.FirstOrDefault(v => v.FullPath == fullPath);
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
			dbCtx.Add(fileView);
		} else {
			Debug.Assert(fileView.FullPath == fullPath);
			fileView.SortBy = SortBy;
			fileView.IsAscending = IsAscending;
			fileView.GroupBy = GroupBy;
			fileView.FileViewType = FileViewType;
			fileView.ItemSize = ItemSize;
			fileView.DetailLists = DetailLists;
			dbCtx.Update(fileView);
		}
		return dbCtx.SaveAsync();
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

	public void Refresh() {
		_ = LoadDirectoryAsync(FullPath, false);
	}

	/// <summary>
	/// 加载一个文件夹路径，不会产生任何异常
	/// </summary>
	/// <param name="path">如果为null或者WhiteSpace，就加载“此电脑”</param>
	/// <param name="recordHistory">是否记录历史，返回、前进就为false</param>
	/// <param name="selectedPath">如果是返回，那就把这个设为返回前选中的那一项</param>
	/// <returns></returns>
	public async Task<bool> LoadDirectoryAsync(string? path, bool recordHistory = true, string? selectedPath = null) {
		watcher.Enabled = false;
		IsLoading = true;
		switchIconCts?.Cancel();
		SelectedItems.Clear();
		cts.Cancel();

		if (Folder is IDisposable disposable) {
			disposable.Dispose();
		}

		path = path?.Trim();
		if (string.IsNullOrEmpty(path) || path == "$Home") {
			Folder = HomeFolderItem.Singleton;
			PathType = PathType.Home;
		} else {
			FolderItem? folder;
			try {
				(folder, PathType) = FolderItem.ParsePath(path);
			} catch (Exception e) {
				ContentDialog.Error(e.Message, "CannotOpenPath".L(), OwnerWindow);
				await ErrorGoBack();
				return false;
			}

			if (folder == null) {
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
					ContentDialog.Error("#InvalidPath".L(), "CannotOpenPath".L(), OwnerWindow);
				}

				await ErrorGoBack();
				return false;
			}
			Folder = folder;
		}

		FileItemCommand.Folder = Folder;
		Folder.LoadIcon(loadDetailsOptions);

		if (!Folder.IsVirtual) {
			try {
				watcher.Path = Folder.FullPath;
				watcher.Enabled = true;
			} catch {
				hc.MessageBox.Error("Error watcher");
			}
		}

#if DEBUG
		var sw = Stopwatch.StartNew();
#endif

		try {
			OnPropertyChanged(nameof(Folder));
			OnPropertyChanged(nameof(FullPath));
		} catch (Exception e) {
			Logger.Exception(e, false);
			ContentDialog.Error(string.Format("#ExplorerExCannotFind...".L(), path), "Error".L(), OwnerWindow);
			await ErrorGoBack();
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
		FileView? savedView;
		try {
			var fullPath = FullPath;
			savedView = App.FileViewDbContext.FirstOrDefault(v => v.FullPath == fullPath);
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
		FileListViewItem? scrollIntoItem;

		try {
			(fileListViewItems, scrollIntoItem) = await Task.Run(() => {
				var items = Folder.EnumerateItems(selectedPath, out var selectedItem, token);
				return (items, selectedItem);
			}, token);
		} catch (Exception e) {
			if (e is TaskCanceledException) {
				return false;
			}
			Logger.Exception(e);
			await ErrorGoBack();
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
		Items.Clear();
		FileView.CommitChange();  // 一旦调用这个，模板就会改变，所以要在清空之后，不然会导致排版混乱和绑定失败
		loadDetailsOptions.SetPreLoadIconByItemCount(fileListViewItems.Count);
		LoadDetailsOptions.Current.SetPreLoadIconByItemCount(fileListViewItems.Count);
		LoadDetailsOptions.Current.UseLargeIcon = loadDetailsOptions.UseLargeIcon = FileViewType is FileViewType.Icons or FileViewType.Tiles or FileViewType.Content;

		if (fileListViewItems.Count > 0) {
			Items.AddRange(fileListViewItems);
			_ = dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => {
				GroupBy = savedView?.GroupBy;  // Loaded之后再执行，不然会非常卡QAQ
				FileListView?.ScrollIntoView(scrollIntoItem);  // TODO：有时还是null，先用?.
				IsLoading = false;
			});
		} else {
			IsLoading = false;
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

		await LoadDetails(fileListViewItems, loadDetailsOptions, token);

#if DEBUG
		Trace.WriteLine($"Async load costs: {sw.ElapsedMilliseconds}ms");
		sw.Stop();
#endif

		return !token.IsCancellationRequested;
	}

	/// <summary>
	/// 遇到错误需要返回
	/// </summary>
	/// <returns></returns>
	private Task ErrorGoBack() {
		if (CanGoBack) {
			var originalPath = HistoryList[--nextHistoryIndex].FullPath;
			while (nextHistoryIndex > 1) {
				var currentPath = HistoryList[nextHistoryIndex - 1].FullPath;
				var prevPath = HistoryList[nextHistoryIndex].FullPath;
				if (currentPath.Length > prevPath.Length && currentPath.StartsWith(prevPath)) {  // 如果这个路径包含了之前的路径，那还是不行，继续回退
					nextHistoryIndex--;
				} else if (currentPath == originalPath) {  // 如果相等，还是不得行
					nextHistoryIndex--;
				} else {
					break;
				}
			}
			return LoadDirectoryAsync(HistoryList[nextHistoryIndex - 1].FullPath, false, FullPath);
		}
		return LoadDirectoryAsync(null, false, FullPath);
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
		OnPropertyChanged(nameof(CanPaste));
		OnPropertyChanged(nameof(CanGoBack));
		OnPropertyChanged(nameof(CanGoForward));
		OnPropertyChanged(nameof(GoBackButtonToolTip));
		OnPropertyChanged(nameof(GoForwardButtonToolTip));
		OnPropertyChanged(nameof(CanGoToUpperLevel));
		OnPropertyChanged(nameof(GoToUpperLevelButtonToolTip));
		OnPropertyChanged(nameof(SearchPlaceholderText));
	}

	/// <summary>
	/// 和文件相关的UI，选择更改时更新
	/// </summary>
	private void UpdateFileUI() {
		if (PathType == PathType.Home) {
			SelectedFileItemsSizeText = null;
		} else {
			var size = SelectedFilesSize;
			if (size == -1) {
				SelectedFileItemsSizeText = null;
			} else {
				SelectedFileItemsSizeText = FileUtils.FormatByteSize(size);
			}
		}
		OnPropertyChanged(nameof(IsItemSelected));
		OnPropertyChanged(nameof(CanDeleteOrCut));
		OnPropertyChanged(nameof(SelectedFileItemsCountVisibility));
		OnPropertyChanged(nameof(SelectedFileItemsCount));
		OnPropertyChanged(nameof(SelectedFileItemsSizeText));
	}

	public async void FileListViewItem_OnDoubleClicked(object? args) {
		if (args is not ItemClickEventArgs e) {
			return;
		}
		var isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
		var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
		var isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
		switch (e.Item) {
		// 双击事件
		case DiskDriveItem ddi:
			if (isCtrlPressed) {
				await OwnerTabControl.OpenPathInNewTabAsync(ddi.Drive.Name);
			} else if (isShiftPressed) {
				new MainWindow(ddi.Drive.Name).Show();
			} else if (isAltPressed) {
				Shell32Interop.ShowProperties(ddi);
			} else {
				await LoadDirectoryAsync(ddi.Drive.Name);
			}
			break;
		case FileSystemItem fsi:
			if (isAltPressed) {
				Shell32Interop.ShowProperties(fsi);
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

	public void ChangeSelection(SelectionChangedEventArgs e) {
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

	private uint everythingQueryId;

	/// <summary>
	/// 当用户更改SearchTextBox时触发
	/// </summary>
	private void UpdateSearch() {
		everythingReplyCts?.Cancel();
		if (string.IsNullOrEmpty(searchText)) {
			_ = LoadDirectoryAsync(FullPath);
			return;
		}
		if (EverythingInterop.IsAvailable) {
			Items.Clear();  // 清空文件列表，进入搜索模式

			PathType = PathType.Search;
			FileView.CommitChange();
			EverythingInterop.Search = PathType == PathType.LocalFolder ? FullPath + ' ' + searchText : searchText;
			EverythingInterop.Max = 999;
			var mainWindow = OwnerTabControl.MainWindow;
			mainWindow.UnRegisterEverythingQuery(everythingQueryId);
			everythingQueryId = mainWindow.RegisterEverythingQuery();
			EverythingInterop.Query(false);
		} else {
			hc.MessageBox.Error("EverythingIsNotAvailable".L());
		}
	}

	private CancellationTokenSource? everythingReplyCts;

	private async Task OnEverythingQueryReplied(uint id, EverythingInterop.QueryReply reply) {
		if (id != everythingQueryId) {
			return;
		}
		PathType = PathType.Home;
		everythingReplyCts?.Cancel();
		everythingReplyCts = new CancellationTokenSource();
		var token = everythingReplyCts.Token;
		var fileListViewItems = await Task.Run(() => {
			var fileListViewItems = new List<FileSystemItem>(reply.FullPaths.Length);
			foreach (var fullPath in reply.FullPaths) {
				if (token.IsCancellationRequested) {
					return null;
				}
				try {
					if (Directory.Exists(fullPath)) {
						fileListViewItems.Add(new FolderItem(new DirectoryInfo(fullPath)));
					} else if (File.Exists(fullPath)) {
						fileListViewItems.Add(new FileItem(new FileInfo(fullPath)));
					}
				} catch (Exception e) {
					Logger.Exception(e, false);
					break;
				}
			}
			return fileListViewItems;
		}, token);

		if (token.IsCancellationRequested) {
			return;
		}

		Items.Reset(fileListViewItems!);

		UpdateFolderUI();
		UpdateFileUI();

		await LoadDetails(fileListViewItems, loadDetailsOptions, token);
	}

	private void Watcher_OnError(Exception e) {
		if (e is Win32Exception { NativeErrorCode: 5 }) {  // 当前目录不复存在力，被删除力
			string? parentPath;
			while (true) {
				parentPath = Path.GetDirectoryName(FullPath);
				if (parentPath == null) {
					break;
				}
				var di = new DirectoryInfo(parentPath);
				di.Refresh();
				if (di.Exists) {
					break;
				}
			}
			_ = LoadDirectoryAsync(parentPath, false);
		} else {
			Logger.Error(e.Message);
		}
	}

	private void Watcher_OnChanged(FileSystemChangeEvent e) {
		FileSystemItem? oldItem = null;
		foreach (var item in Items.OfType<FileSystemItem>()) {
			if (cts.IsCancellationRequested) {
				return;
			}
			if (item.FullPath == e.FullPath) {
				oldItem = item;
				break;
			}
		}
		switch (e.ChangeType) {
		case WatcherChangeTypes.Created: {
			if (oldItem == null) {
				FileSystemItem newItem;
				if (Directory.Exists(e.FullPath)) {
					newItem = new FolderItem(new DirectoryInfo(e.FullPath));
				} else if (File.Exists(e.FullPath)) {
					newItem = new FileItem(new FileInfo(e.FullPath));
				} else {
					return;
				}
				Items.Add(newItem);
				Task.Run(() => {
					newItem.LoadAttributes(loadDetailsOptions); // TODO: 需要一个专有线程去加载……
					newItem.LoadIcon(loadDetailsOptions);
				});
			} else {
				Task.Run(() => oldItem.Refresh(loadDetailsOptions)); // TODO: 需要一个专有线程去加载……
			}
			break;
		}
		case WatcherChangeTypes.Changed:
			if (oldItem != null) {
				Task.Run(() => oldItem.Refresh(loadDetailsOptions)); // TODO: 需要一个专有线程去加载……
			}
			break;
		case WatcherChangeTypes.Renamed: {
			FileSystemItem? newItem = null;
			if (Directory.Exists(e.FullPath)) {
				newItem = new FolderItem(new DirectoryInfo(e.FullPath));
				Task.Run(() => { // TODO: 需要一个专有线程去加载……
					newItem.LoadAttributes(loadDetailsOptions);
					newItem.LoadIcon(loadDetailsOptions);
				});
			} else if (File.Exists(e.FullPath)) {
				newItem = new FileItem(new FileInfo(e.FullPath));
				Task.Run(() => { // TODO: 需要一个专有线程去加载……
					newItem.LoadAttributes(loadDetailsOptions);
					newItem.LoadIcon(loadDetailsOptions);
				});
			}
			if (newItem == null) {
				if (oldItem != null) {
					Items.Remove(oldItem);
				}
			} else {
				if (oldItem == null) {
					Items.Add(newItem);
				} else {
					Items.Replace(oldItem, newItem);
				}
			}
			break;
		}
		case WatcherChangeTypes.Deleted:
			if (oldItem != null) {
				SelectedItems.Remove(oldItem);
				Items.Remove(oldItem);
			}
			break;
		}
	}

	~FileTabViewModel() {
		DataObjectContent.ClipboardChanged -= OnClipboardChanged;
	}

	public void Dispose() {
		Items.Clear();
		watcher.Dispose();
		cts.Dispose();
		everythingReplyCts?.Dispose();
		GC.SuppressFinalize(this);
	}
}