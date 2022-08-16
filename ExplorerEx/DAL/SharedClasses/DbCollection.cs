using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ExplorerEx.DAL.SharedClasses;

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