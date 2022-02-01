using System.IO;
using ExplorerEx.Utils;
using System.Threading.Tasks;
using ExplorerEx.Win32;
using ExplorerEx.ViewModel;

namespace ExplorerEx.Model; 

/// <summary>
/// 硬盘驱动器
/// </summary>
internal class DiskDriveItem : FileViewBaseItem {
	public DriveInfo Driver { get; }

	public long FreeSpace { get; }

	public long TotalSpace { get; }

	public double FreeSpaceRatio => (double)FreeSpace / TotalSpace;

	public string SpaceOverviewString => $"{"Available: ".L()}{FileUtils.FormatByteSize(FreeSpace)}{", ".L()}{"Total: ".L()}{FileUtils.FormatByteSize(TotalSpace)}";

	public DiskDriveItem(FileViewTabViewModel ownerViewModel, DriveInfo driver) : base(ownerViewModel) {
		Driver = driver;
		Name = $"{(string.IsNullOrWhiteSpace(driver.VolumeLabel) ? "Local_disk".L() : driver.VolumeLabel)} ({driver.Name[..1]})";
		TotalSpace = driver.TotalSize;
		FreeSpace = driver.AvailableFreeSpace;  // 考虑用户配额
	}

	public override async Task LoadIconAsync() {
		Icon = await IconHelper.GetLargePathIcon(Driver.Name, true, true);
	}
}