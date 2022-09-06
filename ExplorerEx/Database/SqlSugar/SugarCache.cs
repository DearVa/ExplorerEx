using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using SqlSugar;

namespace ExplorerEx.Database.SqlSugar;

/// <summary>
/// 类型缓存的基类
/// </summary>
public abstract class SugarCacheBase {
	private static readonly Dictionary<Type, SugarCacheBase> Caches = new();

	protected bool IsLoaded;

	protected SugarCacheBase(Type type) {
		if (!type.IsClass || Caches.ContainsKey(type)) {
			throw new ArgumentOutOfRangeException(nameof(type));
		}
		Caches.Add(type, this);
	}
}

/// <summary>
/// 含有依赖关系的初始化策略
/// </summary>
/// <typeparam name="TSelf"></typeparam>
/// <typeparam name="TSub"></typeparam>
public class SugarStrategy<TSelf, TSub>
	where TSelf : class, new()
	where TSub : class, INotifyPropertyChanged, new() {

	public Action<TSelf, TSub> Action { get; }
	public SugarCache<TSub> Source { get; }

	public SugarStrategy(SugarCache<TSub> source, Action<TSelf, TSub> action) {
		Source = source;
		Action = action;
	}
}

/// <summary>
/// 需要注意的是 添加到缓存的泛型 需要实现<see cref="INotifyPropertyChanged"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public class SugarCache<T> : SugarCacheBase where T : class, INotifyPropertyChanged, new() {
	protected readonly ISqlSugarClient db;

	/// <summary>
	/// 目前存在于本地的实体
	/// </summary>
	protected readonly HashSet<T> locals = new();

	/// <summary>
	/// 新添加的实体
	/// </summary>
	protected readonly HashSet<T> adds = new();
	protected readonly HashSet<T> changes = new();
	protected readonly HashSet<T> deletes = new();

	private readonly object lockObj = new();

	public SugarCache(ISqlSugarClient db) : base(typeof(T)) {
		this.db = db;
	}

	public void LoadDatabase() {
		if (!IsLoaded) {
			db.Queryable<T>().ForEach(x => {
				x.PropertyChanged += MarkAsChanged;
				locals.Add(x);
			});
			IsLoaded = true;
		}
	}

	public T? FirstOrDefault(Func<T, bool> match) => locals.FirstOrDefault(match);

	public bool Contains(T item) => locals.Contains(item);

	public bool Any(Func<T, bool> match) => locals.Any(match);

	public void Add(T item) {
		lock (lockObj) {
			if (!locals.Contains(item)) {
				item.PropertyChanged += MarkAsChanged;
				adds.Add(item);
				locals.Add(item);
			}
		}
	}

	public void Remove(T item) {
		lock (lockObj) {
			if (!adds.Remove(item) && locals.Remove(item)) {
				changes.Remove(item);
				deletes.Add(item);
			}
		}
	}

	public int Count() => locals.Count;

	public void Save() {
		lock (lockObj) {
			foreach (var add in adds) {
				db.Insertable(add).AddQueue();
			}
			adds.Clear();
			foreach (var change in changes) {
				db.Updateable(change).AddQueue();
			}
			changes.Clear();
			foreach (var delete in deletes) {
				db.Deleteable(delete).AddQueue();
			}
			deletes.Clear();
		}
		db.SaveQueues();
	}

	/// <summary>
	/// 一次性获取所有的本地数据，返回数组
	/// </summary>
	/// <returns></returns>
	public T[] QueryAll() {
		lock (lockObj) {
			return locals.ToArray();
		}
	}

	/// <summary>
	/// 一次性获取所有符合条件的本地数据，返回数组
	/// </summary>
	/// <returns></returns>
	public T[] Query(Func<T, bool> match) {
		lock (lockObj) {
			return locals.Where(match).ToArray();
		}
	}

	/// <summary>
	/// 标记为已更改
	/// </summary>
	private void MarkAsChanged(object? sender, PropertyChangedEventArgs e) {
		lock (lockObj) {
			var item = (T)sender!;
			if (!changes.Contains(item) && !deletes.Contains(item) && locals.Contains(item)) {
				changes.Add(item);
			}
		}
	}
}
