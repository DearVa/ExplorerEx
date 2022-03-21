﻿using System;
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
using ExplorerEx.Model;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.View;
using ExplorerEx.View.Controls;
using ExplorerEx.Win32;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using static ExplorerEx.View.Controls.FileGrid;
using hc = HandyControl.Controls;

namespace ExplorerEx.ViewModel;

/// <summary>
/// 对应一个Tab
/// </summary>
public class FileGridViewModel : SimpleNotifyPropertyChanged, IDisposable {
	public FileTabControl OwnerTabControl { get; }

	public MainWindow OwnerWindow => OwnerTabControl.MainWindow;

	public FileGrid FileGrid { get; set; }

	/// <summary>
	/// 当前路径的FileViewBaseItem
	/// </summary>
	public FileViewBaseItem Folder { get; private set; } = Home.Instance;

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
	public ObservableCollection<FileViewBaseItem> Items { get; } = new();

	public ObservableHashSet<FileViewBaseItem> SelectedItems { get; } = new();

	public SimpleCommand SelectionChangedCommand { get; }

	public bool CanGoBack => nextHistoryIndex > 1;

	public SimpleCommand GoBackCommand { get; }

	public bool CanGoForward => nextHistoryIndex < historyCount;

	public SimpleCommand GoForwardCommand { get; }

	/// <summary>
	/// 历史记录
	/// </summary>
	public ObservableCollection<FileViewBaseItem> HistoryList { get; } = new();

	public bool CanGoToUpperLevel => PathType != PathType.Home;

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

	public SimpleCommand FileDropCommand { get; }

	public SimpleCommand ItemDoubleClickedCommand { get; }

	public bool CanPaste {
		get => canPaste;
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

	private readonly FileSystemWatcher watcher = new();

	private readonly Dispatcher dispatcher;

	private CancellationTokenSource cts;

	public FileGridViewModel(FileTabControl ownerTabControl) {
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
		DeleteCommand = new SimpleCommand(_ => Delete());

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

		DataObjectContent.ClipboardChanged += OnClipboardChanged;
	}

	private void OnSwitchView(object e) {
		if (e is MenuItem { CommandParameter: string param } && int.TryParse(param, out var type)) {
			SwitchViewType(type);
		}
	}

	private bool isLastViewTypeUseLargeIcon;
	/// <summary>
	/// 切换视图时，有的要使用大图标，有的要使用小图标，所以要运行一个Task去更改，取消这个来中断Task
	/// </summary>
	private CancellationTokenSource switchIconCts;

	public async void SwitchViewType(int type) {
		switch (type) {
		case 0:  // 大图标
			FileViewType = FileViewType.Icon;
			ItemSize = new Size(120d, 170d);
			break;
		case 1:  // 小图标
			FileViewType = FileViewType.Icon;
			ItemSize = new Size(80d, 170d);
			break;
		case 2:  // 列表，size.Width为0代表横向填充
			FileViewType = FileViewType.List;
			ItemSize = new Size(260d, 30d);
			break;
		case 3:  // 详细信息
			FileViewType = FileViewType.Detail;
			ItemSize = new Size(0d, 30d);
			break;
		case 4:  // 平铺
			FileViewType = FileViewType.Tile;
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
			SortBy = DetailListType.ModificationDate;
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
			GroupBy = DetailListType.ModificationDate;
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
			var useLargeIcon = FileViewType is FileViewType.Icon or FileViewType.Tile or FileViewType.Content;
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

	private void AddHistory(FileViewBaseItem item) {
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

	public async Task<bool> Refresh() {
		return await LoadDirectoryAsync(FullPath, false);
	}

	/// <summary>
	/// 添加单个项目，这将会验证文件是否存在，之后将其添加，这在创建新文件时很有用
	/// </summary>
	/// <param name="name">文件或文件夹名，不包含路径</param>
	/// <returns>成功返回添加的项，失败返回null</returns>
	public FileViewBaseItem AddSingleItem(string name) {
		if (Folder is Home) {
			return null;
		}
		var fullPath = Path.Combine(FullPath, name);
		FileViewBaseItem item;
		if (File.Exists(fullPath)) {
			item = new FileSystemItem(new FileInfo(fullPath)) {
				UseLargeIcon = FileViewType is FileViewType.Icon or FileViewType.Tile or FileViewType.Content
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
		switchIconCts?.Cancel();
		SelectedItems.Clear();
		var isLoadHome = string.IsNullOrWhiteSpace(path) || path == "This_computer".L() || path.ToLower() == Home.Uuid;  // 加载“此电脑”

		if (!isLoadHome && !Directory.Exists(path)) {
			var err = new StringBuilder();
			err.Append("Cannot_open".L()).Append(' ').Append(path).Append('\n').Append("Check_your_input_and_try_again".L());
			hc.MessageBox.Error(err.ToString(), "Error");
			return false;
		}

		cts?.Cancel();
		cts = new CancellationTokenSource();
		var token = cts.Token;

#if DEBUG
		var sw = Stopwatch.StartNew();
#endif

		if (isLoadHome) {
			Folder = Home.Instance;
		} else if (path.Length <= 3) {
			Folder = new DiskDrive(new DriveInfo(path[..1]));
			new Task(Folder.LoadIcon, token).Start();
		} else {
			Folder = new FileSystemItem(new DirectoryInfo(path));
			new Task(Folder.LoadIcon, token).Start();
		}

		UpdateUI(nameof(Folder));
		if (token.IsCancellationRequested) {
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
			if (FileViewType == FileViewType.Detail) {
				DetailLists = savedView.DetailLists;
				ItemSize = new Size(0d, 30d);
			} else {
				ItemSize = savedView.ItemSize;
			}
		} else {
			SortBy = DetailListType.Name;
			IsAscending = true;
			GroupBy = null;
			FileViewType = FileViewType.Detail;  // 没有保存，默认使用详细信息
			DetailLists = null;
			ItemSize = new Size(0d, 30d);
		}
		UpdateUI(nameof(FullPath));
		if (token.IsCancellationRequested) {
			return false;
		}

#if DEBUG
		Trace.WriteLine($"Query Database costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif

		var fileListBuffer = new List<FileViewBaseItem>(128);
		FileViewBaseItem scrollIntoViewItem = null;

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
						scrollIntoViewItem = item;
					}
				}
			}, token);
		} else {
			try {
				watcher.Path = FullPath;
				watcher.EnableRaisingEvents = true;
			} catch {
				hc.MessageBox.Error("Access_denied".L(), "Cannot_access_directory".L());
				if (nextHistoryIndex > 0) {
					await LoadDirectoryAsync(HistoryList[nextHistoryIndex - 1].FullPath, false, path);
				}
				return false;
			}

			var useLargeIcon = FileViewType is FileViewType.Icon or FileViewType.Tile or FileViewType.Content;
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
						scrollIntoViewItem = item;
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
						scrollIntoViewItem = item;
					}
				}
			}, token);
		}
		if (token.IsCancellationRequested) {
			return false;
		}

