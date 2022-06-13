using System;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ExplorerEx.Utils; 

public readonly struct HSVColor {
	public readonly double hue, saturation, value;

	public HSVColor(double hue, double saturation, double value) {
		this.hue = Math.Min(Math.Max(hue, 0), 360);
		this.saturation = Math.Min(Math.Max(saturation, 0), 1);
		this.value = Math.Min(Math.Max(value, 0), 1);
	}

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

	public Color ToRGB() {
		return HSV2RGB(hue, saturation, value);
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

public static class HSVColorUtils {
	public static HSVColor ToHSV(this Color color) {
		return new HSVColor(color);
	}
}
