using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ExplorerEx.Converter;
using ExplorerEx.Utils;
using Microsoft.EntityFrameworkCore;
using static ExplorerEx.Model.FileListViewItem;
using static ExplorerEx.Utils.IconHelper;

namespace ExplorerEx.Model;

/// <summary>
/// 书签分类
/// </summary>
[Serializable]
public class BookmarkCategory : NotifyPropertyChangedBase {
	[Key] 
	public string Name { get; set; } = null!;

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
public class BookmarkItem : FileListViewItem, IFilterable {
	public override string DisplayText => Name;

	public string CategoryForeignKey { get; set; } = null!;

	public BookmarkCategory Category { get; set; } = null!;

	public BookmarkItem() : base(null!, null!) { }

	public BookmarkItem(string fullPath, string name, BookmarkCategory category) : base(null!, null!) {
		FullPath = Path.GetFullPath(fullPath);
		Name = name;
		Category = category;
		category.AddBookmark(this);
	}

	public override void LoadAttributes(LoadDetailsOptions options) {
		throw new InvalidOperationException();
	}

	public override void LoadIcon(LoadDetailsOptions options) {
		if (FullPath.Length == 3) {
			IsFolder = true;
			Icon = GetPathThumbnail(FullPath);
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

public class BookmarkDbContext : DbContext {
	public static BookmarkDbContext Instance { get; } = new();
	public static ObservableCollection<BookmarkCategory> BookmarkCategories { get; set; } = null!;
	public DbSet<BookmarkCategory> BookmarkCategoryDbSet { get; set; } = null!;
	public DbSet<BookmarkItem> BookmarkDbSet { get; set; } = null!;

	private readonly string dbPath;

	private BookmarkDbContext() {
		var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data");
		if (!Directory.Exists(path)) {
			Directory.CreateDirectory(path);
		}
		dbPath = Path.Combine(path, "BookMarks.db");
	}

	public async Task LoadDataBase() {
		try {
			await Database.EnsureCreatedAsync();
			await BookmarkCategoryDbSet.LoadAsync();
			await BookmarkDbSet.LoadAsync();

			BookmarkCategories = BookmarkCategoryDbSet.Local.ToObservableCollection();
			if (BookmarkCategories.Count == 0) {
				var defaultCategory = new BookmarkCategory("Default_bookmark".L());
				await BookmarkCategoryDbSet.AddAsync(defaultCategory);
				await BookmarkDbSet.AddRangeAsync(
					new BookmarkItem(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents".L(), defaultCategory),
					new BookmarkItem(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Desktop".L(), defaultCategory));
				await SaveChangesAsync();
			}
			await Task.Run(() => {
				foreach (var item in BookmarkDbSet.Local) {
					item.LoadIcon(LoadDetailsOptions.Default);
				}
			});
		} catch (Exception e) {
			MessageBox.Show("无法加载数据库，可能是权限不够或者数据库版本过旧，请删除Data文件夹后再试一次。\n错误为：" + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
			Logger.Exception(e, false);
		}
	}

	protected override void OnConfiguring(DbContextOptionsBuilder ob) {
		ob.UseSqlite($"Data Source={dbPath}");
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder) {
		modelBuilder.Entity<BookmarkItem>().HasOne(b => b.Category)
			.WithMany(cb => cb.Children).HasForeignKey(b => b.CategoryForeignKey);
	}
}