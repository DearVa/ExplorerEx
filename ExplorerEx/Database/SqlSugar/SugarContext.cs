using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using ExplorerEx.Database.Interface;
using ExplorerEx.Database.Shared;
using SqlSugar;

namespace ExplorerEx.Database.SqlSugar; 

public class SugarContext<T> : IDatabase, IDbContext<T> where T : class, new() {
	protected readonly SqlSugarClient ConnectionClient;

	protected SugarContext(string databaseFilename) {
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
						}
					}
				}
			}
		});
	}

	public virtual Task LoadAsync() => Task.Run(() => {
		ConnectionClient.DbMaintenance.CreateDatabase();
		ConnectionClient.CodeFirst.InitTables<T>();
	});

	public virtual void Save() => ConnectionClient.SaveQueues();

	public virtual Task SaveAsync() => ConnectionClient.SaveQueuesAsync();

	public virtual void Add(T item) => ConnectionClient.Insertable(item).AddQueue();

	public virtual void Update(T item) => ConnectionClient.Updateable(item).AddQueue();

	public virtual T? FirstOrDefault(Expression<Func<T, bool>> match) => ConnectionClient.Queryable<T>().First(match);

	public virtual void Remove(T item) => ConnectionClient.Deleteable(item).AddQueue();

	public virtual bool Contains(T item) => ConnectionClient.Queryable<T>().Any(i => i == item);

	public virtual bool Any(Expression<Func<T, bool>> match) => ConnectionClient.Queryable<T>().Any(match);

	public virtual int Count() => ConnectionClient.Queryable<T>().Count();
}