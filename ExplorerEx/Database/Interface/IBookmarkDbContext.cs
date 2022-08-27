using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ExplorerEx.Model;

namespace ExplorerEx.Database.Interface;

public interface IBookmarkDbContext : IDatabase {
	void Add(BookmarkItem bookmark);
	Task AddAsync(BookmarkItem bookmark);
	public BookmarkItem? FirstOrDefault(Func<BookmarkItem, bool> match);
	void Remove(BookmarkItem bookmark);
	bool Contains(BookmarkItem bookmark);
	bool Any(Func<BookmarkItem, bool> match);


	BookmarkCategory? FirstOrDefault(Func<BookmarkCategory, bool> match);
	void Add(BookmarkCategory category);
	Task AddAsync(BookmarkCategory category);

	ObservableCollection<BookmarkCategory> GetBindable();
}