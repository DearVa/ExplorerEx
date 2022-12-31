using ExplorerEx.Database.Interface;
using ExplorerEx.Models;

namespace ExplorerEx.Database.SqlSugar; 

/// <summary>
/// 这个不需要Cache
/// </summary>
public class FileViewSugarContext : SugarContext<FileView>, IFileViewDbContext {
	public FileViewSugarContext() : base("FileViews.db") { }
}