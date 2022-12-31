using System.Threading.Tasks;

namespace ExplorerEx.Database.Interface; 

/// <summary>
/// 这个对应一个数据库文件或者一个数据源
/// </summary>
public interface IDatabase {
	/// <summary>
	/// 负责对数据库加载的异常进行处理，并处理特殊情况（如首次加载、错误数据的修正等等）
	/// </summary>
	/// <returns></returns>
	public Task LoadAsync();

	public void Save();

	public Task SaveAsync();
}