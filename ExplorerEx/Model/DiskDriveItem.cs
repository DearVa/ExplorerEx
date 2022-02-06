using System.IO;
using ExplorerEx.Utils;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerEx.Win32;
using ExplorerEx.ViewModel;
using System.Runtime.CompilerServices;
using System;
using System.Diagnostics;

namespace ExplorerEx.Model; 

/// <summary>
/// 硬盘驱动器
/// </summary>
public class DiskDriveItem : FileViewBaseItem {
	public DriveInfo Driver { get; }

	public long FreeSpace { get; }

	public long TotalSpace { get; }

	public double FreeSpaceRatio => (double)(TotalSpace - FreeSpace) / TotalSpace;

	public Brush ProgressBarBackground => FreeSpaceRatio > 0.8d ? new SolidColorBrush(GradientColor.Eval((FreeSpaceRatio - 0.5d) * 2d)) : NormalProgressBrush;

	public string SpaceOverviewString => $"{"Available: ".L()}{FileUtils.FormatByteSize(FreeSpace)}{", ".L()}{"Total: ".L()}{FileUtils.FormatByteSize(TotalSpace)}";

	private static readonly SolidColorBrush NormalProgressBrush = new(Colors.ForestGreen);

	private static readonly Gradient GradientColor = new(Colors.ForestGreen, Colors.Orange, Colors.Red);

	public DiskDriveItem(FileViewTabViewModel ownerViewModel, DriveInfo driver) : base(ownerViewModel) {
		Driver = driver;
		Name = $"{(string.IsNullOrWhiteSpace(driver.VolumeLabel) ? "Local_disk".L() : driver.VolumeLabel)} ({driver.Name[..1]})";
		TotalSpace = driver.TotalSize;
		FreeSpace = driver.AvailableFreeSpace;  // 考虑用户配额
	}

	public override async Task LoadIconAsync() {
		Icon = await IconHelper.GetLargePathIcon(Driver.Name, true, true);
	}

	protected override bool Rename() {
		throw new NotImplementedException();
	}

	public async Task RefreshAsync() {
		await LoadIconAsync();
		OnPropertyChanged(nameof(Icon));
	}

	private class Gradient {
		private readonly int segmentLength;
		private readonly HSVColor[] colors;
		private readonly Color endColor;

		public Gradient(params Color[] colors) {
			Debug.Assert(colors is {Length: > 1});
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