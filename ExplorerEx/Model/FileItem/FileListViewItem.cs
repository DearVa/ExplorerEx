using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using ExplorerEx.Utils;
using ExplorerEx.View.Controls;
using HandyControl.Data;

namespace ExplorerEx.Model; 

/// <summary>
/// 所有可以显示在<see cref="FileListView"/>中的项目的基类
/// </summary>
public abstract class FileListViewItem : SimpleNotifyPropertyChanged {
	/// <summary>
	/// 图标，自动更新UI
	/// </summary>
	[NotMapped]
	public ImageSource Icon {
		get => icon;
		protected set {
			if (icon != value) {
				icon = value;
				UpdateUI();
			}
		}
	}

	private ImageSource icon;

	[Key]
	public string FullPath { get; protected set; }

	public abstract string DisplayText { get; }

	public string Name { get; set; }

	/// <summary>
	/// 类型描述，自动更新UI
	/// </summary>
	[NotMapped]
	public string Type {
		get => type;
		protected set {
			if (type != value) {
				type = value;
				UpdateUI();
			}
		}
	}

	private string type;

	[NotMapped]
	public long FileSize {
		get => fileSize;
		protected set {
			if (fileSize != value) {
				fileSize = value;
				UpdateUI();
			}
		}
	}

	private long fileSize;

	[NotMapped]
	public bool IsFolder { get; protected set; }

	public bool IsBookmarked => BookmarkDbContext.Instance.BookmarkDbSet.Any(b => b.FullPath == FullPath);

	[NotMapped]
	public bool IsSelected {
		get => isSelected;
		set {
			if (isSelected != value) {
				isSelected = value;
				UpdateUI();
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
	/// 获取刚开始重命名时的文件名
	/// </summary>
	/// <returns></returns>
	public abstract string GetRenameName();

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
				UpdateUI(nameof(Name));
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

	/// <summary>
	/// 加载详细信息，包括文件大小、类型、图标等
	/// </summary>
	/// <param name="list"></param>
	/// <param name="token"></param>
	/// <param name="options"></param>
	/// <returns></returns>
	public static async Task LoadDetails(IReadOnlyCollection<FileListViewItem> list, CancellationToken token, LoadDetailsOptions options) {
		try {
			if (list.Count > 0) {
				await Task.WhenAll(Partitioner.Create(list).GetPartitions(4).Select(partition => Task.Run(() => {
					if (token.IsCancellationRequested) {
						return Task.FromCanceled(token);
					}
					using (partition) {
						while (partition.MoveNext()) {
							var item = partition.Current!;
							item.LoadAttributes(options);
							item.LoadIcon(options);

							if (token.IsCancellationRequested) {
								return Task.FromCanceled(token);
							}
						}
					}

					return Task.CompletedTask;
				}, token)));
			}
		} catch (TaskCanceledException) {
			// Ignore
		} catch (Exception e) {
			Logger.Exception(e);
		}
	}


	/// <summary>
	/// 加载详细信息时的设置，例如是否使用大图标
	/// </summary>
	public class LoadDetailsOptions {
		public static LoadDetailsOptions Default { get; } = new() {
			UseLargeIcon = false
		};

		public bool UseLargeIcon { get; set; }
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
