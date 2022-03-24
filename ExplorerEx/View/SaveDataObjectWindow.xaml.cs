using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using ExplorerEx.Utils;
using System.Windows.Media.Imaging;
using ExplorerEx.Win32;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExplorerEx.Command;

namespace ExplorerEx.View;

/// <summary>
/// 将文本、链接或者图像存成文件
/// 是不是感觉这个类写的很烂？确实，因为我还没写完
/// </summary>
public partial class SaveDataObjectWindow {
	public string Text { get; set; }

	public string Url { get; }

	public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(
		"Image", typeof(BitmapImage), typeof(SaveDataObjectWindow), new PropertyMetadata(default(BitmapImage)));

	public BitmapImage Image {
		get => (BitmapImage)GetValue(ImageProperty);
		set => SetValue(ImageProperty, value);
	}

	public static readonly DependencyProperty ImageDownloadProgressProperty = DependencyProperty.Register(
		"ImageDownloadProgress", typeof(double), typeof(SaveDataObjectWindow), new PropertyMetadata(default(double)));

	public double ImageDownloadProgress {
		get => (double)GetValue(ImageDownloadProgressProperty);
		set => SetValue(ImageDownloadProgressProperty, value);
	}

	public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(
		"IsIndeterminate", typeof(bool), typeof(SaveDataObjectWindow), new PropertyMetadata(default(bool)));

	public bool IsIndeterminate {
		get => (bool)GetValue(IsIndeterminateProperty);
		set => SetValue(IsIndeterminateProperty, value);
	}

	public static readonly DependencyProperty SaveFileNameProperty = DependencyProperty.Register(
		"SaveFileName", typeof(string), typeof(SaveDataObjectWindow), new PropertyMetadata(default(string)));

	public string SaveFileName {
		get => (string)GetValue(SaveFileNameProperty);
		set => SetValue(SaveFileNameProperty, value);
	}

	public static readonly DependencyProperty CanSaveProperty = DependencyProperty.Register(
		"CanSave", typeof(bool), typeof(SaveDataObjectWindow), new PropertyMetadata(default(bool)));

	public bool CanSave {
		get => (bool)GetValue(CanSaveProperty);
		set => SetValue(CanSaveProperty, value);
	}


	public static readonly DependencyProperty SaveAsImageProperty = DependencyProperty.Register(
		"SaveAsImage", typeof(bool), typeof(SaveDataObjectWindow), new PropertyMetadata(false, SaveAsImage_OnChanged));

