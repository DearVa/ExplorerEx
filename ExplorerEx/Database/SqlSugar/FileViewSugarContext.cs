using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExplorerEx.Database.Interface;
using ExplorerEx.Model;

namespace ExplorerEx.Database.SqlSugar {
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

		public void Add(FileView item) {
			fileSugarCache.Add(item);
		}

		public Task AddAsync(FileView item) {
			return Task.Run(() => Add(item));
		}

		public FileView? FindFirstOrDefault(Func<FileView, bool> match) {
			return fileSugarCache.Find(match);
		}

		public ISet<FileView> GetFileViews() {
			return fileSugarCache.QueryAll().ToHashSet();
		}

		public override void Save() {
			fileSugarCache.Save();
		}

		public override Task SaveAsync() {
			return Task.Run(Save);
		}
	}
}
