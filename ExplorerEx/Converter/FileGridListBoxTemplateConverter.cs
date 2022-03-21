using ExplorerEx.Model;
using System.Windows;
using ExplorerEx.View.Controls;

namespace ExplorerEx.Converter; 

internal class FileGridListBoxTemplateConverter {
	public FileGrid FileGrid { get; set; }
	/// <summary>
	/// 图标模式
	/// </summary>
	public DataTemplate IconTemplate { get; set; }
	/// <summary>
	/// 列表模式
	/// </summary>
	public DataTemplate ListTemplate { get; set; }
	/// <summary>
	/// 平铺模式
	/// </summary>
	public DataTemplate TileTemplate { get; set; }
	/// <summary>
	/// 内容模式
	/// </summary>
	public DataTemplate ContentTemplate { get; set; }
	/// <summary>
	/// Home的平铺模板
	/// </summary>
	public DataTemplate TileHomeTemplate { get; set; }

	public DataTemplate Convert() {
		return FileGrid.FileView.FileViewType switch {
			FileViewType.Icon => IconTemplate,
			FileViewType.List => ListTemplate,
			FileViewType.Tile when FileGrid.FileView.PathType == PathType.Home => TileHomeTemplate,
			FileViewType.Tile => TileTemplate,
			FileViewType.Content => ContentTemplate,
			_ => null
		};
	}
}