	private static void SaveAsImage_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if ((bool)e.NewValue) {
			var window = (SaveDataObjectWindow)d;
			window.SaveAsLink = false;
			window.SaveAsText = false;
			if (window.imageExtension != null) {
				window.SaveFileName = Path.ChangeExtension(window.SaveFileName, window.imageExtension);
			}
		}
	}

	public bool SaveAsImage {
		get => (bool)GetValue(SaveAsImageProperty);
		set => SetValue(SaveAsImageProperty, value);
	}

	public static readonly DependencyProperty SaveAsLinkProperty = DependencyProperty.Register(
		"SaveAsLink", typeof(bool), typeof(SaveDataObjectWindow), new PropertyMetadata(false, SaveAsLink_OnChanged));

	private static void SaveAsLink_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if ((bool)e.NewValue) {
			var window = (SaveDataObjectWindow)d;
			window.SaveAsImage = false;
			window.SaveAsText = false;
			window.SaveFileName = Path.ChangeExtension(window.SaveFileName, ".url");
		}
	}

	public bool SaveAsLink {
		get => (bool)GetValue(SaveAsLinkProperty);
		set => SetValue(SaveAsLinkProperty, value);
	}

	public static readonly DependencyProperty SaveAsTextProperty = DependencyProperty.Register(
		"SaveAsText", typeof(bool), typeof(SaveDataObjectWindow), new PropertyMetadata(false, SaveAsText_OnChanged));

	private static void SaveAsText_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		if ((bool)e.NewValue) {
			var window = (SaveDataObjectWindow)d;
			window.SaveAsLink = false;
			window.SaveAsImage = false;
			window.SaveFileName = Path.ChangeExtension(window.SaveFileName, ".txt");
		}
	}

	public bool SaveAsText {
		get => (bool)GetValue(SaveAsTextProperty);
		set => SetValue(SaveAsTextProperty, value);
	}

	public SimpleCommand SaveCommand { get; }

	private readonly string basePath;

	private long imageSize;

	private readonly string imageExtension;

	public SaveDataObjectWindow(string basePath, string textOrLink) {
		this.basePath = basePath;
		DataContext = this;
		InitializeComponent();
		Win32Interop.GetCursorPos(out var p);
		var mousePoint = new Point(p.x, p.y);
		Left = Math.Min(SystemParameters.PrimaryScreenWidth - Width, Math.Max(0, mousePoint.X));
		Top = Math.Min(SystemParameters.PrimaryScreenHeight - Height, Math.Max(0, mousePoint.Y));

		var originalTextOrLink = textOrLink;
		if (Regex.IsMatch(textOrLink, @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=\u4e00-\u9fa5]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()!@:%_\+.~#?&\/\/=\u4e00-\u9fa5]*)")) {
			// 比如B站，封面的链接形如https://i2.hdslb.com/bfs/archive/4a4bf858fc158668bf81c7e206799a5f6f99d6c3.png@672w_378h_1c.webp
			// 这就可以把@后面的全部去掉
			var lastIndexOfAt = textOrLink.LastIndexOf('@');
			if (lastIndexOfAt != -1) {
				textOrLink = textOrLink[..lastIndexOfAt];
			}
			var lastIndexOfQm = textOrLink.LastIndexOf('?');
			if (lastIndexOfQm != -1) {
				textOrLink = textOrLink[..lastIndexOfQm];
			}
			var lastIndexOfDot = textOrLink.LastIndexOf('.');
			var lastIndexOfSlash = textOrLink.LastIndexOf('/');
			if (lastIndexOfSlash > lastIndexOfDot) {
				// 说明是链接，不是网络图像
				SaveAsLink = true;
				Url = Text = textOrLink;
				SaveFileName = textOrLink[(lastIndexOfSlash + 1)..] + ".url";
				SaveFileNameTextBox.SelectionLength = SaveFileName.Length - 4;
			} else {
				var extension = textOrLink[lastIndexOfDot..];
				SaveFileName = textOrLink[(lastIndexOfSlash + 1)..];
				if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp") {
					imageExtension = extension;
					SaveAsImage = true;
					Url = textOrLink;
					LoadImage();
				}
			}
		} else {
			Text = textOrLink;
			SaveFileName = (textOrLink.Length > 8 ? textOrLink[..7] : textOrLink) + ".txt";
			SaveFileNameTextBox.SelectionLength = textOrLink.Length > 8 ? 8 : textOrLink.Length;
			CanSave = true;
		}
		SaveCommand = new SimpleCommand(Save);
	}

	private void LoadImage() {
		Task.Run(() => {
			try {
				var response = new HttpClient().GetAsync(Url).Result;
				response.EnsureSuccessStatusCode();
				imageSize = response.Content.Headers.ContentLength.GetValueOrDefault(-1);
				MemoryStream ms;
				if (imageSize == -1) {
					IsIndeterminate = true;
					ms = new MemoryStream();
				} else {
					ms = new MemoryStream(new byte[imageSize]);
				}
				using var stream = response.Content.ReadAsStream();
				var buf = new byte[10240];
				int length;
				do {
					length = stream.Read(buf, 0, buf.Length);
					ms.Write(buf, 0, length);
					if (imageSize != -1) {
						ImageDownloadProgress = 100d * ms.Position / imageSize;
					}
				} while (length != 0);
				ms.Position = 0;
				var image = new BitmapImage();
				image.BeginInit();
				image.CacheOption = BitmapCacheOption.OnLoad;
				image.StreamSource = ms;
				image.EndInit();
				ms.Dispose();
				image.Freeze();
				Image = image;
				CanSave = true;
			} catch (Exception e) {
				Logger.Exception(e);
			}
		});
	}

	private void Save(object _) {
		if (Image != null) {
			BitmapEncoder encoder = Path.GetExtension(SaveFileName) switch {
				".png" => new PngBitmapEncoder(),
				".bmp" => new BmpBitmapEncoder(),
				".jpg" => new JpegBitmapEncoder(),
				".jpeg" => new JpegBitmapEncoder(),
				".gif" => new GifBitmapEncoder(),
				_ => new BmpBitmapEncoder()
			};
			encoder.Frames.Add(BitmapFrame.Create(Image));
			try {
				using var fs = new FileStream(Path.Combine(basePath, SaveFileName!), FileMode.Create);
				encoder.Save(fs);
			} catch (Exception e) {
				Logger.Exception(e);
			}
		} else if (Url != null) {
			using var writer = new StreamWriter(Path.Combine(basePath, SaveFileName!));
			writer.WriteLine("[InternetShortcut]");
			writer.WriteLine("URL=" + Url);
		}
		Close();
	}
}