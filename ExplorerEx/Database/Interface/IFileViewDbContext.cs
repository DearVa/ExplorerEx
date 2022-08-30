using System;
using System.Linq.Expressions;
using ExplorerEx.Model;

namespace ExplorerEx.Database.Interface; 

public interface IFileViewDbContext : IDatabase {
	FileView? FirstOrDefault(Expression<Func<FileView, bool>> match);
	bool Contains(FileView fileView);
	bool Any(Expression<Func<FileView, bool>> match);
	void Add(FileView item);
	/// <summary>
	/// 由于不带Cache跟踪，所以需要手动更新数据
	/// </summary>
	/// <param name="item"></param>
	void Update(FileView item);
}