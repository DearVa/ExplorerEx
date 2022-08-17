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
using Castle.MicroKernel.Internal;

namespace ExplorerEx.DAL.SqlSugar
{
    public class BookmarkSugarContext :SugarContext, IBookmarkDbContext
    {
        private readonly SugarCache<BookmarkItem> itemSugarCache;
        private readonly SugarCache<BookmarkCategory> categorySugarCache;


        public BookmarkSugarContext():base("BookMarks.db")
        {
            itemSugarCache = new SugarCache<BookmarkItem>(ConnectionClient);
            categorySugarCache = new SugarCache<BookmarkCategory,BookmarkItem>(ConnectionClient,
                new(itemSugarCache, (category, item) =>
                {
                    if (item.CategoryForeignKey == category.Name)
                    {
                        item.Category = category;
                        category.Children?.Add(item);
                    }
                }));
        }

        public new Task LoadDataBase()
        {
            return Task.Run(async() =>
            {
               await base.LoadDataBase();
               ConnectionClient.CodeFirst.InitTables<BookmarkItem,BookmarkCategory>();
               itemSugarCache.LoadDatabase();
               categorySugarCache.LoadDatabase();
            });
        }

        public void Add(BookmarkItem item)
        {
            itemSugarCache.Add(item);
        }

        public void Add(BookmarkCategory item)
        {
            categorySugarCache.Add(item);
        }

        public Task AddAsync(BookmarkItem item)
        {
            return Task.Run(() => Add(item));
        }

        public Task AddAsync(BookmarkCategory item)
        {
            return Task.Run(() => Add(item));
        }

        public BookmarkCategory? FindFirstOrDefault(Func<BookmarkCategory, bool> match)
        {
            return  categorySugarCache.Find(match);
        }

        public BookmarkItem? FindLocalItemFirstOrDefault(Func<BookmarkItem, bool> match)
        {
            return itemSugarCache.Find(match);
        }

        public ObservableCollection<BookmarkCategory> GetBindable()
        {
            ObservableCollection<BookmarkCategory> ret = new ObservableCollection<BookmarkCategory>();
            categorySugarCache.QueryAll().ForEach(x => { ret.Add(x); });
            return ret;
        }

        public ISet<BookmarkCategory> GetBookmarkCategories()
        {
            return categorySugarCache.QueryAll().ToHashSet();
        }

        public ISet<BookmarkItem> GetBookmarkItems()
        {
            return itemSugarCache.QueryAll().ToHashSet();
        }

        public ISet<BookmarkItem> GetLocalBookmarkItems()
        {
            return itemSugarCache.QueryAll().ToHashSet();
        }


        public void Remove(BookmarkItem item)
        {
            itemSugarCache.Remove(item);
        }

        public void Save()
        {
            itemSugarCache.Save();
            categorySugarCache.Save();
        }

        public Task SaveAsync()
        {
            return Task.Run(() => { Save(); });
        }
    }
}
