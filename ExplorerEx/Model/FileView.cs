using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using ExplorerEx.Annotations;
using ExplorerEx.Utils;
using Microsoft.EntityFrameworkCore;

namespace ExplorerEx.Model;

/// <summary>
/// 文件视图类型
/// </summary>
public enum FileViewType {
	/// <summary>
	/// 图标，表现为WarpPanel，每个小格上边是缩略图下边是文件名
	/// </summary>
	Icons,
	/// <summary>
	/// 列表，表现为WarpPanel，每个小格左边是缩略图右边是文件名
	/// </summary>
	List,
	/// <summary>
	/// 详细信息，表现为DataGrid，上面有Header，一列一列的
	/// </summary>
	Details,
	/// <summary>
	/// 平铺，表现为WarpPanel，每个小格左边是缩略图右边从上到下依次是文件名、文件类型描述、文件大小
	/// </summary>
	Tiles,
	/// <summary>
	/// 内容，表现为DataGrid，但Header不可见，左边是图标、文件名、大小，右边是详细信息
	/// </summary>
	Content
}

/// <summary>
/// 选择详细信息中的显示列
/// </summary>
public enum DetailListType : byte {
	/// <summary>
	/// 名称
	/// </summary>
	Name,

	#region 主页

	/// <summary>
	/// 可用空间
	/// </summary>
	AvailableSpace,
	/// <summary>
	/// 总大小
	/// </summary>
	TotalSpace,
	/// <summary>
	/// 文件系统（NTFS、FAT）
	/// </summary>
	FileSystem,
	/// <summary>
	/// 填充的百分比
	/// </summary>
	FillRatio,

	#endregion

	#region 回收站

	/// <summary>
	/// 原位置
	/// </summary>
	OriginalLocation,
	/// <summary>
	/// 删除日期
	/// </summary>
	DateDeleted,

	#endregion

	#region 搜索

	/// <summary>
	/// 所在文件夹
	/// </summary>
	Folder,

	#endregion

	/// <summary>
	/// 修改日期
	/// </summary>
	DateModified,
	/// <summary>
	/// 类型
	/// </summary>
	Type,
	/// <summary>
	/// 文件大小
	/// </summary>
	FileSize,
	/// <summary>
	/// 创建日期
	/// </summary>
	CreationDate,
}

/// <summary>
/// 详细信息中的一列，包括列的类型和宽度
/// </summary>
public class DetailList : IByteCodec {
	public DetailListType ListType { get; set; }

	/// <summary>
	/// 列宽，小于0表示自动宽度
	/// </summary>
	public double Width { get; set; }

	private static readonly DetailList[] DefaultHomeDetailLists = {
		new(DetailListType.Name, 300),
		new(DetailListType.Type, 100),
		new(DetailListType.AvailableSpace, 100),
		new(DetailListType.TotalSpace, 100),
		new(DetailListType.FillRatio, 150),
		new(DetailListType.FileSystem, 100)
	};

	private static readonly DetailList[] DefaultLocalFolderDetailLists = {
		new(DetailListType.Name, 300),
		new(DetailListType.DateModified, 200),
		new(DetailListType.Type, 200),
		new(DetailListType.FileSize, 100)
	};

	private static readonly DetailList[] DefaultRecycleBinDetailLists = {
		new(DetailListType.Name, 300),
		new(DetailListType.OriginalLocation, 300),
		new(DetailListType.DateDeleted, 100),
		new(DetailListType.FileSize, 100),
		new(DetailListType.Type, 200),
		new(DetailListType.DateModified, 100)
	};

	private static readonly DetailList[] DefaultSearchDetailLists = {
		new(DetailListType.Name, 300),
		new(DetailListType.DateModified, 100),
		new(DetailListType.Type, 200),
		new(DetailListType.FileSize, 100),
		new(DetailListType.Folder, 300)
	};

