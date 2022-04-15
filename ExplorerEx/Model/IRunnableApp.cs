namespace ExplorerEx.Model;

/// <summary>
/// 可以运行的APP
/// </summary>
public interface IRunnableApp {
	/// <summary>
	/// 运行该app，带有命令参数
	/// </summary>
	/// <param name="args"></param>
	void Run(string args);
}