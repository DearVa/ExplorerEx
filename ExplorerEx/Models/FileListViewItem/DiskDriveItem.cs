using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerEx.Utils;
using HandyControl.Controls;

namespace ExplorerEx.Models;

/// <summary>
/// 硬盘驱动器
/// </summary>
public sealed class DiskDriveItem : FolderItem {
	public DriveInfo Drive { get; }

	public override string DisplayText => DriveUtils.GetFriendlyName(Drive);

	public long FreeSpace { get; private set; }

	public long TotalSpace { get; private set; }

	/// <summary>
	/// 使用占比
	/// </summary>
	public double PercentFull => TotalSpace == -1 ? 0 : (double)(TotalSpace - FreeSpace) / TotalSpace;

	public Brush ProgressBarBackground => PercentFull > 0.8d ? new SolidColorBrush(GradientColor.Eval((PercentFull - 0.8d) * 10d)) : NormalProgressBrush;

	public string SpaceOverviewString => $"{"Available: ".L()}{FileUtils.FormatByteSize(FreeSpace)}{", ".L()}{"Total: ".L()}{FileUtils.FormatByteSize(TotalSpace)}";

	private static readonly SolidColorBrush NormalProgressBrush = new(Colors.ForestGreen);

	private static readonly Gradient GradientColor = new(Colors.ForestGreen, Colors.OrangeRed, Colors.Red);

	public DiskDriveItem(DriveInfo drive) : base(new DirectoryInfo(drive.Name), LoadDetailsOptions.Default) {
		Drive = drive;
		FullPath = drive.Name;
		IsFolder = true;
		TotalSpace = -1;
		Name = drive.Name;
	}

	protected override void LoadAttributes() {
		Type = DriveUtils.GetTypeDescription(Drive);
		if (Drive.IsReady) {
			TotalSpace = Drive.TotalSize;
			FreeSpace = Drive.AvailableFreeSpace; // 考虑用户配额
		} else {
			TotalSpace = -1;
		}
		OnPropertyChanged(nameof(FreeSpace));
		OnPropertyChanged(nameof(TotalSpace));
		OnPropertyChanged(nameof(PercentFull));
		OnPropertyChanged(nameof(ProgressBarBackground));
		OnPropertyChanged(nameof(SpaceOverviewString));
	}

	protected override void LoadIcon() {
		Icon = IconHelper.GetDriveThumbnail(Drive.Name);
	}

	public Task<List<FileListViewItem>> EnumerateItems() {
		throw new NotImplementedException();
	}

	public override string? GetRenameName() {
		if (Drive.IsReady) {
			return string.IsNullOrWhiteSpace(Drive.VolumeLabel) ? Type ?? string.Empty : Drive.VolumeLabel;
		}
		MessageBox.Error("DriveIsNotReady".L());
		return null;
	}

	protected override void InternalRename(string newName) {
		Drive.VolumeLabel = newName;
	}

	private class Gradient {
		private readonly int segmentLength;
		private readonly HsvColor[] colors;

		public Gradient(params Color[] colors) {
			Debug.Assert(colors is { Length: > 1 });
			this.colors = new HsvColor[colors.Length];
			for (var i = 0; i < colors.Length; i++) {
				this.colors[i] = new HsvColor(colors[i]);
			}
			segmentLength = colors.Length - 1;
		}

		public Color Eval(double v) {
			switch (v) {
			case <= 0d:
				return colors[0].ToRgb();
			case >= 1d:
				return colors[^1].ToRgb();
			default:
				var j = v * segmentLength;
				var i = (int)j;
				return HsvColor.Lerp(colors[i], colors[i + 1], j - i);
			}
		}
	}
}