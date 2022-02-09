using System.Collections.ObjectModel;
using System.Windows.Controls;
using ExplorerEx.View.Controls;

namespace ExplorerEx.Converter;

/// <summary>
/// 根据<see cref="FileDataGrid.Lists"/>选择对应的Columns
/// </summary>
internal class FileDataGridColumnsConverter {
	/// <summary>
	/// 图标模式
	/// </summary>
	public DataGridColumn IconTemplate { get; set; }
	/// <summary>
	/// Home的平铺模板
	/// </summary>
	public DataGridColumn TileHomeTemplate { get; set; }

	#region 详细信息 列
	public DataGridColumn Name { get; set; }
	public DataGridColumn ModificationDate { get; set; }
	public DataGridColumn Type { get; set; }
	public DataGridColumn FileSize { get; set; }
	public DataGridColumn CreationDate { get; set; }

	public DataGridColumn AvailableSpace { get; set; }
	public DataGridColumn TotalSpace { get; set; }
	public DataGridColumn FillRatio { get; set; }
	public DataGridColumn FileSystem { get; set; }
	#endregion

	public void Convert(in ObservableCollection<DataGridColumn> columns, FileDataGrid.PathTypes pathType, FileDataGrid.ViewTypes viewType, FileDataGrid.Lists lists) {
		columns.Clear();
		switch (viewType) {
		case FileDataGrid.ViewTypes.Icon:
			columns.Add(IconTemplate);
			break;
		case FileDataGrid.ViewTypes.Tile:
			if (pathType == FileDataGrid.PathTypes.Home) {
				columns.Add(TileHomeTemplate);
			}
			break;
		case FileDataGrid.ViewTypes.Detail:
			columns.Add(Name);
			if (lists.HasFlag(FileDataGrid.Lists.ModificationDate)) {
				columns.Add(ModificationDate);
			}
			if (lists.HasFlag(FileDataGrid.Lists.Type)) {
				columns.Add(Type);
			}
			if (lists.HasFlag(FileDataGrid.Lists.FileSize)) {
				columns.Add(FileSize);
			}
			if (lists.HasFlag(FileDataGrid.Lists.CreationDate)) {
				columns.Add(CreationDate);
			}
			if (lists.HasFlag(FileDataGrid.Lists.AvailableSpace)) {
				columns.Add(AvailableSpace);
			}
            if (lists.HasFlag(FileDataGrid.Lists.TotalSpace)) {
                columns.Add(TotalSpace);
            }
            if (lists.HasFlag(FileDataGrid.Lists.FillRatio)) {
                columns.Add(FillRatio);
            }
            if (lists.HasFlag(FileDataGrid.Lists.FileSystem)) {
                columns.Add(FileSystem);
            }
			break;
		}
	}
}