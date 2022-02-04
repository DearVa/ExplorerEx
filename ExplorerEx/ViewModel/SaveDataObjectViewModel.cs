using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ExplorerEx.Utils;
using ExplorerEx.View;

namespace ExplorerEx.ViewModel;

public class SaveDataObjectViewModel : ViewModelBase {
	public string Text { get; set; }

	public string Url { get; }

	public BitmapImage Image { get; private set; }

	public double ImageDownloadProgress { get; private set; }

	public bool IsIndeterminate { get; private set; }

	public string SaveFileName { get; set; }

	public bool CanSave { get; private set; }

	public bool SaveAsImage {
		get => saveAsImage;
		set {
			if (saveAsImage != value) {
				saveAsImage = value;
				if (value) {
					SaveAsLink = false;
					SaveAsText = false;
					if (imageExtension != null) {
						SaveFileName = Path.ChangeExtension(SaveFileName, imageExtension);
						OnPropertyChanged(nameof(SaveFileName));
					}
				}
				OnPropertyChanged();
			}
		}
	}
	private bool saveAsImage;

	public bool SaveAsLink {
		get => saveAsLink;
		set {
			if (saveAsLink != value) {
				saveAsLink = value;
				if (value) {
					SaveAsImage = false;
					SaveAsText = false;
					SaveFileName = Path.ChangeExtension(SaveFileName, ".url");
					OnPropertyChanged(nameof(SaveFileName));
				}
				OnPropertyChanged();
			}
		}
	}
	private bool saveAsLink;

	public bool SaveAsText {
		get => saveAsText;
		set {
			if (saveAsText != value) {
				saveAsText = value;
				if (value) {
					SaveAsImage = false;
					SaveAsLink = false;
				}
				OnPropertyChanged();
			}
		}
	}
	private bool saveAsText;

	public SimpleCommand SaveCommand { get; }

	private readonly SaveDataObjectWindow window;

	private readonly string basePath;

	private long imageSize;

	private readonly string imageExtension;

	public SaveDataObjectViewModel(SaveDataObjectWindow window, string basePath, string textOrLink) {
		this.window = window;
		this.basePath = basePath;
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
			if (lastIndexOfDot == -1 || lastIndexOfSlash > lastIndexOfDot) {
				// 说明是链接，不是网络图像
				SaveAsLink = true;
				Url = Text = textOrLink;
				SaveFileName = textOrLink[(lastIndexOfSlash + 1)..] + ".url";
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
			//window.SaveFileNameTextBox.Text = SaveFileName;
			//window.SaveFileNameTextBox.SelectionLength = textOrLink.Length - lastIndexOfSlash;
		} else {
			Text = textOrLink;
			SaveFileName = (textOrLink.Length > 8 ? textOrLink[..7] : textOrLink) + ".txt";
			//window.SaveFileNameTextBox.Text = SaveFileName;
			//window.SaveFileNameTextBox.SelectionLength = textOrLink.Length > 8 ? 8 : textOrLink.Length;
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
					OnPropertyChanged(nameof(IsIndeterminate));
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
						OnPropertyChanged(nameof(ImageDownloadProgress));
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
				OnPropertyChanged(nameof(Image));
				OnPropertyChanged(nameof(CanSave));
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
		window.Close();
	}
}