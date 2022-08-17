using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using SqlSugar;

namespace ExplorerEx.Database; 

internal static class SqlSugarHelper {
	public static SqlSugarScope CreateSqlSugarScope(string dbPath) {
		return new SqlSugarScope(new ConnectionConfig {
			ConnectionString = @"DataSource=" + dbPath,  // 连接符字串
			DbType = DbType.Sqlite,  // 数据库类型
			IsAutoCloseConnection = true, // 不设成true要手动close
			ConfigureExternalServices = new ConfigureExternalServices {
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
					if (dbColumn == null) {
						column.IsIgnore = true;  // 没有这个特性的通通忽略
					} else {
						if (dbColumn.Name != null) {
							column.DbColumnName = dbColumn.Name;
						}
						if (dbColumn.IsPrimaryKey) {
							column.IsPrimarykey = true;
							column.IsIdentity = dbColumn.IsIdentity;
						} else if (dbColumn.NavigateTo != null) {
							var navigateType = dbColumn.NavigateType switch {
								DbColumnNavigateType.OneToOne => NavigateType.OneToOne,
								DbColumnNavigateType.OneToMany => NavigateType.OneToMany,
								DbColumnNavigateType.ManyToOne => NavigateType.ManyToOne,
								DbColumnNavigateType.ManyToMany => NavigateType.ManyToMany,
								_ => NavigateType.Dynamic
							};
							column.Navigat = new Navigate(navigateType, dbColumn.NavigateTo);
							if (t.PropertyType.IsSubclassOf(typeof(ObservableCollection<>))) {

							}
						}
					}
				}
			}
		}, db => {
			// 调试SQL事件，可以删掉
			db.Aop.OnLogExecuting = (sql, pars) => {
				Console.WriteLine(sql);
				// 输出sql,查看执行sql 性能无影响
				// 5.0.8.2 获取无参数化 SQL  对性能有影响，特别大的SQL参数多的，调试使用
				// UtilMethods.GetSqlString(DbType.SqlServer,sql,pars)
			};
		});
	}
}