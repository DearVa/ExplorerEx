using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ExplorerEx.Database.Interface;
using ExplorerEx.Database.Shared;
using SqlSugar;

namespace ExplorerEx.Database.SqlSugar; 

public abstract class SugarContext : IDatabase {
	protected readonly SqlSugarClient ConnectionClient;

	protected SugarContext(string databaseFilename) {
		var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "SqlSugar");
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
		ConnectionClient.Aop.DataExecuting = (value, info) => {
			var column = info.EntityColumnInfo;
			if (column.Navigat != null) {
				if (info.EntityValue is INotifyCollectionChanged ncc) {
					
				}
			}
		};
	}

	public virtual Task LoadAsync() {
		throw new InvalidOperationException();
	}

	public virtual void Save() {
		throw new InvalidOperationException();
	}

	public virtual Task SaveAsync() {
		throw new InvalidOperationException();
	}
}