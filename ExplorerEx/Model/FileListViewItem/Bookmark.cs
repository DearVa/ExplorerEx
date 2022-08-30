using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using ExplorerEx.Converter;
using ExplorerEx.Database.Shared;
using ExplorerEx.Utils;
using static ExplorerEx.Utils.IconHelper;

namespace ExplorerEx.Model;

/// <summary>
/// 书签分类
/// </summary>
[Serializable]
[DbTable(TableName = "BookmarkCategoryDbSet")]
public class BookmarkCategory : NotifyPropertyChangedBase {
	[DbColumn(IsPrimaryKey = true)]
	public virtual string Name { get; set; } = null!;

	/// <summary>
	/// 在Binding里使用
	/// </summary>
	// ReSharper disable once UnusedMember.Global
	public bool IsExpanded {
		get => isExpanded;
		set {
			if (isExpanded != value) {
				isExpanded = value;
				OnPropertyChanged();
			}
		}
	}

	private bool isExpanded;

	public ImageSource Icon => Children is { Count: > 0 } ? FolderDrawingImage : EmptyFolderDrawingImage;

	[DbColumn(nameof(BookmarkItem.CategoryForeignKey), DbNavigateType.OneToMany)]
	public virtual ObservableCollection<BookmarkItem>? Children { get; set; }

	public BookmarkCategory() { }

	public BookmarkCategory(string name) {
		Name = name;
	}

	public void AddBookmark(BookmarkItem item) {
		Children ??= new ObservableCollection<BookmarkItem>();
		Children.Add(item);
		OnPropertyChanged(nameof(Children));
		OnPropertyChanged(nameof(Icon));
	}

	public override string ToString() {
		return Name;
	}
}

/// <summary>
/// 书签项
/// </summary>
[Serializable]
[DbTable(TableName = "BookmarkDbSet")]
public class BookmarkItem : FileListViewItem, IFilterable {
	public override string DisplayText => Name;

	/// <summary>
	/// 不要设置这个的值
	/// </summary>
	[DbColumn]
	public virtual string CategoryForeignKey { get; set; } = null!;

	/// <summary>
	/// 通过自己的外键来找
	/// </summary>
	[DbColumn(nameof(CategoryForeignKey), DbNavigateType.OneToOne)]
	public virtual BookmarkCategory Category { get; set; } = null!;

	public BookmarkItem() { }

	public BookmarkItem(string fullPath, string name, BookmarkCategory category) {
		FullPath = Path.GetFullPath(fullPath);
		Name = name;
		Category = category;
		CategoryForeignKey = category.Name;  // 不管了先设置上
		category.AddBookmark(this);
	}

	public override void LoadAttributes(LoadDetailsOptions options) {
		throw new InvalidOperationException();
	}

	public override void LoadIcon(LoadDetailsOptions options) {
		if (FullPath.Length == 3) {
			IsFolder = true;
			Icon = GetDriveThumbnail(FullPath);
		} else if (Directory.Exists(FullPath)) {
			IsFolder = true;
			Icon = FolderUtils.IsEmptyFolder(FullPath) ? EmptyFolderDrawingImage : FolderDrawingImage;
		} else if (File.Exists(FullPath)) {
			IsFolder = false;
			Icon = GetSmallIcon(FullPath, false);
		} else {
			Icon = MissingFileDrawingImage;
		}
	}

	public override string GetRenameName() {
		throw new InvalidOperationException();
	}

	protected override bool InternalRename(string newName) {
		throw new InvalidOperationException();
	}

	public bool Filter(string filter) {
		return Name.ToLower().Contains(filter);
	}
}