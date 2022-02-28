using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ExplorerEx.Model;
using ExplorerEx.Utils;

namespace ExplorerEx.Converter;

/// <summary>
/// 根据<see cref="DetailLists"/>选择对应的Columns
/// </summary>
internal class FileDataGridColumnsConverter {
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

	#region 详细信息 列
	public DataTemplate Name { get; set; }
	public DataTemplate ModificationDate { get; set; }
	public DataTemplate Type { get; set; }
	public DataTemplate FileSize { get; set; }
	public DataTemplate CreationDate { get; set; }

	public DataTemplate AvailableSpace { get; set; }
	public DataTemplate TotalSpace { get; set; }
	public DataTemplate FillRatio { get; set; }
	public DataTemplate FileSystem { get; set; }
	#endregion

	private static readonly DataGridLength Auto = new(0, DataGridLengthUnitType.SizeToCells);

	private static void AddColumn(in ObservableCollection<DataGridColumn> columns, DataTemplate template, DataGridLength width, string header = null) {
		columns.Add(new DataGridTemplateColumn {
			CellTemplate = template,
			Width = width,
			Header = header
		});
	}

	public void Convert(in ObservableCollection<DataGridColumn> columns, PathType pathType, FileViewType viewType, IList<DetailList> detailLists) {
		columns.Clear();
		switch (viewType) {
		case FileViewType.Icon:
			AddColumn(columns, IconTemplate, Auto);
			break;
		case FileViewType.List:
			AddColumn(columns, ListTemplate, Auto);
			break;
		case FileViewType.Tile:
			if (pathType == PathType.Home) {
				AddColumn(columns, TileHomeTemplate, Auto);
			} else {
				AddColumn(columns, TileTemplate, Auto);
			}
			break;
		case FileViewType.Content:
			AddColumn(columns, ContentTemplate, Auto);
			break;
		case FileViewType.Detail:
			detailLists ??= DetailList.GetDefaultLists(pathType);  // 如果为null，表示使用默认
			foreach (var (list, width) in detailLists) {
				switch (list) {
				case DetailLists.Name:
					AddColumn(columns, Name, width, "Name".L());
					break;
				case DetailLists.AvailableSpace:
					AddColumn(columns, AvailableSpace, width, "Available_space".L());
					break;
				case DetailLists.TotalSpace:
					AddColumn(columns, TotalSpace, width, "Total_space".L());
					break;
				case DetailLists.FileSystem:
					AddColumn(columns, FileSystem, width, "File_system".L());
					break;
				case DetailLists.FillRatio:
					AddColumn(columns, FillRatio, width, "Fill_ratio".L());
					break;
				case DetailLists.ModificationDate:
					AddColumn(columns, ModificationDate, width, "Modification_date".L());
					break;
				case DetailLists.Type:
					AddColumn(columns, Type, width, "Type".L());
					break;
				case DetailLists.FileSize:
					AddColumn(columns, FileSize, width, "File_size".L());
					break;
				case DetailLists.CreationDate:
					AddColumn(columns, CreationDate, width, "Creation_date".L());
					break;
				}
			}
			break;
		}
	}
}