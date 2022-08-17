using System;
using System.CodeDom;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO.Enumeration;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using Castle.DynamicProxy;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ObjectsComparer;
using SqlSugar;
namespace ExplorerEx.DAL.SqlSugar
{
    /// <summary>
    /// 类型缓存的基类
    /// </summary>
    public abstract class SugarCacheBase
    {
        protected static readonly ProxyGenerator Generator = new();
        private static readonly Dictionary<Type, SugarCacheBase> Caches = new();
        protected bool Loaded = false;
        protected SugarCacheBase(Type type)
        {
            if (!type.IsClass || Caches.ContainsKey(type))
            {
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
        where TSub : class, new()
    {
        public Action<TSelf, TSub> Action;
        public SugarCache<TSub> Source;
        public SugarStrategy(SugarCache<TSub> source,Action<TSelf, TSub> action)
        {
            Source = source;
            Action = action;
        }
    }

    /// <summary>
    /// 需要注意的是 添加到缓存的泛型 需要将getter和setter标记为virtual才能被正确捕捉属性的更改，
    /// 相反的，不标记virtual可以防止不必要的捕捉
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SugarCache<T> : SugarCacheBase where T : class, new()
    {
        protected readonly ISqlSugarClient db;
        protected readonly List<T> locals = new();
        protected readonly List<T> adds = new();
        protected readonly List<T> changes = new();
        protected readonly List<T> deletes = new();
        
        public SugarCache(ISqlSugarClient db) : base(typeof(T))
        {
            this.db = db;
        }

        public override void LoadDatabase()
        {
            if (!Loaded)
            {
                var interceptor = new DbModelInterceptor<T>(this);
                db.Queryable<T>().ForEach(x => { locals.Add(Generator.CreateClassProxyWithTarget(x, interceptor)); });
                Loaded = true;
            }
        }

        public T? Find(Func<T, bool> match)
        {
            var ret = adds.FirstOrDefault(match);
            if (ret != null)
            {
                ret = locals.FirstOrDefault(match);
            }
            return ret;
        }
        public void Add(T item)
        {
            adds.Add(item);
        }
        public void Remove(T item)
        {
            if (!adds.Remove(item) && locals.Remove(item))
            {
                changes.Remove(item);
                deletes.Add(item);
            }
        }
        public List<T> QueryAll()
        {
            var ret = new List<T>();
            ret.AddRange(locals);
            ret.AddRange(adds);
            return ret;
        }
        public void Save()
        {
            adds.ForEach(x => db.Insertable(x).AddQueue());
            changes.ForEach(x => db.Updateable(x).AddQueue());
            deletes.ForEach(x => db.Deleteable(x).AddQueue());
            db.SaveQueues();
            Reset();
        }
        private void Reset()
        {
            var interceptor = new DbModelInterceptor<T>(this);
            //存储成功的目标添加代理并塞进Locals
            adds.ForEach(x =>
            {
                locals.Add(Generator.CreateClassProxyWithTarget(x, interceptor));
            });
            adds.Clear();
            deletes.Clear();
        }

        protected class DbModelInterceptor<T> : IInterceptor
            where T : class, new()
        {
            private readonly SugarCache<T> Cache;
            private readonly Type type = typeof(T);
            private readonly PropertyInfo[] typeinfos;
            public DbModelInterceptor(SugarCache<T> cache)
            {
                Cache = cache;
                typeinfos = type.GetProperties();
            }

            public void Intercept(IInvocation invocation)
            {
                invocation.Proceed();
                Task.Run(() =>
                {
                    if (invocation.Method.Attributes.HasFlag(MethodAttributes.HideBySig |
                                                             MethodAttributes.NewSlot |
                                                             MethodAttributes.SpecialName))
                    {
                        T p = (T)invocation.Proxy;
                        if (!Cache.changes.Contains(p) && 
                            !Cache.deletes.Contains(p) && 
                             Cache.locals.Contains(p)) 
                        {
                            Cache.changes.Add(p);
                        }
                    }
                });
            }
            
            
            public T? Clone(T o)
            {
                var ret = (T?)type.InvokeMember("", BindingFlags.CreateInstance, null, o, null);
                foreach (var pi in typeinfos)
                {
                    if (pi.CanWrite)
                    {
                        pi.SetValue(ret, pi.GetValue(o, null), null);
                    }
                }
                return ret;
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
        where TSub : class, new()
    {
        private readonly SugarStrategy<TSelf, TSub> Strategy;
        public SugarCache(ISqlSugarClient db, SugarStrategy<TSelf,TSub> strategy) : base(db)
        {
            Strategy = strategy;
        }

        public void LoadDataBase()
        {
            if (!Loaded)
            {
                var interceptor = new DbModelInterceptor<TSelf>(this);
                db.Queryable<TSelf>().ForEach(x =>
                {
                    Strategy.Source.QueryAll().ForEach(v => { Strategy.Action(x, v); });
                    locals.Add(Generator.CreateClassProxyWithTarget(x, interceptor));
                });
                Loaded = true;
            }

        }
    }

    /// <summary>
    /// 含有更多依赖关系的缓存
    /// </summary>
    /// <typeparam name="TSelf"></typeparam>
    /// <typeparam name="TSub1"></typeparam>
    /// <typeparam name="TSub2"></typeparam>
    public class SugarCache<TSelf, TSub1, TSub2> :SugarCache<TSelf>
        where TSelf : class, new()
        where TSub1 : class, new()
        where TSub2 : class, new()
    {
        private readonly SugarStrategy<TSelf, TSub1> Strategy1;
        private readonly SugarStrategy<TSelf, TSub2> Strategy2;

        public SugarCache(ISqlSugarClient db, SugarStrategy<TSelf, TSub1> strategy1, SugarStrategy<TSelf, TSub2> strategy2) : base(db)
        {
            Strategy1 = strategy1;
            Strategy2 = strategy2;
        }

        public void LoadDataBase()
        {
            if (!Loaded)
            {
                var interceptor = new DbModelInterceptor<TSelf>(this);
                db.Queryable<TSelf>().ForEach(x =>
                {
                    Strategy1.Source.QueryAll().ForEach(v => { Strategy1.Action(x, v); });
                    Strategy2.Source.QueryAll().ForEach(v => { Strategy2.Action(x, v); });
                    locals.Add(Generator.CreateClassProxyWithTarget(x, interceptor));
                });
                Loaded = true;
            }

        }
    }
}
