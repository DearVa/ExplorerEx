using System;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Windows;
using ExplorerEx.Database.Interface;
using ExplorerEx.Model;
using ExplorerEx.Utils;

namespace ExplorerEx.Database.SqlSugar;

public class BookmarkSugarContext : IBookmarkDbContext {
	private readonly CachedSugarContext<BookmarkItem> bookmarkCtx;
	private readonly CachedSugarContext<BookmarkCategory> categoryCtx;

	public BookmarkSugarContext() {
		categoryCtx = new CachedSugarContext<BookmarkCategory>("BookMarks.db");
		bookmarkCtx = new CachedSugarContext<BookmarkItem>("BookMarks.db");
		//categorySugarCache = new SugarCache<BookmarkCategory, BookmarkItem>(ConnectionClient,
		//	new SugarStrategy<BookmarkCategory, BookmarkItem>(itemSugarCache, (category, item) => {
		//		if (item.CategoryForeignKey == category.Name) {
		//			item.Category = category;
		//			category.Children?.Add(item);
		//		}
		//	}));
	}

	public async Task LoadAsync() {
		await categoryCtx.LoadAsync();
		await bookmarkCtx.LoadAsync();
		await Task.Run(() => {
			try {
				if (categoryCtx.Count() == 0) {
					var defaultCategory = new BookmarkCategory("DefaultBookmark".L());
					categoryCtx.Add(defaultCategory);
					categoryCtx.Save();
					bookmarkCtx.Add(new BookmarkItem(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents".L(), defaultCategory));
					bookmarkCtx.Add(new BookmarkItem(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Desktop".L(), defaultCategory));
					bookmarkCtx.Save();
				}
			} catch (Exception e) {
				MessageBox.Show("无法加载数据库，可能是权限不够或者数据库版本过旧，请删除Data文件夹后再试一次。\n错误为：" + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
				Logger.Exception(e, false);
			}
		});
	}

	public void Add(BookmarkItem bookmark) => bookmarkCtx.Add(bookmark);

	public void Add(BookmarkCategory category) => categoryCtx.Add(category);

	public bool Contains(BookmarkItem bookmark) => bookmarkCtx.Contains(bookmark);

	public bool Any(Expression<Func<BookmarkItem, bool>> match) => bookmarkCtx.Any(match);

	public BookmarkCategory? FirstOrDefault(Expression<Func<BookmarkCategory, bool>> match) => categoryCtx.FirstOrDefault(match);

	public BookmarkItem? FirstOrDefault(Expression<Func<BookmarkItem, bool>> match) => bookmarkCtx.FirstOrDefault(match);

	public ObservableCollection<BookmarkCategory> GetBindable() => categoryCtx.GetBindable();

	public void Remove(BookmarkItem bookmark) => bookmarkCtx.Remove(bookmark);

	public void Save() {
		categoryCtx.Save();
		bookmarkCtx.Save();
	}

	public Task SaveAsync() => Task.Run(Save);  // TODO: Thread safe???
}