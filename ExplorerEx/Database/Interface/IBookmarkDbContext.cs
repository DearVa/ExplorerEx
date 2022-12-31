using System;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using ExplorerEx.Models;

namespace ExplorerEx.Database.Interface;

public interface IBookmarkDbContext : IDatabase {
	void Add(BookmarkItem bookmark);
	public BookmarkItem? FirstOrDefault(Expression<Func<BookmarkItem, bool>> match);
	void Remove(BookmarkItem bookmark);
	bool Contains(BookmarkItem bookmark);
	bool Any(Expression<Func<BookmarkItem, bool>> match);


	BookmarkCategory? FirstOrDefault(Expression<Func<BookmarkCategory, bool>> match);
	void Add(BookmarkCategory category);
	/// <summary>
	/// 注意，<bold>不会</bold>级联删除子项
	/// </summary>
	/// <param name="category"></param>
	void Remove(BookmarkCategory category);
	ObservableCollection<BookmarkCategory> AsObservableCollection();

	BookmarkCategory QueryBookmarkCategory(string foreignKey);

	BookmarkItem[] QueryBookmarkItems(string foreignKey);
}