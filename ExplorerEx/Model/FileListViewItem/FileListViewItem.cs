using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using ExplorerEx.Database;
using ExplorerEx.Database.Interface;
using ExplorerEx.Database.Shared;
using ExplorerEx.Utils;
using ExplorerEx.Utils.Collections;
using ExplorerEx.View.Controls;

namespace ExplorerEx.Model;

/// <summary>
/// 所有可以显示在<see cref="FileListView"/>中的项目的基类
/// </summary>
public abstract class FileListViewItem : INotifyPropertyChanged {
	protected FileListViewItem(string fullPath, string name, bool isFolder) {
		FullPath = fullPath;
		Name = name;
		defaultIcon = isFolder ? IconHelper.FolderDrawingImage : IconHelper.UnknownFileDrawingImage;
		This = this;
	}

	protected FileListViewItem(string fullPath, string name, ImageSource defaultIcon) {
		FullPath = fullPath;
		Name = name;
		this.defaultIcon = defaultIcon;
		This = this;
	}

	/// <summary>
	/// 图标，自动更新UI
	/// </summary>
	public ImageSource? Icon {
		get {
			if (icon != null) {
				return icon;
			}
			Task.Run(() => LoadIcon(LoadDetailsOptions.Default));
			return defaultIcon;
		}
		protected set {
			if (icon != value) {
				icon = value;
				OnPropertyChanged();
			}
		}
	}

	private ImageSource? icon;

	protected readonly ImageSource defaultIcon;

	
	public double Opacity {
		get => opacity;
		set {
			if (opacity != value) {
				opacity = value;
				OnPropertyChanged();
			}
		}
	}

	private double opacity = 1d;

	[DbColumn(IsPrimaryKey = true)]
	public string FullPath { get; protected init; }
	
	public abstract string DisplayText { get; }

	[DbColumn]
	public virtual string Name { get; set; }

	/// <summary>
	/// 类型描述，自动更新UI
	/// </summary>
	public string? Type {
		get => type;
		protected set {
			if (type != value) {
				type = value;
				OnPropertyChanged();
			}
		}
	}

	private string? type;
	
	public long FileSize {
		get => fileSize;
		protected set {
			if (fileSize != value) {
				fileSize = value;
				OnPropertyChanged();
			}
		}
	}

	private long fileSize;
	
	public bool IsFolder { get; protected set; }

	public bool IsBookmarked => ConfigHelper.Container.Resolve<IBookmarkDbContext>().GetBookmarkItems().Any(b => b.FullPath == FullPath);
	
	public bool IsSelected {
		get => isSelected;
		set {
			if (isSelected != value) {
				isSelected = value;
				OnPropertyChanged();
			}
		}
	}

	private bool isSelected;

	/// <summary>
	/// 加载文件的各项属性
	/// </summary>
	public abstract void LoadAttributes(LoadDetailsOptions options);

	/// <summary>
	/// LoadIcon用到了shell，那并不是一个可以多线程的方法，所以与其每次都Task.Run，不如提高粗粒度
	/// </summary>
	public abstract void LoadIcon(LoadDetailsOptions options);

	#region 文件重命名
	/// <summary>
	/// 获取刚开始重命名时的文件名，如果失败，返回null
	/// </summary>
	/// <returns></returns>
	public abstract string? GetRenameName();

	/// <summary>
	/// 重命名
	/// </summary>
	public bool Rename(string newName) {
		if (string.IsNullOrWhiteSpace(newName)) {
			return false;
		}
		newName = newName.Trim();
		if (!FileUtils.IsProhibitedFileName(newName)) {
			if (newName != Name && InternalRename(newName)) {
				Name = newName;
				OnPropertyChanged(nameof(Name));
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// 重命名
	/// <returns>成功返回true</returns>
	/// </summary>
	protected abstract bool InternalRename(string newName);

	#endregion

	public override string ToString() {
		return FullPath;
	}

	public override int GetHashCode() {
		return FullPath.GetHashCode();
	}

	public override bool Equals(object? other) {
		return other is FileListViewItem i && i.FullPath == FullPath;
	}

	public bool Equals(FileListViewItem other) {
		return other.FullPath == FullPath;
	}

	/// <summary>
	/// 加载详细信息，包括文件大小、类型、图标等
	/// </summary>
	/// <param name="list"></param>
	/// <param name="token"></param>
	/// <param name="options"></param>
	/// <returns></returns>
	public static async Task LoadDetails(IReadOnlyCollection<FileListViewItem>? list, LoadDetailsOptions options, CancellationToken token) {
		if (list is { Count: > 0 }) {
			try {
				var tasks = Partitioner.Create(list).GetPartitions(Math.Max(App.ProcessorCount, 2)).Select(partition => Task.Run(() => {
					if (token.IsCancellationRequested) {
						return Task.FromCanceled(token);
					}
					using (partition) {
						while (partition.MoveNext()) {
							if (token.IsCancellationRequested) {
								return Task.FromCanceled(token);
							}

							partition.Current.LoadAttributes(options);
						}
					}

					return Task.CompletedTask;
				}, token));

				if (options.PreLoadIcon) {
					tasks = tasks.Append(Task.Run(() => {
						foreach (var item in list) {
							if (token.IsCancellationRequested) {
								return Task.FromCanceled(token);
							}

							item.LoadIcon(options);
						}

						return Task.CompletedTask;
					}, token));
				}

				await Task.WhenAll(tasks);
			} catch (TaskCanceledException) {
				// Ignore
			} catch (Exception e) {
				Logger.Exception(e);
			}
		}
	}


	/// <summary>
	/// 加载详细信息时的设置，例如是否使用大图标
	/// </summary>
	public class LoadDetailsOptions {
		public static LoadDetailsOptions Default { get; } = new() {
			PreLoadIcon = true,
			UseLargeIcon = false
		};

		public bool PreLoadIcon { get; set; }

		public bool UseLargeIcon { get; set; }

		public void SetPreLoadIconByItemCount(int count) {
			PreLoadIcon = count < 20 * App.ProcessorCount;
		}
	}

	/// <summary>
	/// 用于更新绑定到自身的东西
	/// </summary>
	public FileListViewItem This { get; }

	public event PropertyChangedEventHandler? PropertyChanged;

	public void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(This)));
	}
}

public class FileItemAttach {
	public static readonly DependencyProperty FileItemProperty = DependencyProperty.RegisterAttached(
		"FileItem", typeof(FileListViewItem), typeof(FileItemAttach), new PropertyMetadata(default(FileListViewItem)));

	public static void SetFileItem(DependencyObject element, FileListViewItem value) {
		element.SetValue(FileItemProperty, value);
	}

	public static FileListViewItem GetFileItem(DependencyObject element) {
		return (FileListViewItem)element.GetValue(FileItemProperty);
	}
}
