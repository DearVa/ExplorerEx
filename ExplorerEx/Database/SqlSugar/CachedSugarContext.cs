using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ExplorerEx.Database.SqlSugar;

public class CachedSugarContext<T> : SugarContext<T> where T : class, INotifyPropertyChanged, new() {
	private readonly SugarCache<T> cache;

	/// <summary>
	/// 如果用户调用了<see cref="AsObservableCollection"/>，生成的同时要在这里也存下来，这样的话以后用户调用<see cref="Add"/>等还得更新这个
	/// </summary>
	private ObservableCollection<T>? observableCollection;

	public CachedSugarContext(string databaseFilename) : base(databaseFilename) {
		cache = new SugarCache<T>(ConnectionClient);
	}

	public override async Task LoadAsync() {
		await base.LoadAsync();
		await Task.Run(cache.LoadDatabase);
	}

	public override void Save() => cache.Save();

	public override Task SaveAsync() => Task.Run(cache.Save);  // TODO: Thread safe???

	public override void Add(T item) {
		cache.Add(item);
		observableCollection?.Add(item);
	}

	public override T? FirstOrDefault(Expression<Func<T, bool>> match) => cache.FirstOrDefault(match.Compile());

	public override void Remove(T item) {
		cache.Remove(item);
		observableCollection?.Remove(item);
	}

	public override bool Contains(T item) => cache.Contains(item);

	public override bool Any(Expression<Func<T, bool>> match) => cache.Any(match.Compile());

	public override int Count() => cache.Count();

	/// <summary>
	/// 获得满足条件的项目列表
	/// </summary>
	/// <param name="match"></param>
	/// <returns></returns>
	public T[] Query(Expression<Func<T, bool>> match) => cache.Query(match.Compile());

	public ObservableCollection<T> AsObservableCollection() => observableCollection ??= new ObservableCollection<T>(cache.QueryAll());
}