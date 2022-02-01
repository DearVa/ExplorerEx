using System.IO;
using ExplorerEx.Utils;
using System.Threading.Tasks;
using ExplorerEx.Win32;

namespace ExplorerEx.Model; 

/// <summary>
/// 硬盘驱动器
/// </summary>
internal class DiskDriveItem : FileViewBaseItem {
	public DriveInfo Driver { get; }

	public long UsedSpace { get; }

	public long TotalSpace { get; }

	public string SpaceOverviewString => $"{"Available:".L()}{FileUtils.FormatByteSize(TotalSpace - UsedSpace)}{",".L()}{"Total:".L()}{FileUtils.FormatByteSize(TotalSpace)}";

	public DiskDriveItem(DriveInfo driver) {
		Driver = driver;
		Name = $"{driver.VolumeLabel} ({driver.Name[..1]})";
		TotalSpace = driver.TotalSize;
		UsedSpace = TotalSpace - driver.AvailableFreeSpace;  // 考虑用户配额
	}

	public override async Task LoadIconAsync() {
		Icon = await IconHelper.GetLargePathIcon(Driver.Name, true, true);
	}
}