using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ExplorerEx.Database;

/// <summary>
/// 这个对应一个数据库文件或者一个数据源
/// </summary>
public interface IDatabase {
	/// <summary>
	/// 负责对数据库加载的异常进行处理，并处理特殊情况（如首次加载、错误数据的修正等等）
	/// </summary>
	/// <returns></returns>
	public Task LoadAsync();

	public Task SaveChangesAsync();
}

/// <summary>
/// 代表一个可与数据库对接的集合，同时还实现INotify接口可以直接绑定到View
/// </summary>
public abstract class DbCollection<TEntity> : ICollection<TEntity>, INotifyPropertyChanged, INotifyCollectionChanged where TEntity : class {
	protected readonly IDatabase database;
	public event PropertyChangedEventHandler? PropertyChanged;
	public event NotifyCollectionChangedEventHandler? CollectionChanged;

	protected DbCollection(IDatabase database) {
		this.database = database;
	}

	public abstract void Add(TEntity item);
	public abstract void Clear();
	public abstract bool Contains(TEntity item);
	public abstract void CopyTo(TEntity[] array, int arrayIndex);
	public abstract int Count { get; }
	public abstract bool IsReadOnly { get; }
	public abstract bool Remove(TEntity item);
	public abstract Task LoadAsync();
	public abstract Task SaveChangesAsync();
	public abstract IEnumerator<TEntity> GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() {
		return GetEnumerator();
	}
	protected void OnPropertyChanged(string propertyName) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
	protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
		CollectionChanged?.Invoke(this, e);
	}
}

[AttributeUsage(AttributeTargets.Class)]
public class DbTable : Attribute {
	public string? TableName { get; set; }

	public string? EntityName { get; set; }
}

/// <summary>
/// 实体中的属性带有这个Attribute的才会被记录进数据库，默认不记录
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DbColumn : Attribute {
	/// <summary>
	/// 是否为主键
	/// </summary>
	public bool IsPrimaryKey { get; set; }

	/// <summary>
	/// 是否自增
	/// </summary>
	public bool IsIdentity { get; set; }

	/// <summary>
	/// 指定存储时的列名
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// 说明是一个映射
	/// </summary>
	public string? NavigateTo { get; set; }

	public DbColumnNavigateType NavigateType { get; set; }
}

public enum DbColumnNavigateType {
	NoNavigate,
	OneToOne,
	OneToMany,
	ManyToOne,
	ManyToMany,
}