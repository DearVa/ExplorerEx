using System;
using System.Collections.Generic;
using System.Reflection;
using SqlSugar;

namespace ExplorerEx.Database.SqlSugar;

/// <summary>
/// 实验性质
/// </summary>
public static class SqlSugarHelper {
	private static readonly Dictionary<string, Type> TypeCache;
	private static readonly MethodInfo GetCustomTypeByClassInfo;

	static SqlSugarHelper() {
		TypeCache = (Dictionary<string, Type>)typeof(InstanceFactory).GetField("typeCache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
		GetCustomTypeByClassInfo = typeof(InstanceFactory).GetMethod("GetCustomTypeByClass", BindingFlags.NonPublic | BindingFlags.Static)!;
	}

	public static object Queryable(this SqlSugarClient client, Type type) {
		client.InitMappingInfo(type);
		return GetCacheInstance(GetClassName(client.CurrentConnectionConfig.DbType.ToString(), "Queryable"), type);
	}

	private static string GetClassName(string type, string name) {
		return type switch {
			"MySqlConnector" => "SqlSugar.MySqlConnector.MySql" + name,
			"Access" => "SqlSugar.Access.Access" + name,
			"ClickHouse" => "SqlSugar.ClickHouse.ClickHouse" + name,
			_ => type == "Custom" ? InstanceFactory.CustomNamespace + "." + InstanceFactory.CustomDbName + name : "SqlSugar." + type + name
		};
	}

	private static object GetCacheInstance(string className, Type type) {
		var key = className + type;
		lock (TypeCache) {
			if (TypeCache.ContainsKey(key)) {
				type = TypeCache[key];
			} else {
				if (string.IsNullOrEmpty(InstanceFactory.CustomDllName)) {
					type = Type.GetType(className + "`1", true)!.MakeGenericType(type);
				} else {
					var customTypeByClass = (Type?)GetCustomTypeByClassInfo.Invoke(null, new object[] { className + "`1" });
					if (customTypeByClass != null) {
						type = customTypeByClass.MakeGenericType(type);
					}
				}
				if (!TypeCache.ContainsKey(key)) {
					TypeCache.Add(key, type);
				}
			}
		}
		return Activator.CreateInstance(type, true)!;
	}
}