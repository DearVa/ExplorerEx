using System.Collections.Generic;
using System.Windows.Media;
using ExplorerEx.Shell32;
using ExplorerEx.Win32;
using Microsoft.Win32;

namespace ExplorerEx.Model;

/// <summary>
/// 文件的打开者，比如打开方式中，包含图标、打开命令行等
/// </summary>
internal class FileOpener {
	public string Path { get; }

	public ImageSource Icon { get; }

	public string OpenCommand { get; }

	public List<FileOpener> OpenWithList { get; } = new();

	private FileOpener(string extension) {
		var defineReg = Registry.ClassesRoot.OpenSubKey(extension);
		if (defineReg == null) {
			Icon = IconHelper.UnknownFileDrawingImage;
			return;
		}
		if (defineReg.OpenSubKey("OpenWithList") is { SubKeyCount: > 0 } owl) {
			foreach (var subKeyName in owl.GetSubKeyNames()) {
				OpenWithList.Add(CreateByName(subKeyName));
			}
		}
	}

	private FileOpener CreateByName(string openWithName) {
		return null;
	}

	/// <summary>
	/// 所有的打开者，可以通过文件拓展名来查找
	/// </summary>
	private static readonly Dictionary<string, FileOpener> FileOpeners = new();

	public static FileOpener GetOpener(string extension) {
		if (!FileOpeners.TryGetValue(extension, out var fileOpener)) {
			fileOpener = new FileOpener(extension);
			FileOpeners.Add(extension, fileOpener);
		}
		return fileOpener;
	}
}