using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerEx.Utils;
using ExplorerEx.Win32;
using Microsoft.EntityFrameworkCore;
using static ExplorerEx.Win32.IconHelper;

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
				PropertyUpdateUI();
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
		PropertyUpdateUI(nameof(Children));
		PropertyUpdateUI(nameof(Icon));
	}

	public override string ToString() {
		return Name;
	}
}

/// <summary>
/// 书签项
/// </summary>
[Serializable]
public class BookmarkItem : FileViewBaseItem {
	[Key]
	public override string FullPath { get; protected set; }

	public override string Type => throw new InvalidOperationException();

	[NotMapped]
	public bool IsExpanded { get; set; }

	public string CategoryForeignKey { get; set; }

	public BookmarkCategory Category { get; set; }

	public SimpleCommand MouseLeftButtonDownCommand { get; }

	public BookmarkItem() {
		MouseLeftButtonDownCommand = new SimpleCommand(OnMouseLeftButtonDown);
	}

	private DateTimeOffset lastClickTime;

	private void OnMouseLeftButtonDown(object args) {
		if (lastClickTime.Add(TimeSpan.FromMilliseconds(Win32Interop.GetDoubleClickTime())) >= DateTimeOffset.Now) {
			OpenCommand.Execute(null);
		}
		lastClickTime = DateTimeOffset.Now;
	}

	public BookmarkItem(string fullPath, string name, BookmarkCategory category) : this() {
		// ReSharper disable once VirtualMemberCallInConstructor
		FullPath = Path.GetFullPath(fullPath);
		Name = name;
		Category = category;
		category.AddBookmark(this);
	}

	public override void LoadIcon() {
		if (FullPath.Length == 3) {
			IsFolder = true;
			Icon = GetPathThumbnail(FullPath);
		} else if (Directory.Exists(FullPath)) {
			IsFolder = true;
			Icon = FolderUtils.IsEmptyFolder(FullPath) ? EmptyFolderDrawingImage : FolderDrawingImage;
		} else if (File.Exists(FullPath)) {
			IsFolder = false;
			Icon = GetPathIcon(FullPath, false);
		}
	}

	protected override bool Rename() {
		throw new NotImplementedException();
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

	public async Task LoadOrMigrateAsync() {
		try {
			await Database.EnsureCreatedAsync();
			await BookmarkCategoryDbSet.LoadAsync();
			await BookmarkDbSet.LoadAsync();
		} catch {
			await Database.MigrateAsync();
			await BookmarkCategoryDbSet.LoadAsync();
			await BookmarkDbSet.LoadAsync();
		} finally {
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