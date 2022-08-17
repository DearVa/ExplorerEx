using System;
using System.Collections.Generic;
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

public class FileViewEfContext : DbContext ,IFileViewDbContext {
    private DbSet<FileView> FolderViewDbSet { get; set; } = null!;

    private readonly string dbPath;

    public FileViewEfContext() {
        var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data");
        if (!Directory.Exists(path)) {
            Directory.CreateDirectory(path);
        }
        dbPath = Path.Combine(path, "FileViews.db");
    }

    public async Task LoadDataBase() {
        try {
            await Database.EnsureCreatedAsync();
            await FolderViewDbSet!.LoadAsync();
        } catch (Exception e) {
            MessageBox.Show("无法加载数据库，可能是权限不够或者数据库版本过旧，请删除Data文件夹后再试一次。\n错误为：" + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Logger.Exception(e, false);
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder ob) {
        ob.UseSqlite($"Data Source={dbPath}");
    }


    public ISet<FileView> GetFileViews()
    {
        return FolderViewDbSet.ToHashSet();
    }

    public void Add(FileView item)
    {
        FolderViewDbSet.Add(item);
    }

    public Task AddAsync(FileView item)
    {
        return FolderViewDbSet.AddAsync(item).AsTask();
    }

    public void Save()
    {
        base.SaveChanges();
    }

    public Task SaveAsync()
    {
        return base.SaveChangesAsync();
    }

    public FileView? FindFirstOrDefault(Func<FileView, bool> match)
    {
        return GetFileViews().FirstOrDefault(match);
    }
}