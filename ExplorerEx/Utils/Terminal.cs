using System.Diagnostics;
using System.Windows.Media;

namespace ExplorerEx.Utils; 

/// <summary>
/// 与Windows终端有关的类
/// </summary>
internal static class Terminal {
	public static string WindowsTerminalPath { get; } = FileUtils.FindFileLocation("WindowsTerminal.exe");

	public const string CmdPath = @"C:\Windows\System32\cmd.exe";

	public static ImageSource TerminalIcon { get; } = IconHelper.GetLargeIcon(WindowsTerminalPath ?? CmdPath, false);

	public static void RunTerminal(string workingPath) {
		Process.Start(new ProcessStartInfo {
			WorkingDirectory = workingPath,
			FileName = WindowsTerminalPath ?? CmdPath,
			UseShellExecute = true
		});
	}
}