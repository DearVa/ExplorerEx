using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
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

	public bool IsExpanded { get; set; }

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
	public int Id { get; set; }

	public override string FullPath { get; protected set; }

	public override string Type => throw new NotImplementedException();

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
		FullPath = fullPath;
		Name = name;
		Category = category;
		category.AddBookmark(this);
	}

	public override async Task LoadIconAsync() {
		if (FullPath.Length == 3) {
			IsFolder = true;
			Icon = await Task.Run(() => GetPathThumbnailAsync(FullPath));
		} else if (Directory.Exists(FullPath)) {
			IsFolder = true;
			Icon = FolderUtils.IsEmptyFolder(FullPath) ? EmptyFolderDrawingImage : FolderDrawingImage;
		} else if (File.Exists(FullPath)) {
			IsFolder = false;
			Icon = await Task.Run(() => GetPathIconAsync(FullPath, false));
		}
	}

	protected override bool Rename() {
		throw new NotImplementedException();
	}
}

public class BookmarkDbContext : DbContext {
	public static BookmarkDbContext Instance { get; } = new();
	public static ObservableCollection<BookmarkCategory> BookmarkCategories { get; set; }
	public DbSet<BookmarkCategory> BookmarkCategoryDbSet { get; set; }
	public DbSet<BookmarkItem> BookmarkDbSet { get; set; }

	private readonly string dbPath;

	private BookmarkDbContext() {
		var path = Path.Combine(Environment.CurrentDirectory, "Data");
		if (!Directory.Exists(path)) {
			Directory.CreateDirectory(path);
		}
		dbPath = Path.Combine(path, "BookMarks.db");
	}

	public static async Task LoadOrMigrateAsync() {
		try {
			await Instance.Database.EnsureCreatedAsync();
			await Instance.BookmarkCategoryDbSet.LoadAsync();
			await Instance.BookmarkDbSet.LoadAsync();
		} catch {
			await Instance.Database.MigrateAsync();
			await Instance.BookmarkCategoryDbSet.LoadAsync();
			await Instance.BookmarkDbSet.LoadAsync();
		} finally {
			BookmarkCategories = Instance.BookmarkCategoryDbSet.Local.ToObservableCollection();
			if (BookmarkCategories.Count == 0) {
				BookmarkCategories.Add(new BookmarkCategory("Default_bookmark".L()));
				SaveChanges();
			}
			foreach (var item in Instance.BookmarkDbSet.Local) {
				await item.LoadIconAsync();
			}
		}
	}

	public new static void SaveChanges() {
		((DbContext)Instance).SaveChanges();
	}

	protected override void OnConfiguring(DbContextOptionsBuilder ob) {
		ob.UseSqlite($"Data Source={dbPath}");
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder) {
		modelBuilder.Entity<BookmarkItem>().HasOne(b => b.Category)
			.WithMany(cb => cb.Children).HasForeignKey(b => b.CategoryForeignKey);
	}
}