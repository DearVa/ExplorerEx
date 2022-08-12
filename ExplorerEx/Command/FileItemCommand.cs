using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ExplorerEx.Model;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.View;
using ExplorerEx.View.Controls;
using HandyControl.Data;
using hc = HandyControl.Controls;

namespace ExplorerEx.Command;

public class FileItemCommand : ICommand {
	public Func<IEnumerable<FileListViewItem>?> SelectedItemsProvider { get; init; } = null!;

	public Func<FileTabControl?> TabControlProvider { get; init; } = null!;

	/// <summary>
	/// 当前操作的目录
	/// </summary>
	public FolderItem Folder { get; set; }

	public bool CanExecute(object? parameter) => true;

	private ImmutableList<FileListViewItem> Items => SelectedItemsProvider.Invoke()?.ToImmutableList() ?? ImmutableList<FileListViewItem>.Empty;
	private ImmutableList<FileListViewItem> Folders => SelectedItemsProvider.Invoke()?.Where(i => i.IsFolder).ToImmutableList() ?? ImmutableList<FileListViewItem>.Empty;

	public async void Execute(object? param) {
		switch (param) {
		case string str:
			switch (str) {
			case "Open":  // 打开文件
				foreach (var item in Items) {
					OpenFile(item, false);
				}
				break;
			case "RunAs":
				foreach (var item in Items) {
					OpenFile(item, true);
				}
				break;
			case "OpenInNewTab": {
				var tabControl = TabControlProvider.Invoke();
				if (tabControl == null) {
					return;
				}
				foreach (var item in Folders) {
					await tabControl.OpenPathInNewTabAsync(item.FullPath);
				}
				break;
			}
			case "OpenInNewWindow":
				foreach (var item in Folders) {
					new MainWindow(item.FullPath).Show();
				}
				break;
			case "OpenFileLocation": {
				var tabControl = TabControlProvider.Invoke();
				if (tabControl == null) {
					return;
				}
				var items = Items;
				if (items.Count == 0) {
					return;
				}
				await tabControl.SelectedTab.LoadDirectoryAsync(FileUtils.GetFileLocation(items[0].FullPath), true, items[0].FullPath);  // 第一个在选中的标签页打开
				for (var i = 1; i < items.Count; i++) {
					await tabControl.OpenPathInNewTabAsync(FileUtils.GetFileLocation(items[i].FullPath), items[i].FullPath);  // 如果还有，就新建标签页打开
				}
				break;
			}
			case "CopyAsPath": {
				var items = Items;
				if (items.Count == 0) {
					return;
				}
				var sb = new StringBuilder();
				for (var i = 0; i < items.Count - 1; i++) {
					sb.Append('"').Append(items[i].FullPath).Append("\" ");
				}
				sb.Append('"').Append(items[^1].FullPath).Append('"');
				Clipboard.SetText(sb.ToString());
				break;
			}
			case "Copy":
			case "Cut": {
				var items = Items;
				if (items.Count > 0) {
					var data = new DataObject(DataFormats.FileDrop, items.Where(item => item is FileSystemItem or DiskDriveItem).Select(item => item.FullPath).ToArray());
					data.SetData("IsCut", str == "Cut");
					Clipboard.SetDataObject(data);
				}
				break;
			}
			case "Paste": {
				if (Clipboard.GetDataObject() is DataObject data) {
					if (data.GetData(DataFormats.FileDrop) is string[] filePaths) {
						bool isCut;
						if (data.GetData("IsCut") is bool i) {
							isCut = i;
						} else {
							isCut = false;
						}
						var destPaths = filePaths.Select(path => Path.Combine(Folder.FullPath, Path.GetFileName(path))).ToList();
						try {
							await FileUtils.FileOperation(isCut ? FileOpType.Move : FileOpType.Copy, filePaths, destPaths);
							FileTabControl.MouseOverTabControl?.SelectedTab.FileListView.SelectItems(destPaths);
						} catch (Exception e) {
							Logger.Exception(e);
						}
					}
				}
				break;
			}
			case "Rename": {
				var items = Items;
				switch (items.Count) {
				case < 0:
					return;
				case 1:
					FileTabControl.MouseOverTabControl?.SelectedTab.StartRename(items[0]);
					break;
				default: // TODO: 批量重命名
					FileTabControl.MouseOverTabControl?.SelectedTab.StartRename(items[0]);
					break;
				}
				break;
			}
			case "Delete":  // 删除一个或多个文件，按住shift就是强制删除
				if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift) {  // 没有按Shift
					if (!MessageBoxHelper.AskWithDefault("Recycle", "#AreYouSureToRecycleTheseFiles".L())) {
						return;
					}
					try {
						await FileUtils.FileOperation(FileOpType.Delete, Items.Where(item => item is FileSystemItem).Select(item => item.FullPath).ToArray());
					} catch (Exception e) {
						Logger.Exception(e);
					}
				} else {
					if (!MessageBoxHelper.AskWithDefault("Delete", "#AreYouSureToDeleteTheseFilesPermanently".L())) {
						return;
					}
					var failedFiles = new List<string>();
					foreach (var item in Items) {
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
						hc.MessageBox.Error(string.Join('\n', failedFiles), "TheFollowingFilesFailedToDelete".L());
					}
				}
				break;
			case "AddToBookmarks":
				TabControlProvider.Invoke()?.MainWindow.AddToBookmarks(Items.Select(i => i.FullPath).ToArray());
				break;
			case "RemoveFromBookmarks":
				await MainWindow.RemoveFromBookmark(Items.Select(i => i.FullPath).ToArray());
				break;
			case "Properties": {
				var items = Items;
				if (items.Count == 0) {
					Shell32Interop.ShowProperties(Folder);
				} else {
					foreach (var item in items) {
						Shell32Interop.ShowProperties(item);
					}
				}
				break;
			}
			case "Edit":
				foreach (var item in Items.Where(i => i is FileItem { IsEditable: true })) {
					OpenFileWith(item, Settings.Current["Common.DefaultTextEditor"].GetString());
				}
				break;
			case "Unzip":
				foreach (var item in Items.Where(i => i is FileItem { IsZip: true })) {
					ExtractZipWindow.Show(item.FullPath);
				}
				break;
			case "Terminal":
				Terminal.RunTerminal(Folder.FullPath);
				break;
			case "ShowMore": {
				if (!ConfigHelper.LoadBoolean("ShowMore")) {
					hc.MessageBox.Info("#ShowMore".L());
					ConfigHelper.Save("ShowMore", true);
				}
				var items = Items;
				if (items.Count == 0) {
					Shell32Interop.ShowShellContextMenu(Folder.FullPath);
				} else {
					Shell32Interop.ShowShellContextMenu(items.Select(i => i.FullPath).ToArray());
				}
				break;
			}
			}
			break;
		case FileAssocItem fileAssoc:
			fileAssoc.Run(Items[0].FullPath);
			break;
		}
	}

	/// <summary>
	/// 运行或打开一个文件
	/// </summary>
	/// <param name="item"></param>
	/// <param name="runAs">是否以管理员方式执行，仅对可执行文件有效</param>
	public static void OpenFile(FileListViewItem item, bool runAs) {
		switch (item) {
		case FolderItem folder:
			_ = FileTabControl.MouseOverTabControl?.SelectedTab.LoadDirectoryAsync(folder.FullPath);
			break;
		case ZipFileItem zipFile:
			var zipArchive = zipFile.ZipArchive;
			if (zipFile.IsExecutable && zipArchive.Entries.Count > 1) {
				var result = hc.MessageBox.Show(new MessageBoxInfo {
					Caption = "Question".L(),
					Image = MessageBoxImage.Question,
					Message = "#ZipFileIsExecutable".L(),
					Button = MessageBoxButton.YesNoCancel,
					YesButtonText = "ExtractAll".L(),
					NoButtonText = "RunAnyway".L(),
					CancelButtonText = "Cancel".L()
				});
				switch (result) {
				case MessageBoxResult.Yes:
					ExtractZipWindow.Show(zipFile.ZipPath);
					return;
				case MessageBoxResult.No:
					break;
				default:
					return;
				}
			}
			Task.Run(() => {
				var tempPath = FolderUtils.GetRandomFolderInTemp(Path.GetFileName(zipFile.ZipPath));
				OpenFile(zipFile.Extract(tempPath, false), runAs);
			});
			break;
		default:
			OpenFile(item.FullPath, runAs);
			break;
		}
	}

	private static void OpenFile(string filePath, bool runAs) {
		try {
			var psi = new ProcessStartInfo {
				FileName = filePath,
				UseShellExecute = true,
				WorkingDirectory = Path.GetDirectoryName(filePath) ?? string.Empty
			};
			if (runAs && FileUtils.IsExecutable(filePath)) {
				psi.Verb = "runas";
			}
			Process.Start(psi);
		} catch (Exception e) {
			if (e is Win32Exception win32) {
				switch (win32.NativeErrorCode) {
				case 1155:  // 找不到程序打开
					Shell32Interop.ShowOpenAsDialog(filePath);
					return;
				case 1223:  // 操作被用户取消
					return;
				}
			}
			hc.MessageBox.Error(e.Message, "FailedToOpenFile".L());
		}
	}

	/// <summary>
	/// 用某应用打开文件
	/// </summary>
	/// <param name="item"></param>
	/// <param name="app"></param>
	private static void OpenFileWith(FileListViewItem item, string app) {
		try {
			Process.Start(new ProcessStartInfo {
				FileName = app,
				Arguments = item.FullPath,
				UseShellExecute = true
			});
		} catch (Exception e) {
			hc.MessageBox.Error(e.Message, "FailedToOpenFile".L());
		}
	}

	public event EventHandler? CanExecuteChanged;
}