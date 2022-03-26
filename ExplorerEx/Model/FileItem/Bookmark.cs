using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ExplorerEx.Converter;
using ExplorerEx.Utils;
using Microsoft.EntityFrameworkCore;
using static ExplorerEx.Shell32.IconHelper;

namespace ExplorerEx.Model;

/// <summary>
/// 书签分类
/// </summary>
[Serializable]
public class BookmarkCategory : SimpleNotifyPropertyChanged {
	[Key]
	public string Name { get; set; }

	/// <summary>
	/// 在Binding里使用
	/// </summary>
	// ReSharper disable once UnusedMember.Global
	public bool IsExpanded {
		get => isExpanded;
		set {
			if (isExpanded != value) {
				isExpanded = value;
				UpdateUI();
			}
		}
	}

	private bool isExpanded;

	public ImageSource Icon => Children is { Count: > 0 } ? FolderDrawingImage : EmptyFolderDrawingImage;

	public virtual ObservableCollection<BookmarkItem> Children { get; set; }

	public BookmarkCategory() { }

	public BookmarkCategory(string name) {
		Name = name;
	}

	public void AddBookmark(BookmarkItem item) {
		Children ??= new ObservableCollection<BookmarkItem>();
		Children.Add(item);
		UpdateUI(nameof(Children));
		UpdateUI(nameof(Icon));
	}

	public override string ToString() {
		return Name;
	}
}

/// <summary>
/// 书签项
/// </summary>
[Serializable]
public class BookmarkItem : FileItem, IFilterable {
	[Key]
	public override string FullPath { get; protected set; }

	public override string DisplayText => Name;

	public string CategoryForeignKey { get; set; }

	public BookmarkCategory Category { get; set; }

	public BookmarkItem() { }

	public BookmarkItem(string fullPath, string name, BookmarkCategory category) {
		// ReSharper disable once VirtualMemberCallInConstructor
		FullPath = Path.GetFullPath(fullPath);
		Name = name;
		Category = category;
		category.AddBookmark(this);
	}

	public override void LoadAttributes() {
		throw new InvalidOperationException();
	}

	public override void LoadIcon() {
		if (FullPath.Length == 3) {
			IsFolder = true;
			Icon = GetDriveThumbnail(new DriveInfo(FullPath[..1]));
		} else if (Directory.Exists(FullPath)) {
			IsFolder = true;
			Icon = FolderUtils.IsEmptyFolder(FullPath) ? EmptyFolderDrawingImage : FolderDrawingImage;
		} else if (File.Exists(FullPath)) {
			IsFolder = false;
			Icon = GetPathIcon(FullPath, false);
		} else {
			Icon = MissingFileDrawingImage;
		}
	}

	public override void StartRename() {
		throw new InvalidOperationException();
	}

	protected override bool Rename() {
		throw new InvalidOperationException();
	}

	public bool Filter(string filter) {
		return Name.Contains(filter);
	}
}

public class BookmarkDbContext : DbContext {
#pragma warning disable CS0612
	public static BookmarkDbContext Instance { get; } = new();
#pragma warning restore CS0612
	public static ObservableCollection<BookmarkCategory> BookmarkCategories { get; set; }
	public DbSet<BookmarkCategory> BookmarkCategoryDbSet { get; set; }
	public DbSet<BookmarkItem> BookmarkDbSet { get; set; }

	private readonly string dbPath;

	/// <summary>
	/// 之所以用public是因为需要迁移，但是*请勿*使用该构造方法，应该使用Instance
	/// </summary>
#pragma warning disable CA1041
	[Obsolete]
#pragma warning restore CA1041
	public BookmarkDbContext() {
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
					item.LoadIcon();
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