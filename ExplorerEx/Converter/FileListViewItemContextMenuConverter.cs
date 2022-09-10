using System.Windows.Controls;
using ExplorerEx.Model;

namespace ExplorerEx.Converter; 

/// <summary>
/// 根据所选项的类型决定是显示文件夹右键菜单还是文件右键菜单
/// </summary>
internal class FileListViewItemContextMenuConverter {
	public ContextMenu FileContextMenu { get; set; } = null!;

	public ContextMenu FolderContextMenu { get; set; } = null!;

	public ContextMenu DiskDriveContextMenu { get; set; } = null!;

	public ContextMenu Convert(FileListViewItem item) {
		return item switch {
			DiskDriveItem => DiskDriveContextMenu,
			FileSystemItem fs => fs.IsFolder ? FolderContextMenu : FileContextMenu,
			BookmarkItem bm => bm.IsFolder ? FolderContextMenu : FileContextMenu,
			_ => FileContextMenu
		};
	}
}