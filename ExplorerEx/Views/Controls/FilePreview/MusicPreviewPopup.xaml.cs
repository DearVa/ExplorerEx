using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ExplorerEx.Utils;

namespace ExplorerEx.Views.Controls;

public sealed partial class MusicPreviewPopup {
	public static MusicPreviewPopup Instance { get; } = new();

	private bool HasCover => Path.GetExtension(FilePath)!.ToLower() is ".mp3" or ".flac";

	public MediaPlayer MediaPlayer { get; } = new();

	private readonly DrawingImage musicImage;

	private readonly Dictionary<Uri, TimeSpan> playHistory = new();
	private MediaTimeline? timeline;
	private ClockController? controller;

	static MusicPreviewPopup() { }

	private MusicPreviewPopup() {
		musicImage = (DrawingImage)Application.Current.Resources["MusicDrawingImage"];
		InitializeComponent();
		MediaPlayer.MediaOpened += MediaPlayer_OnMediaOpened;
	}

	private void MediaPlayer_OnMediaOpened(object? sender, EventArgs e) {
		if (timeline == null || timeline.Source == null) {
			return;
		}
		if (playHistory.TryGetValue(timeline.Source, out var ts)) {
			controller?.Seek(ts, TimeSeekOrigin.BeginTime);
		}
		StatusTextBlock.Text = Path.GetFileName(FilePath);
	}

	private void Timeline_OnCurrentTimeInvalidated(object? sender, EventArgs e) {
		if (controller == null) {
			return;
		}
		if (controller.Clock.NaturalDuration.HasTimeSpan && controller.Clock.CurrentTime.HasValue) {
			var currentTime = controller.Clock.CurrentTime.Value;
			var naturalDuration = controller.Clock.NaturalDuration.TimeSpan;
			CurrentTimeTextBlock.Text = currentTime.ToString(@"hh\:mm\:ss");
			TotalTimeTextBlock.Text = naturalDuration.ToString(@"hh\:mm\:ss");
			var ratio = currentTime / naturalDuration;
			TimeSlider.Value = ratio;
			CoverImageRotation.Angle = currentTime.TotalSeconds * 18d;
			DickHandleRotation.Angle = 3d + 8d * ratio;
		}
	}

	public override void Load(string filePath) {
		if (!File.Exists(filePath)) {
			return;
		}
		FilePath = filePath;
		StatusTextBlock.Text = "Loading".L();
		if (timeline != null && timeline.Source != null && controller != null) {
			playHistory[timeline.Source] = controller.Clock.CurrentTime.GetValueOrDefault();
			controller.Stop();
		}
		var newSource = new Uri(filePath, UriKind.Absolute);
		if (playHistory.TryGetValue(newSource, out var ts)) {
			CoverImageRotation.Angle = ts.TotalSeconds * 18d;
		} else {
			CoverImageRotation.Angle = 0;
		}
		timeline = new MediaTimeline(newSource) {
			RepeatBehavior = RepeatBehavior.Forever
		};
		timeline.CurrentTimeInvalidated += Timeline_OnCurrentTimeInvalidated;
		var clock = MediaPlayer.Clock = timeline.CreateClock();
		controller = clock.Controller;
		if (HasCover) {
			CoverImage.Source = IconHelper.GetPathThumbnail(filePath);
			CoverImage.Source ??= musicImage;
		} else {
			CoverImage.Source = musicImage;
		}
		IsOpen = true;
	}

	public override void Close() {
		IsOpen = false;
		if (timeline != null && timeline.Source != null && controller != null) {
			playHistory[timeline.Source] = controller.Clock.CurrentTime.GetValueOrDefault();
			controller.Stop();
		}
		FilePath = null;
		timeline = null;
		controller = null;
	}

	public override void HandleMouseScroll(MouseWheelEventArgs e) {
		if (controller != null && controller.Clock.NaturalDuration.HasTimeSpan) {
			var offset = controller.Clock.CurrentTime.GetValueOrDefault().Add(TimeSpan.FromMilliseconds(-e.Delta * 10));
			if (offset < TimeSpan.Zero) {
				offset = TimeSpan.Zero;
			} else if (offset > controller.Clock.NaturalDuration.TimeSpan) {
				offset = controller.Clock.NaturalDuration.TimeSpan;
			}
			controller?.Seek(offset, TimeSeekOrigin.BeginTime);
		}
	}
}