// #define EFCore

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ExplorerEx.Converter;
using ExplorerEx.Database.Interface;
using ExplorerEx.Database.Shared;
using ExplorerEx.Utils;
using Microsoft.EntityFrameworkCore;
using static ExplorerEx.Utils.IconHelper;
using Task = System.Threading.Tasks.Task;

namespace ExplorerEx.Model;

/// <summary>
/// 书签分类
/// </summary>
[Serializable]
[DbTable(TableName = "BookmarkCategoryDbSet")]
public class BookmarkCategory : NotifyPropertyChangedBase {
	[DbColumn(IsPrimaryKey = true)]
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

	[DbColumn(nameof(BookmarkItem.CategoryForeignKey), DbNavigateType.OneToMany)]
	public virtual ObservableCollection<BookmarkItem>? Children { get; set; }

	public BookmarkCategory() { }

	public BookmarkCategory(string name) {
		Name = name;
	}

	public void AddBookmark(BookmarkItem item) {
		Children = new ObservableCollection<BookmarkItem>();
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

	[DbColumn]
	public virtual string CategoryForeignKey { get; set; } = null!;

	/// <summary>
	/// 通过自己的外键来找
	/// </summary>
	[DbColumn(nameof(CategoryForeignKey), DbNavigateType.OneToOne)]
	public virtual BookmarkCategory Category { get; set; } = null!;

	public BookmarkItem() : base(null!, null!, false) { }

	public BookmarkItem(string fullPath, string name, BookmarkCategory category) : base(null!, null!, false) {
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


//public static class BookmarkManager {
//	public static DbCollection<BookmarkCategory> BookmarkCategories { get; }
//	public static DbCollection<BookmarkItem> BookmarkItems { get; }

//	private static readonly IDatabase Database;

//	public static Task LoadDataBase() {
//		return Database.LoadAsync();
//	}

//	#region EF Core

//	static BookmarkManager() {
//		var dbContext = new EfCoreBookmarksDbContext();
//		Database = dbContext;
//		BookmarkCategories = new EfCoreDbCollection<BookmarkCategory>(Database, dbContext.BookmarkCategoryDbSet);
//		BookmarkItems = new EfCoreDbCollection<BookmarkItem>(Database, dbContext.BookmarkDbSet);
//	}

//	/// <summary>
//	/// 对接到ef core
//	/// </summary>
//	/// <typeparam name="TEntity"></typeparam>
//	public class EfCoreDbCollection<TEntity> : DbCollection<TEntity> where TEntity : class {
//		private readonly DbSet<TEntity> dbSet;

//		public EfCoreDbCollection(IDatabase database, DbSet<TEntity> dbSet) : base(database) {
//			this.dbSet = dbSet;
//		}

//		private void DbSet_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
//			if (e.Action is not NotifyCollectionChangedAction.Replace and not NotifyCollectionChangedAction.Move) {
//				OnPropertyChanged(nameof(Count));
//			}
//			OnCollectionChanged(e);
//		}

//		public override void Add(TEntity item) {
//			dbSet.Local.Add(item);
//		}

//		public override void Clear() {
//			dbSet.Local.Clear();
//		}

//		public override bool Contains(TEntity item) {
//			return dbSet.Local.Contains(item);
//		}

//		public override void CopyTo(TEntity[] array, int arrayIndex) {
//			dbSet.Local.CopyTo(array, arrayIndex);
//		}

//		public override bool Remove(TEntity item) {
//			return dbSet.Local.Remove(item);
//		}

//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		public override Task LoadAsync() {
//			dbSet.Local.ToObservableCollection().CollectionChanged += DbSet_OnCollectionChanged;
//			return dbSet.LoadAsync();
//		}

//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		public override Task SaveChangesAsync() {
//			return database.SaveAsync();
//		}

//		public override IEnumerator<TEntity> GetEnumerator() {
//			return dbSet.Local.GetEnumerator();
//		}

//		public override int Count => dbSet.Local.Count;

//		public override bool IsReadOnly => false;
//	}

//	private class EfCoreBookmarksDbContext : DbContext, IDatabase {
//		public DbSet<BookmarkCategory> BookmarkCategoryDbSet { get; set; } = null!;
//		public DbSet<BookmarkItem> BookmarkDbSet { get; set; } = null!;

//		private readonly string dbPath;

//		public EfCoreBookmarksDbContext() {
//			var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data");
//			if (!Directory.Exists(path)) {
//				Directory.CreateDirectory(path);
//			}
//			dbPath = Path.Combine(path, "BookMarks.db");
//		}

//		protected override void OnConfiguring(DbContextOptionsBuilder ob) {
//			ob.UseSqlite($"Data Source={dbPath}");
//		}

//		protected override void OnModelCreating(ModelBuilder modelBuilder) {
//			modelBuilder.Entity<BookmarkItem>().HasOne(b => b.Category)
//				.WithMany(cb => cb.Children).HasForeignKey(b => b.CategoryForeignKey);
//		}

//		public async Task LoadAsync() {
//			try {
//				await Database.EnsureCreatedAsync();
//				await BookmarkCategoryDbSet.LoadAsync();
//				await BookmarkDbSet.LoadAsync();
//				if (BookmarkCategories.Count == 0) {
//					var defaultCategory = new BookmarkCategory("DefaultBookmark".L());
//					BookmarkCategories.Add(defaultCategory);
//					BookmarkItems.Add(new BookmarkItem(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents".L(), defaultCategory));
//					BookmarkItems.Add(new BookmarkItem(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Desktop".L(), defaultCategory));
//					await BookmarkCategories.SaveChangesAsync();
//					await BookmarkItems.SaveChangesAsync();
//				}
//			} catch (Exception e) {
//				MessageBox.Show("无法加载数据库，可能是权限不够或者数据库版本过旧，请删除Data文件夹后再试一次。\n错误为：" + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
//				Logger.Exception(e, false);
//			}
//		}

//		public void Save() {
//			throw new NotImplementedException();
//		}

//		[MethodImpl(MethodImplOptions.AggressiveInlining)]
//		public Task SaveAsync() {
//			return ((DbContext)this).SaveChangesAsync();
//		}
//	}


//	#endregion
//}