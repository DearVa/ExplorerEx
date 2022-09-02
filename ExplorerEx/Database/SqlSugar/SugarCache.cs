using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using SqlSugar;

namespace ExplorerEx.Database.SqlSugar;

/// <summary>
/// 类型缓存的基类
/// </summary>
public abstract class SugarCacheBase {
	protected static readonly ProxyGenerator Generator = new();

	private static readonly Dictionary<Type, SugarCacheBase> Caches = new();

	protected bool IsLoaded;

	protected SugarCacheBase(Type type) {
		if (!type.IsClass || Caches.ContainsKey(type)) {
			throw new ArgumentException();
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
	where TSub : class, new() {

	public Action<TSelf, TSub> Action { get; }
	public SugarCache<TSub> Source { get; }

	public SugarStrategy(SugarCache<TSub> source, Action<TSelf, TSub> action) {
		Source = source;
		Action = action;
	}
}

/// <summary>
/// 需要注意的是 添加到缓存的泛型 需要将getter和setter标记为virtual才能被正确捕捉属性的更改，
/// 相反的，不标记virtual可以防止不必要的捕捉
/// </summary>
/// <typeparam name="T"></typeparam>
public class SugarCache<T> : SugarCacheBase where T : class, new() {
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

	protected readonly DbModelInterceptor interceptor;

	public SugarCache(ISqlSugarClient db) : base(typeof(T)) {
		this.db = db;
		interceptor = new DbModelInterceptor(this);
	}

	public void LoadDatabase() {
		if (!IsLoaded) {
			var interceptor = new DbModelInterceptor(this);
			db.Queryable<T>().ForEach(x => locals.Add(Generator.CreateClassProxyWithTarget(x, interceptor)));
			IsLoaded = true;
		}
	}

	public T? FirstOrDefault(Func<T, bool> match) => locals.FirstOrDefault(match);

	public bool Contains(T item) => locals.Contains(item);

	public void ForEach(Action<T> action) {
		foreach (var local in locals) {
			action.Invoke(local);
		}
	}

	public bool Any(Func<T, bool> match) => locals.Any(match);

	public void Add(T item) {
		// TODO: 目前是加锁，是否需要一个并行方法？（并行参考Utils.Collections.ConcurrentObservableCollection）
		lock (lockObj) {
			item = Generator.CreateClassProxyWithTarget(item, interceptor);
			adds.Add(item);
			locals.Add(item);
		}
	}

	/// <summary>
	/// 标记为已更改
	/// </summary>
	/// <param name="item">为Proxy</param>
	private void MarkAsChanged(T item) {
		lock (lockObj) {
			if (!changes.Contains(item) && !deletes.Contains(item) && locals.Contains(item)) {
				changes.Add(item);
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

	protected class DbModelInterceptor : IInterceptor {
		private readonly SugarCache<T> cache;

		public DbModelInterceptor(SugarCache<T> cache) {
			this.cache = cache;
		}

		public void Intercept(IInvocation invocation) {
			invocation.Proceed();
			// 我认为这里不应该用Task
			// 不然修改属性就是隐式的多线程
			// 如果涉及高并发修改，性能压力会比较大

			// 是否应该把反射提前到构造方法，需要跟踪的提前记录下来
			// 这里只用一个简单的比对即可
			// 至于下面的Contains和Add，目前是HashSet，性能很高
			// 可以考虑使用并发Add

			if (invocation.Method.Attributes.HasFlag(MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName)) {
				if (invocation.Method.ReturnType == typeof(void)) {
					cache.MarkAsChanged((T)invocation.Proxy);  // set
				}
			}
		}
	}
}
