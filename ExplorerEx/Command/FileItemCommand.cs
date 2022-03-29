using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using ExplorerEx.Model;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.View;
using ExplorerEx.View.Controls;
using hc = HandyControl.Controls;

namespace ExplorerEx.Command;

public class FileItemCommand : ICommand {
	public Func<IEnumerable<FileItem>> SelectedItemsProvider { get; set; }

	public Func<FileTabControl> TabControlProvider { get; set; }

	/// <summary>
	/// 当前操作的目录，如果不是常规目录（比如在主页）就设为null
	/// </summary>
	public string CurrentFullPath { get; set; }

	public bool CanExecute(object parameter) => true;

	private ImmutableList<FileItem> Items => SelectedItemsProvider?.Invoke().ToImmutableList() ?? ImmutableList<FileItem>.Empty;
	private ImmutableList<FileItem> Folders => SelectedItemsProvider?.Invoke().Where(i => i.IsFolder).ToImmutableList() ?? ImmutableList<FileItem>.Empty;

	public async void Execute(object param) {
		switch ((string)param) {
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
				var data = new DataObject(DataFormats.FileDrop, items.Where(item => item is FileSystemItem or DiskDrive).Select(item => item.FullPath).ToArray());
				data.SetData("IsCut", (string)param == "Cut");
				Clipboard.SetDataObject(data);
			}
			break;
		}
		case "Paste": {
			if (CurrentFullPath != null && Clipboard.GetDataObject() is DataObject data) {
				if (data.GetData(DataFormats.FileDrop) is string[] filePaths) {
					bool isCut;
					if (data.GetData("IsCut") is bool i) {
						isCut = i;
					} else {
						isCut = false;
					}
					var destPaths = filePaths.Select(path => Path.Combine(CurrentFullPath, Path.GetFileName(path))).ToList();
					try {
						FileUtils.FileOperation(isCut ? FileOpType.Move : FileOpType.Copy, filePaths, destPaths);
					} catch (Exception e) {
						Logger.Exception(e);
					}
				}
			}
			break;
		}
		case "Rename": {
			var items = Items;
			if (items.Count == 1) {
				items[0].StartRename();
			} else {  // TODO: 批量重命名
				items[0].StartRename();
			}
			break;
		}
		case "Delete":  // 删除一个或多个文件，按住shift就是强制删除
			if (CurrentFullPath == null) {
				return;
			}
			if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift) {  // 没有按Shift
				if (!MessageBoxHelper.AskWithDefault("Recycle", "#AreYouSureToRecycleTheseFiles".L())) {
					return;
				}
				try {
					FileUtils.FileOperation(FileOpType.Delete, Items.Where(item => item is FileSystemItem).Select(item => item.FullPath).ToArray());
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
				Shell32Interop.ShowFileProperties(CurrentFullPath);
			} else {
				foreach (var item in items) {
					Shell32Interop.ShowFileProperties(item.FullPath);
				}
			}
			break;
		}
		case "Edit":
			foreach (var item in Items.Where(i => i is FileSystemItem { IsEditable: true })) {
				OpenFileWith(item, Settings.Instance.TextEditor);
			}
			break;
		}
	}

	public void OpenFile(FileItem item, bool runAs) {
		if (item.IsFolder) {
			TabControlProvider.Invoke()?.SelectedTab.LoadDirectoryAsync(item.FullPath);
		} else {
			try {
				var psi = new ProcessStartInfo {
					FileName = item.FullPath,
					UseShellExecute = true
				};
				if (runAs && item is FileSystemItem { IsExecutable: true }) {
					psi.Verb = "runas";
				}
				Process.Start(psi);
			} catch (Exception e) {
				if (e is Win32Exception win32) {
					switch (win32.NativeErrorCode) {
					case 1155:  // 找不到程序打开
						Shell32Interop.ShowOpenAsDialog(item.FullPath);
						return;
					case 1223:  // 操作被用户取消
						return;
					}
				}
				hc.MessageBox.Error(e.Message, "FailedToOpenFile".L());
			}
		}
	}

	/// <summary>
	/// 用某应用打开文件
	/// </summary>
	/// <param name="item"></param>
	/// <param name="app"></param>
	private static void OpenFileWith(FileItem item, string app) {
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

	public event EventHandler CanExecuteChanged;
}