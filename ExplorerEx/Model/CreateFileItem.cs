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

namespace ExplorerEx.Model;

/// <summary>
/// 新建 一个文件
/// </summary>
public class CreateFileItem : INotifyPropertyChanged {
	public ImageSource Icon { get; protected set; }

	public string Description { get; protected set; }

	/// <summary>
	/// 拓展名，如.txt
	/// </summary>
	public string Extension { get; }

	protected CreateFileItem(string extension, bool createIcon = true) {
		Extension = extension;
		if (createIcon) {
			Task.Run(() => {
				Icon = GetPathIconAsync(extension, true);
				OnPropertyChanged(nameof(Icon));
				Description = GetFileTypeDescription(extension);
				OnPropertyChanged(nameof(Description));
			});
		}
	}

	/// <summary>
	/// 创建该文件，方法会自动枚举文件，防止重名
	/// </summary>
	/// <param name="path">文件夹路径</param>
	/// <returns>创建的文件名</returns>
	public virtual string Create(string path) {
		var newFileNameBase = $"{"New".L()} {Description}";
		var newFileName = newFileNameBase + Extension;
		var sameNameCount = 0;
		// 这里应该不需要使用哈希表，毕竟数量不多，枚举不需要消耗太多时间，节省内存
		var list = Directory.EnumerateFiles(path, $"*{Extension}").Select(path => path[(path.LastIndexOf('\\') + 1)..]).ToArray();
		while (list.Contains(newFileName)) {
			newFileName = $"{newFileNameBase} ({++sameNameCount}){Extension}";
		}
		var fileName = Path.Combine(path, newFileName);
		File.Create(fileName).Dispose();
		return fileName;
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
		var newItems = (string[])Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Discardable\PostSetup\ShellNew")!.GetValue("Classes")!;
		items.Clear();
		items.Add(new CreateFileDirectoryItem());
		items.Add(new CreateFileLinkItem());
		items.Add(new CreateFileItem(".txt"));
		foreach (var item in newItems) {
			if (item[0] == '.') {
				switch (item) {
				case ".txt":
				case ".lnk":
				case ".library-ms":
					continue;
				default:
					items.Add(new CreateFileItem(item));
					break;
				}
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

	public override string Create(string path) {
		var newFolderNameBase = "New_folder".L();
		var newFolderName = newFolderNameBase;
		var sameNameCount = 0;
		// 这里应该不需要使用哈希表，毕竟数量不多，枚举不需要消耗太多时间，节省内存
		var list = Directory.EnumerateFileSystemEntries(path, "*").Select(path => path[(path.LastIndexOf('\\') + 1)..]).ToArray();
		while (list.Contains(newFolderName)) {
			newFolderName = $"{newFolderNameBase} ({++sameNameCount})";
		}
		var folderName = Path.Combine(path, newFolderName);
		Directory.CreateDirectory(folderName);
		return folderName;
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