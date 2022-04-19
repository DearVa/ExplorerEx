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

	public override void LoadAttributes() {
		Type = DriveUtils.GetTypeDescription(Drive);
		if (Drive.IsReady) {
			TotalSpace = Drive.TotalSize;
			FreeSpace = Drive.AvailableFreeSpace; // 考虑用户配额
		} else {
			TotalSpace = -1;
		}
		UpdateUI(nameof(FreeSpace));
		UpdateUI(nameof(TotalSpace));
		UpdateUI(nameof(FreeSpaceRatio));
		UpdateUI(nameof(ProgressBarBackground));
		UpdateUI(nameof(SpaceOverviewString));
	}

	public override void LoadIcon() {
		Icon = IconHelper.GetPathThumbnail(Drive.Name);
	}

	public Task<List<FileListViewItem>> EnumerateItems() {
		throw new NotImplementedException();
	}

	public override void StartRename() {
		if (Drive.IsReady) {
			EditingName = string.IsNullOrWhiteSpace(Drive.VolumeLabel) ? Type : Drive.VolumeLabel;
		} else {
			MessageBox.Error("DriveIsNotReady".L());
		}
	}

	protected override bool Rename() {
		try {
			Drive.VolumeLabel = EditingName;
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

		private readonly struct HSVColor {
			public readonly double hue, saturation, value;

			public HSVColor(Color color) {
				var r = color.R;
				var g = color.G;
				var b = color.B;
				int max = Math.Max(r, Math.Max(g, b));
				int min = Math.Min(r, Math.Min(g, b));

				if (r == g && g == b) {
					hue = 0f;
				} else {
					float delta = max - min;
					if (r == max) {
						hue = (g - b) / delta;
					} else if (g == max) {
						hue = (b - r) / delta + 2f;
					} else {
						hue = (r - g) / delta + 4f;
					}
					hue *= 60f;
					if (hue < 0f) {
						hue += 360f;
					}
				}
				saturation = (max == 0) ? 0 : 1f - (1f * min / max);
				value = max / 255f;
			}

			private static Color HSV2RGB(double hue, double saturation, double value) {
				var hi = (byte)Math.Floor(hue / 60) % 6;
				var f = hue / 60 - Math.Floor(hue / 60);
				value *= 255;
				var v = (byte)value;
				var p = (byte)(value * (1 - saturation));
				var q = (byte)(value * (1 - f * saturation));
				var t = (byte)(value * (1 - (1 - f) * saturation));
				return hi switch {
					0 => Color.FromArgb(255, v, t, p),
					1 => Color.FromArgb(255, q, v, p),
					2 => Color.FromArgb(255, p, v, t),
					3 => Color.FromArgb(255, p, q, v),
					4 => Color.FromArgb(255, t, p, v),
					_ => Color.FromArgb(255, v, p, q)
				};
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static double Lerp(double f0, double f1, double v) {
				return f0 + (f1 - f0) * v;
			}

			public static Color Lerp(HSVColor color0, HSVColor color1, double v) {
				return HSV2RGB(Lerp(color0.hue, color1.hue, v), Lerp(color0.saturation, color1.saturation, v), Lerp(color0.value, color1.value, v));
			}
		}
	}
}