using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
public class FileGridViewModel : SimpleNotifyPropertyChanged, IDisposable {
	public FileTabControl OwnerTabControl { get; }

	public MainWindow OwnerWindow => OwnerTabControl.MainWindow;

	public FileListView FileListView { get; set; }

	/// <summary>
	/// 当前路径的FileViewBaseItem
	/// </summary>
	public FileItem Folder { get; private set; } = Home.Instance;

	/// <summary>
	/// 当前的路径，如果是首页，就是“此电脑”
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
	public ObservableCollection<FileItem> Items { get; } = new();

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

	public ObservableHashSet<FileItem> SelectedItems { get; } = new();

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
	public ObservableCollection<FileItem> HistoryList { get; } = new();

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
			case Home:
				return null;
			case DiskDrive:
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
		get => canPaste && Folder is not Home;
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

	public FileGridViewModel(FileTabControl ownerTabControl) {
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
		ItemDoubleClickedCommand = new SimpleCommand(OnItemDoubleClicked);

		dispatcher = Application.Current.Dispatcher;

		watcher = new FileSystemWatcher {
			NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.DirectoryName |
			               NotifyFilters.FileName | NotifyFilters.LastWrite |
			               NotifyFilters.Size
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
		if (PathType == PathType.Normal) {
			var useLargeIcon = FileViewType is FileViewType.Icons or FileViewType.Tiles or FileViewType.Content;
			if (useLargeIcon != isLastViewTypeUseLargeIcon) {
				switchIconCts?.Cancel();
				var list = Items.Where(item => item is FileSystemItem && !item.IsFolder).Cast<FileSystemItem>().Where(item => item.UseLargeIcon != useLargeIcon).ToArray();
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
		fileView ??= await FileViewDbContext.Instance.FolderViewDbSet.FirstOrDefaultAsync(v => v.FullPath == FullPath);
		if (fileView == null) {
			fileView = new FileView {
				FullPath = FullPath,
				SortBy = SortBy,
				IsAscending = IsAscending,
				GroupBy = GroupBy,
				FileViewType = FileViewType,
				ItemSize = ItemSize,
				DetailLists = DetailLists
			};
			await FileViewDbContext.Instance.FolderViewDbSet.AddAsync(fileView);
		} else {
			Debug.Assert(fileView.FullPath == FullPath);
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

	private void AddHistory(FileItem item) {
		Debug.Assert(HistoryList.Count >= nextHistoryIndex);
		if (HistoryList.Count == nextHistoryIndex) {
			if (nextHistoryIndex > 0 && HistoryList[nextHistoryIndex - 1].FullPath == item.FullPath) {
				return;  // 如果相同就不记录
			}
			HistoryList.Add(item);
		} else {
			HistoryList[nextHistoryIndex] = item;
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
	/// 添加单个项目，这将会验证文件是否存在，之后将其添加，这在创建新文件时很有用
	/// </summary>
	/// <param name="name">文件或文件夹名，不包含路径</param>
	/// <returns>成功返回添加的项，失败返回null</returns>
	public FileItem AddSingleItem(string name) {
		if (Folder is Home) {
			return null;
		}
		var fullPath = Path.Combine(FullPath, name);
		FileItem item;
		if (File.Exists(fullPath)) {
			item = new FileSystemItem(new FileInfo(fullPath)) {
				UseLargeIcon = FileViewType is FileViewType.Icons or FileViewType.Tiles or FileViewType.Content
			};
		} else if (Directory.Exists(fullPath)) {
			item = new FileSystemItem(new DirectoryInfo(fullPath));
		} else {
			return null;
		}
		Items.Add(item);
		UpdateFolderUI();
		Task.Run(item.LoadAttributes);
		Task.Run(item.LoadIcon);
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
		IsLoading = true;
		switchIconCts?.Cancel();
		SelectedItems.Clear();
		var isLoadHome = string.IsNullOrWhiteSpace(path) || path == "ThisPC".L() || path.ToLower() == Home.Uuid;  // 加载“此电脑”

		if (!isLoadHome && !Directory.Exists(path)) {
			IsLoading = false;
			var err = new StringBuilder();
			err.Append("Cannot_open".L()).Append(' ').Append(path).Append('\n').Append("Check_your_input_and_try_again".L());
			hc.MessageBox.Error(err.ToString(), "Error");
			return false;
		}

		cts?.Cancel();
		cts = new CancellationTokenSource();
		var token = cts.Token;
		Items.Clear();

#if DEBUG
		var sw = Stopwatch.StartNew();
#endif

		if (isLoadHome) {
			Folder = Home.Instance;
			FileItemCommand.CurrentFullPath = null;
		} else if (path.Length <= 3) {
			Folder = new DiskDrive(new DriveInfo(path[..1]));
			FileItemCommand.CurrentFullPath = path;
			new Task(Folder.LoadIcon, token).Start();
		} else {
			Folder = new FileSystemItem(new DirectoryInfo(path));
			FileItemCommand.CurrentFullPath = path;
			new Task(Folder.LoadIcon, token).Start();
		}

		UpdateUI(nameof(Folder));
		if (token.IsCancellationRequested) {
			IsLoading = false;
			return false;
		}

#if DEBUG
		Trace.WriteLine($"PropertyUpdateUI costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif

		// 查找数据库看有没有存储当前目录
		var savedView = await FileViewDbContext.Instance.FolderViewDbSet.FirstOrDefaultAsync(v => v.FullPath == FullPath, token);
		if (savedView != null) {  // 如果存储了，那就获取用户定义的视图模式
			SortBy = savedView.SortBy;
			IsAscending = savedView.IsAscending;
			GroupBy = savedView.GroupBy;
			FileViewType = savedView.FileViewType;
			if (FileViewType == FileViewType.Details) {
				DetailLists = savedView.DetailLists;
				ItemSize = new Size(0d, 30d);
			} else {
				ItemSize = savedView.ItemSize;
			}
		} else {
			SortBy = DetailListType.Name;
			IsAscending = true;
			GroupBy = null;
			FileViewType = FileViewType.Details;  // 没有保存，默认使用详细信息
			DetailLists = null;
			ItemSize = new Size(0d, 30d);
		}
		UpdateUI(nameof(FullPath));
		if (token.IsCancellationRequested) {
			IsLoading = false;
			return false;
		}

#if DEBUG
		Trace.WriteLine($"Query Database costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif

		var fileListBuffer = new List<FileItem>(128);
		FileItem scrollIntoItem = null;

		if (isLoadHome) {
			watcher.EnableRaisingEvents = false;

			await Task.Run(() => {
				foreach (var drive in DriveInfo.GetDrives()) {
					if (token.IsCancellationRequested) {
						return;
					}
					var item = new DiskDrive(drive);
					fileListBuffer.Add(item);
					if (drive.Name == selectedPath) {
						item.IsSelected = true;
						SelectedItems.Add(item);
						scrollIntoItem = item;
					}
				}
			}, token);
		} else {
			try {
				watcher.Path = FullPath;
				watcher.EnableRaisingEvents = true;
			} catch {
				hc.MessageBox.Error("AccessDenied".L(), "Cannot_access_directory".L());
				if (nextHistoryIndex > 0) {
					await LoadDirectoryAsync(HistoryList[nextHistoryIndex - 1].FullPath, false, path);
				}
				return false;
			}

			var useLargeIcon = FileViewType is FileViewType.Icons or FileViewType.Tiles or FileViewType.Content;
			await Task.Run(() => {
				foreach (var directory in Directory.EnumerateDirectories(path)) {
					if (token.IsCancellationRequested) {
						return;
					}
					var item = new FileSystemItem(new DirectoryInfo(directory));
					fileListBuffer.Add(item);
					if (directory == selectedPath) {
						item.IsSelected = true;
						SelectedItems.Add(item);
						scrollIntoItem = item;
					}
				}
				foreach (var filePath in Directory.EnumerateFiles(path)) {
					if (token.IsCancellationRequested) {
						return;
					}
					var item = new FileSystemItem(new FileInfo(filePath)) {
						UseLargeIcon = useLargeIcon
					};
					fileListBuffer.Add(item);
					if (filePath == selectedPath) {
						item.IsSelected = true;
						SelectedItems.Add(item);
						scrollIntoItem = item;
					}
				}
			}, token);
		}
		if (token.IsCancellationRequested) {
			IsLoading = false;
			return false;
		}

#if DEBUG
		Trace.WriteLine($"Enumerate {path} costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif
		PathType = isLoadHome ? PathType.Home : PathType.Normal;  // TODO: 网络驱动器、OneDrive等
		FileView.CommitChange();  // 一旦调用这个，模板就会改变，所以要在清空之后，不然会导致排版混乱和绑定失败

		if (fileListBuffer.Count > 0) {
			foreach (var fileViewBaseItem in fileListBuffer) {
				Items.Add(fileViewBaseItem);
			}
			_ = dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => FileListView.ScrollIntoView(scrollIntoItem));
		}

		if (recordHistory) {
			AddHistory(Folder);
		}
		UpdateFolderUI();
		UpdateFileUI();
		IsLoading = false;
		if (token.IsCancellationRequested) {
			return false;
		}

#if DEBUG
		Trace.WriteLine($"Update UI costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif

		if (fileListBuffer.Count > 0) {
			await Parallel.ForEachAsync(fileListBuffer, token, (item, token) => {
				if (token.IsCancellationRequested) {
					return ValueTask.FromCanceled(token);
				}
				item.LoadAttributes();
				return ValueTask.CompletedTask;
			});
			await Task.Run(() => {
				foreach (var item in fileListBuffer) {
					if (token.IsCancellationRequested) {
						return;
					}
					item.LoadIcon();
				}
			}, token);
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
			return LoadDirectoryAsync(HistoryList[nextHistoryIndex - 1].FullPath, false);
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

	public void OnItemDoubleClicked(object args) {
		var isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
		var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
		var isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
		switch (((ItemClickEventArgs)args).Item) {
		// 双击事件
		case DiskDrive ddi:
			if (isCtrlPressed) {
				_ = OwnerTabControl.OpenPathInNewTabAsync(ddi.Drive.Name);
			} else if (isShiftPressed) {
				new MainWindow(ddi.Drive.Name).Show();
			} else if (isAltPressed) {
				Shell32Interop.ShowFileProperties(ddi.Drive.Name);
			} else {
				_ = LoadDirectoryAsync(ddi.Drive.Name);
			}
			break;
		case FileSystemItem fsi:
			if (isAltPressed) {
				Shell32Interop.ShowFileProperties(fsi.FullPath);
			} else {
				if (fsi.IsFolder) {
					if (isCtrlPressed) {
						_ = OwnerTabControl.OpenPathInNewTabAsync(fsi.FullPath);
					} else if (isShiftPressed) {
						new MainWindow(fsi.FullPath).Show();
					} else {
						_ = LoadDirectoryAsync(fsi.FullPath);
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
		foreach (FileItem addedItem in e.AddedItems) {
			SelectedItems.Add(addedItem);
			addedItem.IsSelected = true;
		}
		foreach (FileItem removedItem in e.RemovedItems) {
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
		if (searchText == null) {
			_ = LoadDirectoryAsync(FullPath);
		}
		if (EverythingInterop.IsAvailable) {
			Items.Clear();  // 清空文件列表，进入搜索模式

			PathType = PathType.Search;
			FileView.CommitChange();
			// EverythingInterop.Reset();
			EverythingInterop.Search = PathType == PathType.Normal ? FullPath + ' ' + searchText : searchText;
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
						list.Add(new FileSystemItem(new DirectoryInfo(fullPath)));
					} else if (File.Exists(fullPath)) {
						list.Add(new FileSystemItem(new FileInfo(fullPath)));
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

		foreach (var item in list) {
			Items.Add(item);
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
		if (PathType == PathType.Home) {
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
						await Task.Run(item.LoadIcon);
					}
					return;
				}
			}
		});
	}

	private void Watcher_OnDeleted(object sender, FileSystemEventArgs e) {
		if (PathType == PathType.Home) {
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
		if (PathType == PathType.Home) {
			return;
		}
		dispatcher.Invoke(async () => {
			if (Items.Any(i => i.Name == e.Name)) {
				return;
			}
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
				await Task.Run(item.LoadIcon);
			}
		});
	}

	private void Watcher_OnChanged(object sender, FileSystemEventArgs e) {
		if (PathType == PathType.Home) {
			return;
		}
		dispatcher.Invoke(async () => {
			// ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
			foreach (FileSystemItem item in Items) {
				if (item.FullPath == e.FullPath) {
					await Task.Run(item.Refresh);
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