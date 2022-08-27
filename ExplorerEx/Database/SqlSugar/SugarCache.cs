using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
			throw new ExternalException("This class has already been proxied");
		}
		Caches.Add(type, this);
	}

	public abstract void LoadDatabase();
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

	public override void LoadDatabase() {
		if (!IsLoaded) {
			var interceptor = new DbModelInterceptor(this);
			db.Queryable<T>().ForEach(x => locals.Add(Generator.CreateClassProxyWithTarget(x, interceptor)));
			IsLoaded = true;
		}
	}

	public T? FirstOrDefault(Func<T, bool> match) {
		return locals.FirstOrDefault(match);
	}

	public bool Contains(T item) {
		return locals.Contains(item);
	}

	public void ForEach(Action<T> action) {
		foreach (var local in locals) {
			action.Invoke(local);
		}
	}

	public bool Any(Func<T, bool> match) {
		return locals.Any(match);
	}

	public void Add(T item) {
		// TODO: 目前是加锁，是否需要一个并行方法？（并行参考Utils.Collections.ConcurrentObservableCollection）
		lock (lockObj) {
			item = Generator.CreateClassProxyWithTarget(item, interceptor);
			adds.Add(item);
			locals.Add(item);
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

	public int Count() {
		return locals.Count;
	}

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
				var p = (T)invocation.Proxy;
				if (!cache.changes.Contains(p) && !cache.deletes.Contains(p) && cache.locals.Contains(p)) {
					cache.changes.Add(p);
				}
			}
		}
	}
}

/// <summary>
/// 含有依赖关系的缓存
/// </summary>
/// <typeparam name="TSelf"></typeparam>
/// <typeparam name="TSub"></typeparam>
public class SugarCache<TSelf, TSub> : SugarCache<TSelf> 
	where TSelf : class, new() 
	where TSub : class, new() {

	private readonly SugarStrategy<TSelf, TSub> strategy;

	public SugarCache(ISqlSugarClient db, SugarStrategy<TSelf, TSub> strategy) : base(db) {
		this.strategy = strategy;
	}

	public void LoadDataBase() {
		if (!IsLoaded) {
			var interceptor = new DbModelInterceptor(this);
			db.Queryable<TSelf>().ForEach(x => {
				strategy.Source.ForEach(v => strategy.Action(x, v));
				locals.Add(Generator.CreateClassProxyWithTarget(x, interceptor));
			});
			IsLoaded = true;
		}
	}
}

/// <summary>
/// 含有更多依赖关系的缓存
/// </summary>
/// <typeparam name="TSelf"></typeparam>
/// <typeparam name="TSub1"></typeparam>
/// <typeparam name="TSub2"></typeparam>
public class SugarCache<TSelf, TSub1, TSub2> : SugarCache<TSelf>
	where TSelf : class, new()
	where TSub1 : class, new()
	where TSub2 : class, new() {
	private readonly SugarStrategy<TSelf, TSub1> strategy1;
	private readonly SugarStrategy<TSelf, TSub2> strategy2;

	public SugarCache(ISqlSugarClient db, SugarStrategy<TSelf, TSub1> strategy1, SugarStrategy<TSelf, TSub2> strategy2) : base(db) {
		this.strategy1 = strategy1;
		this.strategy2 = strategy2;
	}

	public void LoadDataBase() {
		if (!IsLoaded) {
			var interceptor = new DbModelInterceptor(this);
			db.Queryable<TSelf>().ForEach(x => {
				strategy1.Source.ForEach(v => strategy1.Action(x, v));
				strategy2.Source.ForEach(v => strategy2.Action(x, v));
				locals.Add(Generator.CreateClassProxyWithTarget(x, interceptor));
			});
			IsLoaded = true;
		}

	}
}