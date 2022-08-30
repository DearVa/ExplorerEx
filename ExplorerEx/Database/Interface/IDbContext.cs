using System;
using System.Linq.Expressions;

namespace ExplorerEx.Database.Interface;

public interface IDbContext<T> {
	void Add(T item);
	public T? FirstOrDefault(Expression<Func<T, bool>> match);
	void Remove(T item);
	bool Contains(T item);
	bool Any(Expression<Func<T, bool>> match);

	int Count();
}