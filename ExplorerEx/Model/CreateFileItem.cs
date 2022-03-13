using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerEx.Utils;
using static ExplorerEx.Win32.IconHelper;

namespace ExplorerEx.Model;

/// <summary>
/// 新建 一个文件
/// </summary>
public class CreateFileItem : SimpleNotifyPropertyChanged {
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
				Icon = GetPathIcon(extension, true);
				PropertyUpdateUI(nameof(Icon));
				Description = GetFileTypeDescription(extension);
				PropertyUpdateUI(nameof(Description));
			});
		}
	}

	/// <summary>
	/// 创建该文件，方法会自动枚举文件，防止重名
	/// </summary>
	/// <param name="path">文件夹路径</param>
	/// <returns>创建的文件名，不包括路径</returns>
	public virtual string Create(string path) {
		var fileName = FileUtils.GetNewFileName(path, $"{"New".L()} {Description}");
		File.Create(Path.Combine(path, fileName)).Dispose();
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
		items.Add(new CreateDirectoryItem());
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
}

/// <summary>
/// 新建文件夹
/// </summary>
internal class CreateDirectoryItem : CreateFileItem {
	public CreateDirectoryItem() : base(null, false) {
		Icon = EmptyFolderDrawingImage;
		Description = "Folder".L();
	}

	public override string Create(string path) {
		var folderName = FileUtils.GetNewFileName(path, "New_folder".L());
		Directory.CreateDirectory(Path.Combine(path, folderName));
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