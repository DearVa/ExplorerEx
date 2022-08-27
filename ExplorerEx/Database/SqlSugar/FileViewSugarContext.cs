using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExplorerEx.Database.Interface;
using ExplorerEx.Model;

namespace ExplorerEx.Database.SqlSugar; 

public class FileViewSugarContext : SugarContext, IFileViewDbContext {
	private readonly SugarCache<FileView> fileSugarCache;

	public FileViewSugarContext() : base("FileViews.db") {
		fileSugarCache = new SugarCache<FileView>(ConnectionClient);
	}

	public override Task LoadAsync() {
		return Task.Run(() => {
			ConnectionClient.CodeFirst.InitTables<FileView>();
			fileSugarCache.LoadDatabase();
		});
	}

	public bool Any(Func<FileView, bool> match) {
		throw new NotImplementedException();
	}

	public void Add(FileView item) {
		fileSugarCache.Add(item);
	}

	public Task AddAsync(FileView item) {
		return Task.Run(() => Add(item));
	}

	public FileView? FirstOrDefault(Func<FileView, bool> match) {
		return fileSugarCache.FirstOrDefault(match);
	}

	public bool Contains(FileView fileView) {
		return fileSugarCache.Contains(fileView);
	}

	public override void Save() {
		fileSugarCache.Save();
	}

	public override Task SaveAsync() {
		return Task.Run(Save);
	}
}