using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerEx.Utils;
using static ExplorerEx.Utils.IconHelper;

namespace ExplorerEx.Model;

/// <summary>
/// 新建 一个文件
/// </summary>
public class CreateFileItem : SimpleNotifyPropertyChanged {
	public static CreateFileItem NoExtension { get; } = new(string.Empty, false) {
		Description = "File".L()
	};

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
				Icon = GetSmallIcon(extension, true);
				UpdateUI(nameof(Icon));
				Description = FileUtils.GetFileTypeDescription(extension);
				UpdateUI(nameof(Description));
			});
		}
	}

	/// <summary>
	/// 自动枚举文件获取下一个可以创建的文件
	/// </summary>
	/// <param name="path">文件夹路径</param>
	/// <returns>创建的文件名，不包括路径</returns>
	public virtual string GetCreateName(string path) {
		return FileUtils.GetNewFileName(path, $"{"New".L()} {Description}{Extension}");
	}

	public virtual bool Create(string path, string fileName) {
		var filePath = Path.Combine(path, fileName);
		if (File.Exists(filePath)) {
			return false;
		}
		File.Create(filePath).Dispose();
		return true;
	}

	public static ObservableCollection<CreateFileItem> Items {
		get {
			if (items.Count == 0) {
				UpdateItems();
			}
			return items;
		}
	}

	// ReSharper disable once InconsistentNaming
	private static readonly ObservableCollection<CreateFileItem> items = new();

	public static void UpdateItems() {
		var newItems = (string[])Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Discardable\PostSetup\ShellNew")!.GetValue("Classes")!;
		items.Clear();
		items.Add(CreateFolderItem.Singleton);
		// items.Add(new CreateFileLinkItem());
		items.Add(NoExtension);
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
internal class CreateFolderItem : CreateFileItem {
	public static CreateFolderItem Singleton { get; } = new();

	private CreateFolderItem() : base(null, false) {
		Icon = EmptyFolderDrawingImage;
		Description = "Folder".L();
	}

	public override string GetCreateName(string path) {
		return FileUtils.GetNewFileName(path, "New_folder".L());
	}

	public override bool Create(string path, string folderName) {
		var folderPath = Path.Combine(path, folderName);
		if (Directory.Exists(folderPath)) {
			return false;
		}
		Directory.CreateDirectory(folderPath);
		return true;
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