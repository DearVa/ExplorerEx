using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
	public DataGridColumn TileHomeTemplate { get; set; }
	public DataGridColumn Name { get; set; }
	public DataGridColumn ModificationDate { get; set; }
	public DataGridColumn Type { get; set; }
	public DataGridColumn FileSize { get; set; }
	public DataGridColumn CreationDate { get; set; }
	
	public void Convert(in ObservableCollection<DataGridColumn> columns, FileDataGrid.PathTypes pathType, FileDataGrid.ViewTypes viewType, FileDataGrid.Lists lists) {
		columns.Clear();
		if (pathType == FileDataGrid.PathTypes.Home) {
			columns.Add(TileHomeTemplate);
			return;
		}
		switch (viewType) {
		case FileDataGrid.ViewTypes.Icon:
			columns.Add(IconTemplate);
			break;
		case FileDataGrid.ViewTypes.Tile:
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
			break;
		}
	}
}