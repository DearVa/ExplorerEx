using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using ExplorerEx.Database.Interface;
using ExplorerEx.Database.Shared;
using ExplorerEx.Model;
using SqlSugar;

namespace ExplorerEx.Database.SqlSugar; 

public class SugarContext<T> : IDatabase, IDbContext<T> where T : class, new() {
	protected readonly SqlSugarClient ConnectionClient;

	public SugarContext(string databaseFilename) {
		var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data");
		if (!Directory.Exists(path)) {
			Directory.CreateDirectory(path);
		}
		var dbPath = Path.Combine(path, databaseFilename);
		ConnectionClient = new SqlSugarClient(new ConnectionConfig {
			ConnectionString = @"DataSource=" + dbPath,
			DbType = DbType.Sqlite,
			InitKeyType = InitKeyType.Attribute,
			ConfigureExternalServices = new ConfigureExternalServices {
				// 在这里解析Attributes标注
				EntityNameService = (t, info) => {
					var dbTable = t.GetCustomAttributes().OfType<DbTable>().FirstOrDefault();
					if (dbTable != null) {
						if (dbTable.TableName != null) {
							info.DbTableName = dbTable.TableName;
						}
						if (dbTable.EntityName != null) {
							info.EntityName = dbTable.EntityName;
						}
					}
				},
				EntityService = (t, column) => {
					var dbColumn = t.GetCustomAttributes().OfType<DbColumn>().FirstOrDefault();
					column.Navigat = null;
					if (dbColumn == null) {
						column.IsIgnore = true;  // 没有这个特性的通通忽略
					} else {
						if (dbColumn.Name != null) {
							column.DbColumnName = dbColumn.Name;
						}
						if (dbColumn.MaxLength > 0) {
							column.Length = dbColumn.MaxLength;
						}
						if (dbColumn.IsPrimaryKey) {
							column.IsPrimarykey = true;
							column.IsIdentity = dbColumn.IsIdentity;
						} else if (dbColumn.NavigateTo != null) {
							var navigateType = dbColumn.NavigateType switch {
								DbNavigateType.OneToOne => NavigateType.OneToOne,
								DbNavigateType.OneToMany => NavigateType.OneToMany,
								DbNavigateType.ManyToOne => NavigateType.ManyToOne,
								DbNavigateType.ManyToMany => NavigateType.ManyToMany,
								_ => NavigateType.Dynamic
							};
							column.Navigat = new Navigate(navigateType, dbColumn.NavigateTo);
							column.IsIgnore = true;
						}
					}
				}
			}
		});
		// 实体创建的时候执行
		ConnectionClient.Aop.DataExecuting = (_, info) => Aop(info.EntityValue, info.EntityColumnInfo);
		ConnectionClient.Aop.DataExecuted = (_, info) => {
			foreach (var column in info.EntityColumnInfos) {
				Aop(info.EntityValue, column);
			}
		};
	}

	private void Aop(object entityValue, EntityColumnInfo column) {
		// TODO: Fuck this, I'm out
		if (column.Navigat != null) {
			switch (column.Navigat.NavigatType) {
			case NavigateType.OneToOne:
				if (column.UnderType == typeof(BookmarkCategory)) {
					var category = ConnectionClient.Queryable<BookmarkCategory>().First(c => c.Name == ((BookmarkItem)entityValue).Name);
					column.PropertyInfo.SetValue(entityValue, category);
				}
				break;
			case NavigateType.OneToMany:  // 这里是一个集合，那么就需要查询一次，填充集合
				if (column.UnderType == typeof(ObservableCollection<BookmarkItem>)) {
					var list = ConnectionClient.Queryable<BookmarkItem>()
						.Where(i => i.CategoryForeignKey == ((BookmarkCategory)entityValue).Name)
						.ToList();
					column.PropertyInfo.SetValue(entityValue, new ObservableCollection<BookmarkItem>(list));
				}
				break;
			}
		}
	}

	public virtual Task LoadAsync() => Task.Run(() => {
		ConnectionClient.DbMaintenance.CreateDatabase();
		ConnectionClient.CodeFirst.InitTables<T>();
	});

	public virtual void Save() => ConnectionClient.SaveQueues();

	public virtual Task SaveAsync() => ConnectionClient.SaveQueuesAsync();

	public virtual void Add(T item) => ConnectionClient.Insertable(item);

	public virtual T? FirstOrDefault(Expression<Func<T, bool>> match) => ConnectionClient.Queryable<T>().First(match);

	public virtual void Remove(T item) => ConnectionClient.Deleteable(item);

	public virtual bool Contains(T item) => ConnectionClient.Queryable<T>().Any(i => i == item);

	public virtual bool Any(Expression<Func<T, bool>> match) => ConnectionClient.Queryable<T>().Any(match);

	public virtual int Count() => ConnectionClient.Queryable<T>().Count();
}

public class CachedSugarContext<T> : SugarContext<T> where T : class, new() {
	private readonly SugarCache<T> cache;

	public CachedSugarContext(string databaseFilename) : base(databaseFilename) {
		cache = new SugarCache<T>(ConnectionClient);
	}

	public override async Task LoadAsync() {
		await base.LoadAsync();
		await Task.Run(cache.LoadDatabase);
	}

	public override void Save() => cache.Save();

	public override Task SaveAsync() => Task.Run(cache.Save);  // TODO: Thread safe???

	public override void Add(T item) => cache.Add(item);

	public override T? FirstOrDefault(Expression<Func<T, bool>> match) => cache.FirstOrDefault(match.Compile());

	public override void Remove(T item) => cache.Remove(item);

	public override bool Contains(T item) => cache.Contains(item);

	public override bool Any(Expression<Func<T, bool>> match) => cache.Any(match.Compile());

	public override int Count() => cache.Count();

	public ObservableCollection<T> GetBindable() {
		var list = new List<T>(cache.Count());  // 初始化容量
		cache.ForEach(i => list.Add(i));  // 先使用List，这样Add的时候不会触发事件，减少消耗
		return new ObservableCollection<T>(list);
	}
}