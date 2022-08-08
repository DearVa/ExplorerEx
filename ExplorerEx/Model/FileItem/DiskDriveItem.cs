using System.IO;
using ExplorerEx.Utils;
using System.Windows.Media;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ExplorerEx.Shell32;
using HandyControl.Controls;

namespace ExplorerEx.Model;

/// <summary>
/// 硬盘驱动器
/// </summary>
public sealed class DiskDriveItem : FolderItem {
	public DriveInfo Drive { get; }

	public override string DisplayText => DriveUtils.GetFriendlyName(Drive);

	public long FreeSpace { get; private set; }

	public long TotalSpace { get; private set; }

	public double FreeSpaceRatio => TotalSpace == -1 ? 0 : (double)(TotalSpace - FreeSpace) / TotalSpace;

	public Brush ProgressBarBackground => FreeSpaceRatio > 0.8d ? new SolidColorBrush(GradientColor.Eval((FreeSpaceRatio - 0.5d) * 2d)) : NormalProgressBrush;

	public string SpaceOverviewString => $"{"Available: ".L()}{FileUtils.FormatByteSize(FreeSpace)}{", ".L()}{"Total: ".L()}{FileUtils.FormatByteSize(TotalSpace)}";

	private static readonly SolidColorBrush NormalProgressBrush = new(Colors.ForestGreen);

	private static readonly Gradient GradientColor = new(Colors.ForestGreen, Colors.Orange, Colors.Red);

	public DiskDriveItem(DriveInfo drive) : base(drive.Name) {
		Drive = drive;
		FullPath = drive.Name;
		IsFolder = true;
		TotalSpace = -1;
		Name = drive.Name;
	}

	public override void LoadAttributes(LoadDetailsOptions options) {
		Type = DriveUtils.GetTypeDescription(Drive);
		if (Drive.IsReady) {
			TotalSpace = Drive.TotalSize;
			FreeSpace = Drive.AvailableFreeSpace; // 考虑用户配额
		} else {
			TotalSpace = -1;
		}
		OnPropertyChanged(nameof(FreeSpace));
		OnPropertyChanged(nameof(TotalSpace));
		OnPropertyChanged(nameof(FreeSpaceRatio));
		OnPropertyChanged(nameof(ProgressBarBackground));
		OnPropertyChanged(nameof(SpaceOverviewString));
	}

	public override void LoadIcon(LoadDetailsOptions options) {
		Icon = IconHelper.GetPathThumbnail(Drive.Name);
	}

	public Task<List<FileListViewItem>> EnumerateItems() {
		throw new NotImplementedException();
	}

	public override string GetRenameName() {
		if (Drive.IsReady) {
			return string.IsNullOrWhiteSpace(Drive.VolumeLabel) ? Type : Drive.VolumeLabel;
		}
		MessageBox.Error("DriveIsNotReady".L());
		return null;
	}

	protected override bool InternalRename(string newName) {
		try {
			Drive.VolumeLabel = newName;
			return true;
		} catch (Exception e) {
			Logger.Error(e.Message);
			return false;
		}
	}

	private class Gradient {
		private readonly int segmentLength;
		private readonly HSVColor[] colors;
		private readonly Color endColor;

		public Gradient(params Color[] colors) {
			Debug.Assert(colors is { Length: > 1 });
			this.colors = new HSVColor[colors.Length];
			for (var i = 0; i < colors.Length; i++) {
				this.colors[i] = new HSVColor(colors[i]);
			}
			segmentLength = colors.Length - 1;
			endColor = colors[segmentLength];
		}

		public Color Eval(double v) {
			if (v >= 1f) {
				return endColor;
			}
			var j = v * segmentLength;
			var i = (int)j;
			return HSVColor.Lerp(colors[i], colors[i + 1], j - i);
		}
	}
}