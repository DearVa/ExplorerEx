using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using ExplorerEx.Database.Interface;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using Microsoft.EntityFrameworkCore;

namespace ExplorerEx.Database.EntityFramework;

public class FileViewEfContext : DbContext, IFileViewDbContext {
	private DbSet<FileView> FileViewDbSet { get; set; } = null!;

	private readonly string dbPath;

	public FileViewEfContext() {
		var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data");
		if (!Directory.Exists(path)) {
			Directory.CreateDirectory(path);
		}
		dbPath = Path.Combine(path, "FileViews.db");
	}

	protected override void OnConfiguring(DbContextOptionsBuilder ob) {
		ob.UseSqlite($"Data Source={dbPath}");
	}

	public bool Any(Func<FileView, bool> match) {
		return FileViewDbSet.Any(match);
	}

	public void Add(FileView item) {
		FileViewDbSet.Add(item);
	}

	public Task AddAsync(FileView item) {
		return FileViewDbSet.AddAsync(item).AsTask();
	}

	public async Task LoadAsync() {
		try {
			await Database.EnsureCreatedAsync();
			await FileViewDbSet.LoadAsync();
		} catch (Exception e) {
			MessageBox.Show("无法加载数据库，可能是权限不够或者数据库版本过旧，请删除Data文件夹后再试一次。\n错误为：" + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
			Logger.Exception(e, false);
		}
	}

	public void Save() {
		base.SaveChanges();
	}

	public Task SaveAsync() {
		return base.SaveChangesAsync();
	}

	public FileView? FirstOrDefault(Func<FileView, bool> match) {
		return FileViewDbSet.FirstOrDefault(match);
	}

	public bool Contains(FileView fileView) {
		return FileViewDbSet.Contains(fileView);
	}
}