	public static DetailList[] GetDefaultLists(PathType pathType) {
		return pathType switch {
			PathType.Home => DefaultHomeDetailLists,
			PathType.LocalFolder => DefaultLocalFolderDetailLists,
			PathType.RecycleBin => DefaultRecycleBinDetailLists,
			PathType.Search => DefaultSearchDetailLists,
			_ => DefaultLocalFolderDetailLists
		};
	}

	public DetailList() { }

	public DetailList(DetailListType listType, double width) {
		ListType = listType;
		Width = width;
	}

	internal void Deconstruct(out DetailListType listType, out double width) {
		listType = ListType;
		width = Width;
	}

	public int Length => sizeof(DetailListType) + sizeof(double);

	public void Encode(Span<byte> buf) {
		buf[0] = (byte)ListType;
		var width = BitConverter.GetBytes(Width);
		buf[1] = width[0];
		buf[2] = width[1];
	}

	public void Decode(ReadOnlySpan<byte> buf) {
		ListType = (DetailListType)buf[0];
		Width = BitConverter.ToDouble(buf.Slice(1, 2));
	}
}

/// <summary>
/// 当前路径的类型
/// </summary>
public enum PathType {
	Unknown,
	/// <summary>
	/// 首页，“此电脑”
	/// </summary>
	Home,
	/// <summary>
	/// 本地文件夹
	/// </summary>
	LocalFolder,
	/// <summary>
	/// 本地文件，仅在传参时使用
	/// </summary>
	LocalFile,
	/// <summary>
	/// 回收站
	/// </summary>
	RecycleBin,
	/// <summary>
	/// 搜索文件的结果
	/// </summary>
	Search,
	/// <summary>
	/// 网络驱动器
	/// </summary>
	NetworkDisk,
	OneDrive,
	/// <summary>
	/// 在一个压缩文件里
	/// </summary>
	Zip,
	Other
}

/// <summary>
/// 记录一个文件夹的视图状态，即排序方式、分组依据和查看类型，还包括详细信息的列的项目大小
/// </summary>
[Serializable]
public class FileView : INotifyPropertyChanged {
	private readonly List<string> changedPropertiesName = new();

	[Key]
	public string FullPath { 
        get => fullPath;
        set {
            if (fullPath != value) {
                fullPath = value;
                StageChange();
            }
        }
    }
    private string fullPath;

	[NotMapped]
	public PathType PathType {
        get => pathType;
        set {
            if (pathType != value) {
                pathType = value;
				StageChange();
			}
        }
    }
    private PathType pathType;

	public DetailListType SortBy {
        get => sortBy;
        set {
            if (sortBy != value) {
                sortBy = value;
                StageChange();
				UpdateUI(nameof(SortByIndex));
            }
        }
    }
    private DetailListType sortBy;

    public int SortByIndex => SortBy switch {
	    DetailListType.Name => 0,
	    DetailListType.DateModified => 1,
	    DetailListType.Type => 2,
	    DetailListType.FileSize => 3,
	    _ => 0
    };

	public bool IsAscending {
        get => isAscending;
        set {
            if (isAscending != value) {
                isAscending = value;
                StageChange();
				UpdateUI(nameof(IsAscending));
            }
        }
    }
    private bool isAscending;

	public DetailListType? GroupBy { 
        get => groupBy;
        set {
            if (groupBy != value) {
                groupBy = value;
				UpdateUI();
				UpdateUI(nameof(GroupByIndex));
			}
        }
    }
    private DetailListType? groupBy;

    public int GroupByIndex => GroupBy switch {
	    DetailListType.Name => 0,
	    DetailListType.DateModified => 1,
	    DetailListType.Type => 2,
	    DetailListType.FileSize => 3,
	    _ => -1
    };

	public FileViewType FileViewType { 
        get => fileViewType;
        set {
            if (fileViewType != value) {
                fileViewType = value;
				StageChange();
				UpdateUI(nameof(FileViewTypeIndex));
			}
        }
    }
    private FileViewType fileViewType;

