using System;
using System.Threading.Tasks;
using ExplorerEx.Model;

namespace ExplorerEx.Database.Interface; 

public interface IFileViewDbContext : IDatabase {
	FileView? FirstOrDefault(Func<FileView, bool> match);
	bool Contains(FileView fileView);
	bool Any(Func<FileView, bool> match);

	void Add(FileView item);
	Task AddAsync(FileView item);
}