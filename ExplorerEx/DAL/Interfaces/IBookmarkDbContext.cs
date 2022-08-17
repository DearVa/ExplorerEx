using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExplorerEx.Model;

namespace ExplorerEx.DAL.Interfaces
{
    public interface IBookmarkDbContext : ILazyInitialize , IDbBehavior
    {

        ISet<BookmarkItem> GetBookmarkItems();

        void Add(BookmarkItem item);
        Task AddAsync(BookmarkItem item);

        ISet<BookmarkItem> GetLocalBookmarkItems();
        public BookmarkItem? FindLocalItemFirstOrDefault(Func<BookmarkItem,bool> match);
        void Remove(BookmarkItem item);

        ISet<BookmarkCategory> GetBookmarkCategories();

        BookmarkCategory? FindFirstOrDefault(Func<BookmarkCategory,bool> match);

        void Add(BookmarkCategory item);
        Task AddAsync(BookmarkCategory item);

        ObservableCollection<BookmarkCategory> GetBindable();
    }
}
