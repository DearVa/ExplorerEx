using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerEx.Annotations;
using ExplorerEx.Utils;
using static ExplorerEx.Win32.IconHelper;
using System;
using System.Windows.Input;

namespace ExplorerEx.Model;

/// <summary>
/// 新建 一个文件
/// </summary>
internal class CreateFileItem : INotifyPropertyChanged {
	public ImageSource Icon { get; protected set; }

	public string Description { get; protected set; }

	/// <summary>
	/// 拓展名，如.txt
	/// </summary>
	public string Extension { get; }

	public Command CreateCommand { get; }

	protected CreateFileItem(string extension, bool createIcon = true) {
		CreateCommand = new Command(this);
		Extension = extension;
		if (createIcon) {
			Task.Run(() => {
				var task = GetPathIconAsync(extension, false, false, false);
				if (task.Wait(3000)) {  // 防止被shell卡死
					Icon = task.Result;
					Description = GetFileTypeDescription(extension);
					OnPropertyChanged(nameof(Icon));
					OnPropertyChanged(nameof(Description));
				}
			});
		}
	}

	/// <summary>
	/// 创建该文件，方法会自动枚举文件，防止重名
	/// </summary>
	/// <param name="path">文件夹路径</param>
	protected virtual void Create(string path) {
		var newFileNameBase = $"{"New".L()} {Description}";
		var newFileName = newFileNameBase + Extension;
		var sameNameCount = 0;
		// 这里应该不需要使用哈希表，毕竟数量不多，枚举不需要消耗太多时间，节省内存
		var list = Directory.EnumerateFiles(path, $"*{Extension}").Select(path => path[(path.LastIndexOf('\\') + 1)..]).ToArray();
		while (list.Contains(newFileName)) {
			newFileName = $"{newFileNameBase} ({++sameNameCount}){Extension}";
		}
		File.Create(Path.Combine(path, newFileName)).Dispose();
	}

	public static ObservableCollection<CreateFileItem> Items {
		get {
			UpdateItems();
			return items;
		}
	}

	// ReSharper disable once InconsistentNaming
	private static readonly ObservableCollection<CreateFileItem> items = new();
	
	public static void UpdateItems() {
		var newItems = (string[])Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Discardable\PostSetup\ShellNew")!.GetValue("Classes");
		items.Clear();
		var folderAdded = false;
		foreach (var item in newItems!) {
			if (item[0] == '.') {
				switch (item) {
				case ".library-ms":
					continue;
				case ".lnk":
					if (items.Count == 0) {
						items.Add(new CreateFileLinkItem());
					} else {
						items.Insert(folderAdded ? 1 : 0, new CreateFileLinkItem());
					}
					break;
				default:
					items.Add(new CreateFileItem(item));
					break;
				}
			} else if (item.ToLower() == "folder") {
				items.Insert(0, new CreateFileDirectoryItem());
				folderAdded = true;
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public class Command : ICommand {
		private readonly CreateFileItem item;

		public Command(CreateFileItem item) {
			this.item = item;
		}

		public bool CanExecute(object parameter) => true;

		public void Execute(object parameter) {
			try {
				item.Create((string)parameter);
			} catch (Exception e) {
				HandyControl.Controls.MessageBox.Error(e.Message, "Cannot_create".L());
			}
		}

		public event EventHandler CanExecuteChanged;
	}
}

/// <summary>
/// 新建文件夹
/// </summary>
internal class CreateFileDirectoryItem : CreateFileItem {
	public CreateFileDirectoryItem() : base(null, false) {
		Icon = EmptyFolderDrawingImage;
		Description = "Folder".L();
	}

	protected override void Create(string path) {
		var newFolderNameBase = "New_folder".L();
		var newFolderName = newFolderNameBase;
		var sameNameCount = 0;
		// 这里应该不需要使用哈希表，毕竟数量不多，枚举不需要消耗太多时间，节省内存
		var list = Directory.EnumerateFileSystemEntries(path, "*").Select(path => path[(path.LastIndexOf('\\') + 1)..]).ToArray();
		while (list.Contains(newFolderName)) {
			newFolderName = $"{newFolderNameBase} ({++sameNameCount})";
		}
		Directory.CreateDirectory(Path.Combine(path, newFolderName));
	}
}

/// <summary>
/// 新建快捷方式，弹出提示框
/// </summary>
internal class CreateFileLinkItem : CreateFileItem {
	public CreateFileLinkItem() : base(".lnk") {
		Description = "Shortcut".L();
	}
}