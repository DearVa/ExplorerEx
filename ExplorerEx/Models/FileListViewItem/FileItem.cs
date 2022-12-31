using System.IO;
using ExplorerEx.Utils;

namespace ExplorerEx.Models;

public class FileItem : FileSystemItem {
	/// <summary>
	/// 是否是可执行文件
	/// </summary>
	public bool IsExecutable => FileUtils.IsExecutable(FullPath);

	/// <summary>
	/// 是否为文本文件
	/// </summary>
	public bool IsEditable => FileUtils.IsEditable(FullPath);

	public bool IsZip => FullPath[^4..] == ".zip";

	/// <summary>
	/// 是否为.lnk文件
	/// </summary>
	public bool IsLink => FullPath[^4..] == ".lnk" || FileSystemInfo is FileInfo { LinkTarget: { } };

	public override string DisplayText => Name;

	protected FileItem() : base(false) { }

	public FileItem(FileInfo fileInfo, LoadDetailsOptions options) : base(fileInfo, false, options) {
		FileSize = -1;
	}

	protected override void LoadAttributes() {
		if (FileSystemInfo == null) {
			return;
		}
		FileSystemInfo.Refresh();
		var type = FileUtils.GetFileTypeDescription(FileSystemInfo.Extension);
		if (string.IsNullOrEmpty(type)) {
			Type = "UnknownType".L();
		} else {
			Type = type;
		}
		FileSize = ((FileInfo)FileSystemInfo).Length;
		DateModified = FileSystemInfo.LastWriteTime;
		DateCreated = FileSystemInfo.CreationTime;
		if (FileSystemInfo.Attributes.HasFlag(FileAttributes.Hidden)) {
			Opacity = 0.5d;
		}
	}

	protected override void LoadIcon() {
		if (Options.UseLargeIcon) {
			Icon = IconHelper.GetPathThumbnail(FullPath);
		} else {
			Icon = IconHelper.GetSmallIcon(FullPath, false);
		}
	}
}