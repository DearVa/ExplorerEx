using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExplorerEx.Model;

namespace ExplorerEx.DAL.Interfaces
{
    public interface IBookmarkDbContext : ILazyInitialize
    {

        ISet<BookmarkItem> GetBookmarkItems();

        void Add(BookmarkItem item);
        Task AddAsync(BookmarkItem item);

        ISet<BookmarkItem> GetLocalBookmarkItems();

        void Remove(BookmarkItem item);

        ISet<BookmarkCategory> GetBookmarkCategories();

        void Add(BookmarkCategory item);
        Task AddAsync(BookmarkCategory item);

        void SaveChanges();
        Task SaveChangesAsync();

        ObservableCollection<BookmarkCategory> GetBindable();
    }
}
