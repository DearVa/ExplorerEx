using System;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using ExplorerEx.Model;

namespace ExplorerEx.Database.Interface;

public interface IBookmarkDbContext : IDatabase {
	void Add(BookmarkItem bookmark);
	public BookmarkItem? FirstOrDefault(Expression<Func<BookmarkItem, bool>> match);
	void Remove(BookmarkItem bookmark);
	bool Contains(BookmarkItem bookmark);
	bool Any(Expression<Func<BookmarkItem, bool>> match);


	BookmarkCategory? FirstOrDefault(Expression<Func<BookmarkCategory, bool>> match);
	void Add(BookmarkCategory category);

	ObservableCollection<BookmarkCategory> GetBindable();
}