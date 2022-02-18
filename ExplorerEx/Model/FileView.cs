using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using ExplorerEx.Utils;
using ExplorerEx.View.Controls;
using Microsoft.EntityFrameworkCore;

namespace ExplorerEx.Model;

/// <summary>
/// 文件视图类型
/// </summary>
public enum FileViewType {
	/// <summary>
	/// 图标，表现为WarpPanel，每个小格上边是缩略图下边是文件名
	/// </summary>
	Icon,
	/// <summary>
	/// 列表，表现为WarpPanel，每个小格左边是缩略图右边是文件名
	/// </summary>
	List,
	/// <summary>
	/// 详细信息，表现为DataGrid，上面有Header，一列一列的
	/// </summary>
	Detail,
	/// <summary>
	/// 平铺，表现为WarpPanel，每个小格左边是缩略图右边从上到下依次是文件名、文件类型描述、文件大小
	/// </summary>
	Tile,
	/// <summary>
	/// 内容，表现为DataGrid，但Header不可见，左边是图标、文件名、大小，右边是详细信息
	/// </summary>
	Content
}

/// <summary>
/// 选择详细信息中的显示列
/// </summary>
public enum DetailLists : byte {
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
	DeleteDate,

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
	ModificationDate,
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

public class DetailList : IByteCodec {
	public DetailLists List { get; set; }

	public double Width { get; set; }

	private static readonly DetailList[] DefaultHomeDetailLists = {
		new(DetailLists.Name, 300),
		new(DetailLists.Type, 100),
		new(DetailLists.AvailableSpace, 100),
		new(DetailLists.TotalSpace, 100),
		new(DetailLists.FillRatio, 150),
		new(DetailLists.FileSystem, 100)
	};

	private static readonly DetailList[] DefaultNormalDetailLists = {
		new(DetailLists.Name, 300),
		new(DetailLists.ModificationDate, 200),
		new(DetailLists.Type, 100),
		new(DetailLists.FileSize, 100)
	};

	private static readonly DetailList[] DefaultRecycleBinDetailLists = {
		new(DetailLists.Name, 300),
		new(DetailLists.OriginalLocation, 300),
		new(DetailLists.DeleteDate, 100),
		new(DetailLists.FileSize, 100),
		new(DetailLists.Type, 100),
		new(DetailLists.ModificationDate, 100)
	};

	private static readonly DetailList[] DefaultSearchDetailLists = {
		new(DetailLists.Name, 300),
		new(DetailLists.ModificationDate, 100),
		new(DetailLists.Type, 100),
		new(DetailLists.FileSize, 100),
		new(DetailLists.Folder, 300)
	};

	public static DetailList[] GetDefaultLists(PathType pathType) {
		return pathType switch {
			PathType.Home => DefaultHomeDetailLists,
			PathType.Normal => DefaultNormalDetailLists,
			PathType.RecycleBin => DefaultRecycleBinDetailLists,
			PathType.Search => DefaultSearchDetailLists,
			_ => DefaultNormalDetailLists
		};
	}

	public DetailList() { }

	public DetailList(DetailLists list, double width) {
		List = list;
		Width = width;
	}

	internal void Deconstruct(out DetailLists list, out double width) {
		list = List;
		width = Width;
	}

	public int Length => sizeof(DetailLists) + sizeof(double);

	public void Encode(Span<byte> buf) {
		buf[0] = (byte)List;
		var width = BitConverter.GetBytes(Width);
		buf[1] = width[0];
		buf[2] = width[1];
	}

	public void Decode(ReadOnlySpan<byte> buf) {
		List = (DetailLists)buf[0];
		Width = BitConverter.ToDouble(buf.Slice(1, 2));
	}
}

/// <summary>
/// 当前路径的类型
/// </summary>
public enum PathType {
	/// <summary>
	/// 首页，“此电脑”
	/// </summary>
	Home,
	/// <summary>
	/// 普通，即浏览正常文件
	/// </summary>
	Normal,
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
}

/// <summary>
/// 记录一个文件夹的视图状态，即排序方式、分组依据和查看类型
/// </summary>
[Serializable]
public class FileView {
	[Key]
	public string FullPath { get; set; }

	public FileViewType FileViewType { get; set; }

	[NotMapped]
	public Size ItemSize {
		get => new(ItemWidth, ItemWHeight);
		set {
			ItemWidth = value.Width;
			ItemWHeight = value.Height;
		}
	}

	public double ItemWidth { get; set; }

	public double ItemWHeight { get; set; }

	[NotMapped]
	public List<DetailList> DetailLists {
		get => DecodeData();
		set => EncodeData(value);
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

	public async Task LoadOrMigrateAsync() {
		try {
			await Database.EnsureCreatedAsync();
			await FolderViewDbSet.LoadAsync();
		} catch {
			await Database.MigrateAsync();
			await FolderViewDbSet.LoadAsync();
		}
	}

	protected override void OnConfiguring(DbContextOptionsBuilder ob) {
		ob.UseSqlite($"Data Source={dbPath}");
	}
}