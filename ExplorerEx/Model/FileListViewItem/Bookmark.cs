using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using ExplorerEx.Converter;
using ExplorerEx.Database;
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
	public string Name {
		get => name;
		set => SetField(ref name, value);
	}

	private string name = null!;

	/// <summary>
	/// 在Binding里使用
	/// </summary>
	[DbColumn]
	public bool IsExpanded {
		get => isExpanded;
		set => SetField(ref isExpanded, value);
	}

	private bool isExpanded;

	public ImageSource Icon => Children is { Count: > 0 } ? FolderDrawingImage : EmptyFolderDrawingImage;

	public ObservableCollection<BookmarkItem> Children => children ??= new ObservableCollection<BookmarkItem>(DbMain.BookmarkDbContext.QueryBookmarkItems(name));

	private ObservableCollection<BookmarkItem>? children;

	public BookmarkCategory() { }

	public BookmarkCategory(string name) {
		Name = name;
		isExpanded = true;
	}

	public void AddBookmark(BookmarkItem item) {
		Children.Add(item);
		OnPropertyChanged(nameof(Children));
		OnPropertyChanged(nameof(Icon));
	}

	public override string ToString() {
		return Name;
	}

	/// <summary>
	/// 删除
	/// </summary>
	public void Delete() {
		foreach (var bookmarkItem in Children) {
			DbMain.BookmarkDbContext.Remove(bookmarkItem);
		}
		DbMain.BookmarkDbContext.Save();  // TODO
		DbMain.BookmarkDbContext.Remove(this);
		DbMain.BookmarkDbContext.Save();
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
	public string CategoryForeignKey { get; set; } = null!;

	public BookmarkCategory Category {
		get => category ??= DbMain.BookmarkDbContext.QueryBookmarkCategory(CategoryForeignKey);
		set {
			if (category != value) {
				category?.Children.Remove(this);
				CategoryForeignKey = value.Name;
				category = value;
				category.Children.Add(this);
			}
		}
	}

	private BookmarkCategory? category;

	public BookmarkItem() : base(false, LoadDetailsOptions.Default) { }

	public BookmarkItem(string fullPath, string name, BookmarkCategory category) : base(false, LoadDetailsOptions.Default) {
		FullPath = Path.GetFullPath(fullPath);
		Name = name;
		Category = category;
	}

	protected override void LoadAttributes() { }

	protected override void LoadIcon() {
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

	protected override void InternalRename(string newName) {
		throw new InvalidOperationException();
	}

	public bool Filter(string filter) {
		return Name.ToLower().Contains(filter);
	}
}