using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExplorerEx.Model;

namespace ExplorerEx.Database.Interface {
	public interface IFileViewDbContext : IDatabase {
		ISet<FileView> GetFileViews();
		FileView? FindFirstOrDefault(Func<FileView, bool> match);

		void Add(FileView item);
		Task AddAsync(FileView item);
	}
}
