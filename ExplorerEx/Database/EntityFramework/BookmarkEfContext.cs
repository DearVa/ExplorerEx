using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using ExplorerEx.Database.Interface;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using Microsoft.EntityFrameworkCore;

namespace ExplorerEx.Database.EntityFramework;

public class BookmarkEfContext : DbContext, IBookmarkDbContext {
	#region  Fields

	private DbSet<BookmarkCategory> BookmarkCategoryDbSet { get; set; } = null!;
	private DbSet<BookmarkItem> BookmarkDbSet { get; set; } = null!;

	private readonly string dbPath;
	#endregion


	public BookmarkEfContext() {
		var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data");
		if (!Directory.Exists(path)) {
			Directory.CreateDirectory(path);
		}
		dbPath = Path.Combine(path, "BookMarks.db");
	}

	protected override void OnConfiguring(DbContextOptionsBuilder ob) {
		ob.UseSqlite($"Data Source={dbPath}");
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder) {
		modelBuilder.Entity<BookmarkItem>().HasOne(b => b.Category)
			.WithMany(cb => cb.Children).HasForeignKey(b => b.CategoryForeignKey);
	}

	#region Interfaces

	public async Task LoadAsync() {
		try {
			await Database.EnsureCreatedAsync();
			await BookmarkCategoryDbSet.LoadAsync();
			await BookmarkDbSet.LoadAsync();

			if (BookmarkCategoryDbSet.Local.Count == 0) {
				var defaultCategory = new BookmarkCategory("DefaultBookmark".L());
				await BookmarkCategoryDbSet.AddAsync(defaultCategory);
				await BookmarkDbSet.AddRangeAsync(
					new BookmarkItem(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
						"Documents".L(),
						defaultCategory),
					new BookmarkItem(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
						"Desktop".L(),
						defaultCategory));
				await SaveAsync();
			}
			await Task.Run(() => {
				foreach (var item in BookmarkDbSet.Local) {
					item.LoadIcon(FileListViewItem.LoadDetailsOptions.Current);
				}
			});
		} catch (Exception e) {
			MessageBox.Show("无法加载数据库，可能是权限不够或者数据库版本过旧，请删除Data文件夹后再试一次。\n错误为：" + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
			Logger.Exception(e, false);
		}
	}

	public void Save() => base.SaveChanges();

	public Task SaveAsync() => base.SaveChangesAsync();

	public void Add(BookmarkItem bookmark) {
		BookmarkDbSet.Add(bookmark);
	}

	public void Add(BookmarkCategory category) {
		BookmarkCategoryDbSet.Add(category);
	}

	public void Remove(BookmarkItem bookmark) => base.Remove(bookmark);

	public bool Contains(BookmarkItem bookmark) => BookmarkDbSet.Contains(bookmark);

	public bool Any(Expression<Func<BookmarkItem, bool>> match) => BookmarkDbSet.Any(match);

	public ObservableCollection<BookmarkCategory> GetBindable() => BookmarkCategoryDbSet.Local.ToObservableCollection();
	
	public BookmarkCategory? FirstOrDefault(Expression<Func<BookmarkCategory, bool>> match) => BookmarkCategoryDbSet.FirstOrDefault(match);

	public BookmarkItem? FirstOrDefault(Expression<Func<BookmarkItem, bool>> match) => BookmarkDbSet.FirstOrDefault(match);
	
	#endregion
}