using System.Threading.Tasks;
using ExplorerEx.Database.Interface;
using ExplorerEx.Database.SqlSugar;

namespace ExplorerEx.Database; 

internal static class DbMain {
	public static IBookmarkDbContext BookmarkDbContext { get; }

	public static IFileViewDbContext FileViewDbContext { get; }

	static DbMain() {
		BookmarkDbContext = new BookmarkSugarContext();
		FileViewDbContext = new FileViewSugarContext();
	}

	public static Task Initialize() {
		return Task.WhenAll(BookmarkDbContext.LoadAsync(), FileViewDbContext.LoadAsync());
	}

	public static void Save() {
		BookmarkDbContext.Save();
		FileViewDbContext.Save();
	}
}