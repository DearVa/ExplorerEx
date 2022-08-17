using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using ExplorerEx.Database.Interface;

namespace ExplorerEx.Database.Shared;

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
	/// 存储时的最大长度
	/// </summary>
	public int MaxLength { get; set; } = -1;

	/// <summary>
	/// 说明是一个映射，和IsPrimaryKey相冲突
	/// </summary>
	public string? NavigateTo { get; set; }

	public DbNavigateType NavigateType { get; set; }

	public DbColumn() { }

	public DbColumn(string navigateTo, DbNavigateType navigateType) {
		NavigateTo = navigateTo;
		NavigateType = navigateType;
	}
}

public enum DbNavigateType {
	/// <summary>
	/// 默认值，说明不是映射
	/// </summary>
	NoNavigate,
	/// <summary>
	/// 一对一映射
	/// <remarks>
	///	<para>
	/// 用法1，小蝌蚪找妈妈
	///	<code>
	///	class Parent {
	///		public int Id { get; set; }
	///
	///		[DbColumn(nameof(Child.ParentId), DbNavigateType.OneToOne)]
	///		public Child Child { get; set; }
	///	}
	///
	///	class Child {
	///		public int Id { get; set; }
	///		public int ParentId { get; set; }
	///	} 
	///	</code>
	///	</para>
	///
	///	<para>
	///	用法2，妈妈找小蝌蚪
	///	<code>
	///	class Parent {
	///		public int Id { get; set; }
	///
	///		public int ChildId { get; set; }
	///
	///		[DbColumn(nameof(ChildId), DbNavigateType.OneToOne)]
	///		public Child Child { get; set; }
	///	}
	///
	///	class Child {
	///		public int Id { get; set; }
	///	} 
	///	</code>
	///	</para> 
	/// </remarks>
	/// 
	/// </summary>
	OneToOne,
	OneToMany,
	ManyToOne,
	ManyToMany,
}