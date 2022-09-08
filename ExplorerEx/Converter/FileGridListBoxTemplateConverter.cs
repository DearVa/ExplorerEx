using ExplorerEx.Model;
using System.Windows;
using ExplorerEx.View.Controls;

namespace ExplorerEx.Converter; 

internal class FileGridListBoxTemplateConverter {
	public FileListView? FileListView { get; set; }
	/// <summary>
	/// 图标模式
	/// </summary>
	public DataTemplate? IconTemplate { get; set; }
	/// <summary>
	/// 列表模式
	/// </summary>
	public DataTemplate? ListTemplate { get; set; }
	/// <summary>
	/// 平铺模式
	/// </summary>
	public DataTemplate? TileTemplate { get; set; }
	/// <summary>
	/// 内容模式
	/// </summary>
	public DataTemplate? ContentTemplate { get; set; }
	/// <summary>
	/// Home的平铺模板
	/// </summary>
	public DataTemplate? TileHomeTemplate { get; set; }

	public DataTemplate? Convert() {
		return FileListView!.FileView.FileViewType switch {
			FileViewType.Icons => IconTemplate,
			FileViewType.List => ListTemplate,
			FileViewType.Tiles when FileListView.FileView.PathType == PathType.Home => TileHomeTemplate,
			FileViewType.Tiles => TileTemplate,
			FileViewType.Content => ContentTemplate,
			_ => null
		};
	}
}