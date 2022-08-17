using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ExplorerEx.Database.Interface;
using ExplorerEx.Model;
using ExplorerEx.Utils;

namespace ExplorerEx.Database.SqlSugar;

public class BookmarkSugarContext : SugarContext, IBookmarkDbContext {
	private readonly SugarCache<BookmarkItem> itemSugarCache;
	private readonly SugarCache<BookmarkCategory> categorySugarCache;


	public BookmarkSugarContext() : base("BookMarks.db") {
		itemSugarCache = new SugarCache<BookmarkItem>(ConnectionClient);
		categorySugarCache = new SugarCache<BookmarkCategory, BookmarkItem>(ConnectionClient,
			new SugarStrategy<BookmarkCategory, BookmarkItem>(itemSugarCache, (category, item) => {
				if (item.CategoryForeignKey == category.Name) {
					item.Category = category;
					category.Children?.Add(item);
				}
			}));
	}

	public new Task LoadAsync() {
		return Task.Run(() => {
			try {
				ConnectionClient.DbMaintenance.CreateDatabase();
				ConnectionClient.CodeFirst.InitTables<BookmarkItem, BookmarkCategory>();
				itemSugarCache.LoadDatabase();
				categorySugarCache.LoadDatabase();

				if (categorySugarCache.Count() == 0) {
					var defaultCategory = new BookmarkCategory("DefaultBookmark".L());
					categorySugarCache.Add(defaultCategory);
					itemSugarCache.Add(new BookmarkItem(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents".L(), defaultCategory));
					itemSugarCache.Add(new BookmarkItem(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Desktop".L(), defaultCategory));
					itemSugarCache.Save();
					categorySugarCache.Save();
				}
			} catch (Exception e) {
				MessageBox.Show("无法加载数据库，可能是权限不够或者数据库版本过旧，请删除Data文件夹后再试一次。\n错误为：" + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
				Logger.Exception(e, false);
			}
		});
	}

	public void Add(BookmarkItem item) {
		itemSugarCache.Add(item);
	}

	public void Add(BookmarkCategory item) {
		categorySugarCache.Add(item);
	}

	public Task AddAsync(BookmarkItem item) {
		return Task.Run(() => Add(item));
	}

	public Task AddAsync(BookmarkCategory item) {
		return Task.Run(() => Add(item));
	}

	public BookmarkCategory? FindFirstOrDefault(Func<BookmarkCategory, bool> match) {
		return categorySugarCache.Find(match);
	}

	public BookmarkItem? FindLocalItemFirstOrDefault(Func<BookmarkItem, bool> match) {
		return itemSugarCache.Find(match);
	}

	public ObservableCollection<BookmarkCategory> GetBindable() {
		ObservableCollection<BookmarkCategory> ret = new ObservableCollection<BookmarkCategory>();
		categorySugarCache.QueryAll().ForEach(x => { ret.Add(x); });
		return ret;
	}

	public ISet<BookmarkCategory> GetBookmarkCategories() {
		return categorySugarCache.QueryAll().ToHashSet();
	}

	public ISet<BookmarkItem> GetBookmarkItems() {
		return itemSugarCache.QueryAll().ToHashSet();
	}

	public ISet<BookmarkItem> GetLocalBookmarkItems() {
		return itemSugarCache.QueryAll().ToHashSet();
	}


	public void Remove(BookmarkItem item) {
		itemSugarCache.Remove(item);
	}

	public override void Save() {
		itemSugarCache.Save();
		categorySugarCache.Save();
	}

	public override Task SaveAsync() {
		return Task.Run(Save);
	}
}