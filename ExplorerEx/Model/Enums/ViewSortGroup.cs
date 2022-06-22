namespace ExplorerEx.Model.Enums;

/// <summary>
/// 用于使用弹出式菜单切换View SortBy GroupBy 的CommandParameter
/// </summary>
public enum ViewSortGroup : short {
	// View
	GiantIcons,
	LargeIcons,
	MediumIcons,
	SmallIcons,
	List,
	Details,
	Tiles,
	Content,
	
	// Sort
	SortByName = 128,
	SortByDateModified,
	SortByType,
	SortByFileSize,

	Ascending = 256,
	Descending,

	// GroupBy
	GroupByNone,
	GroupByName,
	GroupByDateModified,
	GroupByType,
	GroupByFileSize,
}