using System;

namespace ExplorerEx.Database.Shared;

[AttributeUsage(AttributeTargets.Class)]
public class DbTable : Attribute {
	public string? TableName { get; set; }

	public string? EntityName { get; set; }
}