using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ExplorerEx.Models;

namespace ExplorerEx.Converter;

/// <summary>
/// 根据<see cref="DetailListType"/>选择对应的Columns
/// </summary>
internal class FileGridDataGridColumnsConverter {
	#region 详细信息 列
	public DataTemplate? Name { get; set; }
	public DataTemplate? DateModified { get; set; }
	public DataTemplate? Type { get; set; }
	public DataTemplate? FileSize { get; set; }
	public DataTemplate? CreationDate { get; set; }

	public DataTemplate? AvailableSpace { get; set; }
	public DataTemplate? TotalSpace { get; set; }
	public DataTemplate? FillRatio { get; set; }
	public DataTemplate? FileSystem { get; set; }

	public DataTemplate? FullPath { get; set; }
	#endregion

	private static void AddColumn(in GridViewColumnCollection columns, DataTemplate? template, double width, string? header = null) {
		columns.Add(new GridViewColumn {
			CellTemplate = template,
			Width = width,
			Header = header
		});
	}

	public void Convert(in GridViewColumnCollection columns, FileView fileView) {
		columns.Clear();
		IList<DetailList>? detailLists = fileView.DetailLists;
		detailLists ??= DetailList.GetDefaultLists(fileView.PathType);  // 如果为null，表示使用默认
		foreach (var (list, width) in detailLists) {
			switch (list) {
			case DetailListType.Name:
				AddColumn(columns, Name, width, Strings.Resources.Name);
				break;
			case DetailListType.AvailableSpace:
				AddColumn(columns, AvailableSpace, width, Strings.Resources.AvailableSpace);
				break;
			case DetailListType.TotalSpace:
				AddColumn(columns, TotalSpace, width, Strings.Resources.TotalSpace);
				break;
			case DetailListType.FileSystem:
				AddColumn(columns, FileSystem, width, Strings.Resources.FileSystem);
				break;
			case DetailListType.FillRatio:
				AddColumn(columns, FillRatio, width, Strings.Resources.FillRatio);
				break;
			case DetailListType.FullPath:
				AddColumn(columns, FullPath, width, Strings.Resources.FullPath);
				break;
			case DetailListType.DateModified:
				AddColumn(columns, DateModified, width, Strings.Resources.DateModified);
				break;
			case DetailListType.Type:
				AddColumn(columns, Type, width, Strings.Resources.Type);
				break;
			case DetailListType.FileSize:
				AddColumn(columns, FileSize, width, Strings.Resources.FileSize);
				break;
			case DetailListType.DateCreated:
				AddColumn(columns, CreationDate, width, Strings.Resources.DateCreated);
				break;
			}
		}
	}
}