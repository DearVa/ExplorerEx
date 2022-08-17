using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using ExplorerEx.DAL.Interfaces;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using Microsoft.EntityFrameworkCore;

namespace ExplorerEx.DAL.EntityFramework;


public class BookmarkEfContext : DbContext , IBookmarkDbContext
{
    #region  Fields

    private DbSet<BookmarkCategory> BookmarkCategoryDbSet { get; set; } = null!;
    private DbSet<BookmarkItem> BookmarkDbSet { get; set; } = null!;

    private readonly string dbPath;
    #endregion


    public BookmarkEfContext()
    {
        var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        dbPath = Path.Combine(path, "BookMarks.db");
    }

    public async Task LoadDataBase()
    {
        try
        {
            await Database.EnsureCreatedAsync();
            await BookmarkCategoryDbSet.LoadAsync();
            await BookmarkDbSet.LoadAsync();

            if (BookmarkCategoryDbSet.ToArray().Length == 0)
            {
                var defaultCategory = new BookmarkCategory("Default_bookmark".L());
                await BookmarkCategoryDbSet.AddAsync(defaultCategory);
                await BookmarkDbSet.AddRangeAsync(
                    new BookmarkItem(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                        "Documents".L(), 
                        defaultCategory),
                    new BookmarkItem(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "Desktop".L(), 
                        defaultCategory));
                await SaveAsync();
            }
            await Task.Run(() =>
            {
                foreach (var item in BookmarkDbSet.Local)
                {
                    item.LoadIcon(FileListViewItem.LoadDetailsOptions.Default);
                }
            });
        }
        catch (Exception e)
        {
            MessageBox.Show("无法加载数据库，可能是权限不够或者数据库版本过旧，请删除Data文件夹后再试一次。\n错误为：" + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Logger.Exception(e, false);
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder ob)
    {
        ob.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookmarkItem>().HasOne(b => b.Category)
            .WithMany(cb => cb.Children).HasForeignKey(b => b.CategoryForeignKey);
    }

    #region Interfaces

    public ISet<BookmarkItem> GetBookmarkItems()
    {
        return BookmarkDbSet.ToHashSet();
    }

    public void Add(BookmarkItem item)
    {
        BookmarkDbSet.Add(item);
    }
    public Task AddAsync(BookmarkItem item)
    {
        return BookmarkDbSet.AddAsync(item).AsTask();
    }

    public void Add(BookmarkCategory item)
    {
        BookmarkCategoryDbSet.Add(item);
    }
    public Task AddAsync(BookmarkCategory item)
    {
        return BookmarkCategoryDbSet.AddAsync(item).AsTask();
    }

    
    public ISet<BookmarkItem> GetLocalBookmarkItems()
    {
        return BookmarkDbSet.Local.ToHashSet();
    }
    public ISet<BookmarkCategory> GetBookmarkCategories()
    {
        return BookmarkCategoryDbSet.ToHashSet();
    }

    public new void Save()
    {
        base.SaveChanges();
    }

    public Task SaveAsync()
    {
        return base.SaveChangesAsync();
    }

    public void Remove(BookmarkItem item)
    {
        base.Remove(item);
    }

    public ObservableCollection<BookmarkCategory> GetBindable()
    {
        return BookmarkCategoryDbSet.Local.ToObservableCollection();
    }

    #region ProbablyModify
    public BookmarkCategory? FindFirstOrDefault(Func<BookmarkCategory, bool> match)
    {
        return GetBookmarkCategories().FirstOrDefault(match);
    }

    public BookmarkItem? FindLocalItemFirstOrDefault(Func<BookmarkItem, bool> match)
    {
        return GetLocalBookmarkItems().FirstOrDefault(match);
    }



    #endregion

    #endregion
}