#if DEBUG
		Trace.WriteLine($"Enumerate {path} costs: {sw.ElapsedMilliseconds}ms");
		sw.Restart();
#endif

		Items.Clear();
		PathType = isLoadHome ? PathType.Home : PathType.Normal;  // TODO: 网络驱动器、OneDrive等
		FileView.CommitChange();  // 一旦调用这个，模板就会改变，所以要在清空之后，不然会导致排版混乱和绑定失败

		if (fileListBuffer.Count > 0) {
			foreach (var fileViewBaseItem in fileListBuffer) {
				Items.Add(fileViewBaseItem);
			}
			if (selectedPath == null) {
				scrollIntoViewItem = fileListBuffer[0];
			}
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
			dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => FileGrid.ScrollIntoView(scrollIntoViewItem));
#pragma warning restore CS4014
		}

		if (recordHistory) {
			AddHistory(Folder);
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
	public async void GoBackAsync() {
		if (CanGoBack) {
			nextHistoryIndex--;
			await LoadDirectoryAsync(HistoryList[nextHistoryIndex - 1].FullPath, false, HistoryList[nextHistoryIndex].FullPath);
		}
	}

	/// <summary>
	/// 前进一页
	/// </summary>
	public async void GoForwardAsync() {
		if (CanGoForward) {
			nextHistoryIndex++;
			await LoadDirectoryAsync(HistoryList[nextHistoryIndex - 1].FullPath, false);
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
		if (PathType == PathType.Home || SelectedItems.Count == 0) {
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
		if (PathType == PathType.Home) {
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
					FileUtils.FileOperation(isCut ? FileOpType.Move : FileOpType.Copy, filePaths, destPaths);
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
		if (PathType == PathType.Home) {
			SelectedItems.First().StartRename();
		} else if (SelectedItems.Count == 1) {
			SelectedItems.First().StartRename();
		} else {
			SelectedItems.First().StartRename();
			// TODO: 批量重命名
		}
	}

	/// <summary>
	/// 删除一个或多个文件，按住shift就是强制删除
	/// </summary>
	public void Delete() {
		if (PathType == PathType.Home || SelectedItems.Count == 0) {
			return;
		}
		if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift) {  // 没有按Shift
			if (!MessageBoxHelper.AskWithDefault("Recycle", "Are_you_sure_to_recycle_these_files?".L())) {
				return;
			}
			try {
				FileUtils.FileOperation(FileOpType.Delete, SelectedItems.Where(item => item is FileSystemItem)
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
		UpdateUI(nameof(CanGoBack));
		UpdateUI(nameof(CanGoForward));
		UpdateUI(nameof(CanGoToUpperLevel));
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
		UpdateUI(nameof(SelectedFileItemsCountVisibility));
		UpdateUI(nameof(SelectedFileItemsCount));
		UpdateUI(nameof(SelectedFileItemsSizeVisibility));
		UpdateUI(nameof(SelectedFileItemsSizeText));
	}

	public async Task Item_OnDoubleClicked(ItemClickEventArgs e) {
		var isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
		var isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
		var isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
		switch (e.Item) {
		// 双击事件
		case DiskDrive ddi:
			if (isCtrlPressed) {
				await OwnerTabControl.OpenPathInNewTabAsync(ddi.Driver.Name);
			} else if (isShiftPressed) {
				new MainWindow(ddi.Driver.Name).Show();
			} else if (isAltPressed) {
				Shell32Interop.ShowFileProperties(ddi.Driver.Name);
			} else {
				await LoadDirectoryAsync(ddi.Driver.Name);
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
	}

	private CancellationTokenSource everythingReplyCts;

	private async void OnEverythingQueryReplied(uint id, EverythingInterop.QueryReply reply) {
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