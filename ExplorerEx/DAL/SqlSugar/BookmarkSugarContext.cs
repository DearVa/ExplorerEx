using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExplorerEx.DAL.Interfaces;
using ExplorerEx.Model;
using SqlSugar;
using SQLitePCL;

namespace ExplorerEx.DAL.SqlSugar
{
    public class BookmarkSugarContext : IBookmarkDbContext
    {
        

        public void Add(BookmarkItem item)
        {
            throw new NotImplementedException();
        }

        public void Add(BookmarkCategory item)
        {
            throw new NotImplementedException();
        }

        public Task AddAsync(BookmarkItem item)
        {
            throw new NotImplementedException();
        }

        public Task AddAsync(BookmarkCategory item)
        {
            throw new NotImplementedException();
        }

        public ObservableCollection<BookmarkCategory> GetBindable()
        {
            throw new NotImplementedException();
        }

        public ISet<BookmarkCategory> GetBookmarkCategories()
        {
            throw new NotImplementedException();
        }

        public ISet<BookmarkItem> GetBookmarkItems()
        {
            throw new NotImplementedException();
        }

        public ISet<BookmarkItem> GetLocalBookmarkItems()
        {
            throw new NotImplementedException();
        }

        public Task LoadDataBase()
        {
            throw new NotImplementedException();
        }

        public void Remove(BookmarkItem item)
        {
            throw new NotImplementedException();
        }

        public void SaveChanges()
        {
            throw new NotImplementedException();
        }

        public Task SaveChangesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