    /// <summary>
    /// 用于绑定到下拉按钮
    /// </summary>
    public int FileViewTypeIndex => FileViewType switch {
	    FileViewType.Icons when ItemSize.Width > 100d && ItemSize.Height > 130d => 0,
	    FileViewType.Icons => 1,
	    FileViewType.List => 2,
	    FileViewType.Details => 3,
	    FileViewType.Tiles => 4,
	    FileViewType.Content => 5,
	    _ => -1
    };

	[NotMapped]
	public Size ItemSize {
		get => new(ItemWidth, ItemHeight);
		set {
			ItemWidth = value.Width;
			ItemHeight = value.Height;
			StageChange();
		}
	}

	public double ItemWidth { get; set; }

	public double ItemHeight { get; set; }

	[NotMapped]
	public List<DetailList> DetailLists {
		get => DecodeData();
		set {
            EncodeData(value);
			StageChange();
		}
	}

	public byte[] DetailListsData { get; set; }

	private void EncodeData(IReadOnlyCollection<DetailList> detailLists) {
		if (detailLists == null || detailLists.Count == 0) {
			DetailListsData = null;
		} else {
			DetailListsData = new byte[detailLists.Sum(l => l.Length)];
			var index = 0;
			foreach (var detailList in detailLists) {
				detailList.Encode(DetailListsData.AsSpan(index, detailList.Length));
				index += detailList.Length;
			}
		}
	}

	private List<DetailList> DecodeData() {
		if (DetailListsData == null || DetailListsData.Length == 0) {
			return null;
		}
		var detailLists = new List<DetailList>();
		var index = 0;
		while (index < DetailListsData.Length) {
			var list = new DetailList();
			list.Decode(DetailListsData.AsSpan(index, list.Length));
			detailLists.Add(list);
			index += list.Length;
		}
		return detailLists;
	}

	public event Action Changed;

	public void CommitChange() {
		Changed?.Invoke();
		lock (changedPropertiesName) {
			for (var i = changedPropertiesName.Count - 1; i >= 0; i--) {
				UpdateUI(changedPropertiesName[i]);
				changedPropertiesName.RemoveAt(i);
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected virtual void UpdateUI([CallerMemberName] string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	/// <summary>
	/// 暂存更改
	/// </summary>
	/// <param name="propertyName"></param>
	private void StageChange([CallerMemberName] string propertyName = null) {
		lock (changedPropertiesName) {
			if (!changedPropertiesName.Contains(propertyName)) {
				changedPropertiesName.Add(propertyName);
			}
		}
	}
}

public class FileViewDbContext : DbContext {
#pragma warning disable CS0612
	public static FileViewDbContext Instance { get; } = new();
#pragma warning restore CS0612
	public DbSet<FileView> FolderViewDbSet { get; set; }

	private readonly string dbPath;

	/// <summary>
	/// 之所以用public是因为需要迁移，但是*请勿*使用该构造方法，应该使用Instance
	/// </summary>
#pragma warning disable CA1041
	[Obsolete]
#pragma warning restore CA1041
	public FileViewDbContext() {
		var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data");
		if (!Directory.Exists(path)) {
			Directory.CreateDirectory(path);
		}
		dbPath = Path.Combine(path, "FileViews.db");
	}

	public async Task LoadDataBase() {
		try {
			await Database.EnsureCreatedAsync();
			await FolderViewDbSet.LoadAsync();
		} catch (Exception e) {
			MessageBox.Show("无法加载数据库，可能是权限不够或者数据库版本过旧，请删除Data文件夹后再试一次。\n错误为：" + e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
			Logger.Exception(e, false);
		}
	}

	protected override void OnConfiguring(DbContextOptionsBuilder ob) {
		ob.UseSqlite($"Data Source={dbPath}");
	}
}