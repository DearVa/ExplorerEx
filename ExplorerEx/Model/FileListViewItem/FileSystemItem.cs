using System;
using System.IO;
using System.Windows.Media;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;
using ExplorerEx.View.Controls;
using File = System.IO.File;

namespace ExplorerEx.Model;

public abstract class FileSystemItem : FileListViewItem {
	protected FileSystemInfo? FileSystemInfo { get; }

	/// <summary>
	/// 自动更新UI
	/// </summary>
	public DateTime DateModified {
		get => dateModified;
		protected set {
			if (dateModified != value) {
				dateModified = value;
				OnPropertyChanged();
			}
		}
	}

	private DateTime dateModified = DateTime.MaxValue;

	/// <summary>
	/// 自动更新UI
	/// </summary>
	public DateTime DateCreated {
		get => dateCreated;
		protected set {
			if (dateCreated != value) {
				dateCreated = value;
				OnPropertyChanged();
			}
		}
	}

	private DateTime dateCreated = DateTime.MaxValue;

	public override string GetRenameName() {
		return Name;
	}

	protected override void InternalRename(string newName) {
		var basePath = Path.GetDirectoryName(FullPath);
		if (basePath == null) {
			throw new InvalidOperationException();
		}
		if (Path.GetExtension(FullPath) != Path.GetExtension(newName)) {
			if (!ContentDialog.ShowWithDefault(Settings.CommonSettings.DontAskWhenChangeExtension, "#AreYouSureToChangeExtension".L())) {
				return;
			}
		}
		File.Move(FullPath, Path.Combine(basePath, newName), false);
	}

	/// <summary>
	/// 重新加载图标和详细信息
	/// </summary>
	public void Refresh() {
		LoadIcon();
		LoadAttributes();
	}

	/// <summary>
	/// 用于虚拟文件或者文件夹，继承的类初始化
	/// </summary>
	/// <param name="isFolder"></param>
	protected FileSystemItem(bool isFolder) : base(isFolder, LoadDetailsOptions.Default) { }

	protected FileSystemItem(string fullPath, string name, bool isFolder, LoadDetailsOptions options) : base(fullPath, name, isFolder, options) { }

	protected FileSystemItem(string fullPath, string name, ImageSource defaultIcon, LoadDetailsOptions options) : base(fullPath, name, defaultIcon, options) { }

	protected FileSystemItem(FileSystemInfo fileSystemInfo, bool isFolder, LoadDetailsOptions options) : base(fileSystemInfo.FullName, fileSystemInfo.Name, isFolder, options) {
		FileSystemInfo = fileSystemInfo;
	}

	protected FileSystemItem(FileSystemInfo fileSystemInfo, ImageSource defaultIcon, LoadDetailsOptions options) : base(fileSystemInfo.FullName, fileSystemInfo.Name, defaultIcon, options) {
		FileSystemInfo = fileSystemInfo;
	}
}
