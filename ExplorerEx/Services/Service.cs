// BY: feast107

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExplorerEx.Services; 

/// <summary>
/// 注册的顺序
/// Add* => build => Resolve/CreateScope => 
/// build 之后新增注册或者更改需要重新 build
/// Replace/Add * => build => Resolve/CreateScope
/// 基于会话的服务需要重新 CreateScope ，原有的子容器不会提供更改过的实例
/// </summary>
public class Service
{
	#region Private Fields

	private static readonly Service Singleton;

	static Service() {
		Singleton = new Service();
	}

	private readonly ServiceCollection services = new();
	private ServiceProvider? provider;

	#endregion

	#region Singleton
	/// <summary>
	/// 全局的动态注册(自动依赖)
	/// </summary>
	/// <typeparam name="TService">服务的接口</typeparam>
	public static IServiceCollection AddSingleton<TService>()
		where TService : class
	{
		return Singleton.services.AddSingleton<TService>();
	}
	/// <summary>
	/// 全局的注册(无依赖)
	/// </summary>
	/// <typeparam name="TService">服务的接口</typeparam>
	/// <typeparam name="TImplement">实现类</typeparam>
	public static IServiceCollection AddSingleton<TService, TImplement>()
		where TService : class
		where TImplement : class, TService, new()
	{
		return Singleton.services.AddSingleton<TService, TImplement>();
	}

	/// <summary>
	/// 全局的动态注册(配置依赖)
	/// </summary>
	/// <typeparam name="TService">服务的接口</typeparam>
	public static IServiceCollection AddSingleton<TService>(Func<IServiceProvider, TService> generation)
		where TService : class
	{
		return Singleton.services.AddSingleton(generation);
	}

	/// <summary>
	/// 全局的静态注册(配置依赖)
	/// </summary>
	/// <typeparam name="TService">服务的接口</typeparam>
	/// <typeparam name="TImplement">实现类</typeparam>
	/// <param name="generation"></param>
	public static IServiceCollection AddSingleton<TService, TImplement>(Func<IServiceProvider, TImplement> generation)
		where TService : class
		where TImplement : class, TService
	{
		return Singleton.services.AddSingleton<TService, TImplement>(generation);
	}

	/// <summary>
	/// 提供泛型的全局注册
	/// </summary>
	/// <param name="template">typeof(T)</param>
	/// <param name="implement">typeof(I)</param>
	/// <exception cref="ArgumentException"></exception>
	public static IServiceCollection AddSingleton(Type template, Type implement)
	{
		if (!template.IsInterface)
		{
			throw new ArgumentException("Arg[0] should be interface");
		}

		if (!implement.IsSubclassOf(template))
		{
			throw new ArgumentException("Arg[1] should be implement of Arg[0]");
		}

		return Singleton.services.AddSingleton(template, implement);
	}

	/// <summary>
	/// 替换全局注册(无依赖)
	/// </summary>
	/// <typeparam name="TService"></typeparam>
	/// <typeparam name="TImplement"></typeparam>
	public static IServiceCollection ReplaceSingleton<TService, TImplement>()
		where TService : class
		where TImplement : class, TService, new()
	{
		return Singleton.services.Replace(ServiceDescriptor.Singleton<TService, TImplement>());
	}

	/// <summary>
	/// 替换全局动态注册(配置新的依赖)
	/// </summary>
	/// <typeparam name="TService">服务的接口</typeparam>
	public static IServiceCollection ReplaceSingleton<TService>(Func<IServiceProvider, TService> generation)
		where TService : class
	{
		return Singleton.services.Replace(ServiceDescriptor.Singleton(generation));
	}

	/// <summary>
	/// 替换全局注册(配置新的依赖)
	/// </summary>
	/// <typeparam name="TService"></typeparam>
	/// <typeparam name="TImplement"></typeparam>
	/// <param name="generation"></param>
	public static IServiceCollection ReplaceSingleton<TService, TImplement>(
		Func<IServiceProvider, TImplement> generation)
		where TService : class
		where TImplement : class, TService
	{
		return Singleton.services.Replace(ServiceDescriptor.Singleton<TService, TImplement>(generation));
	}

	/// <summary>
	/// 替换全局泛型的注册
	/// </summary>
	/// <param name="template"></param>
	/// <param name="implement"></param>
	/// <exception cref="ArgumentException"></exception>
	public static IServiceCollection ReplaceSingleton(Type template, Type implement)
	{
		if (!template.IsInterface)
		{
			throw new ArgumentException("Arg[0] should be interface");
		}

		if (!implement.IsSubclassOf(template))
		{
			throw new ArgumentException("Arg[1] should be implement of Arg[0]");
		}

		return Singleton.services.Replace(ServiceDescriptor.Singleton(template, implement));
	}

	#endregion

	#region Scoped
	/// <summary>
	/// 会话的注册(自动依赖)
	/// </summary>
	/// <typeparam name="TModel">模型接口</typeparam>
	public static IServiceCollection AddScoped<TModel>()
		where TModel : class
	{
		return Singleton.services.AddScoped<TModel>();
	}
	/// <summary>
	/// 会话的注册(无依赖)
	/// </summary>
	/// <typeparam name="TModel">模型接口</typeparam>
	/// <typeparam name="TImplement">实现类</typeparam>
	public static IServiceCollection AddScoped<TModel, TImplement>()
		where TModel : class
		where TImplement : class, TModel, new()
	{
		return Singleton.services.AddScoped<TModel, TImplement>();
	}

	/// <summary>
	/// 会话的动态注册(配置依赖)
	/// </summary>
	/// <typeparam name="TService">服务的接口</typeparam>
	public static IServiceCollection AddScoped<TService>(Func<IServiceProvider, TService> generation)
		where TService : class
	{
		return Singleton.services.AddScoped(generation);
	}

	/// <summary>
	/// 会话的静态注册(配置依赖)
	/// </summary>
	/// <typeparam name="TModel">模型接口</typeparam>
	/// <typeparam name="TImplement">实现类</typeparam>
	/// <param name="generation"></param>
	public static IServiceCollection AddScoped<TModel, TImplement>(Func<IServiceProvider, TImplement> generation)
		where TModel : class
		where TImplement : class, TModel
	{
		return Singleton.services.AddScoped<TModel, TImplement>(generation);
	}

	/// <summary>
	/// 提供泛型的会话的注册
	/// </summary>
	/// <param name="template">typeof(T)</param>
	/// <param name="implement">typeof(I)</param>
	/// <exception cref="ArgumentException"></exception>
	public static IServiceCollection AddScoped(Type template, Type implement)
	{
		if (!template.IsInterface)
		{
			throw new ArgumentException("Arg[0] should be interface");
		}

		if (!implement.IsSubclassOf(template))
		{
			throw new ArgumentException("Arg[1] should be implement of Arg[0]");
		}

		return Singleton.services.AddScoped(template, implement);
	}

	/// <summary>
	/// 替换会话注册(无依赖)
	/// </summary>
	/// <typeparam name="TService"></typeparam>
	/// <typeparam name="TImplement"></typeparam>
	public static IServiceCollection ReplaceScoped<TService, TImplement>()
		where TService : class
		where TImplement : class, TService, new()
	{
		return Singleton.services.Replace(ServiceDescriptor.Scoped<TService, TImplement>());
	}

	/// <summary>
	/// 替换会话动态注册(配置新的依赖)
	/// </summary>
	/// <typeparam name="TService">服务的接口</typeparam>
	public static IServiceCollection ReplaceScoped<TService>(Func<IServiceProvider, TService> generation)
		where TService : class
	{
		return Singleton.services.Replace(ServiceDescriptor.Scoped(generation));
	}

	/// <summary>
	/// 替换会话注册(配置新的依赖)
	/// </summary>
	/// <typeparam name="TService"></typeparam>
	/// <typeparam name="TImplement"></typeparam>
	/// <param name="generation"></param>
	public static IServiceCollection ReplaceScoped<TService, TImplement>(Func<IServiceProvider, TImplement> generation)
		where TService : class
		where TImplement : class, TService
	{
		return Singleton.services.Replace(ServiceDescriptor.Scoped<TService, TImplement>(generation));
	}

	/// <summary>
	/// 替换会话泛型的注册
	/// </summary>
	/// <param name="template"></param>
	/// <param name="implement"></param>
	/// <exception cref="ArgumentException"></exception>
	public static IServiceCollection ReplaceScoped(Type template, Type implement)
	{
		if (!template.IsInterface)
		{
			throw new ArgumentException("Arg[0] should be interface");
		}

		if (!implement.IsSubclassOf(template))
		{
			throw new ArgumentException("Arg[1] should be implement of Arg[0]");
		}

		return Singleton.services.Replace(ServiceDescriptor.Scoped(template, implement));
	}

	#endregion

	#region Transient
	/// <summary>
	/// 单次调用的注册(自动依赖)
	/// </summary>
	/// <typeparam name="TModel">模型接口</typeparam>
	public static IServiceCollection AddTransient<TModel>()
		where TModel : class
	{
		return Singleton.services.AddTransient<TModel>();
	}
	/// <summary>
	/// 单次调用的注册(无依赖)
	/// </summary>
	/// <typeparam name="TModel">模型接口</typeparam>
	/// <typeparam name="TImplement">实现类</typeparam>
	public static IServiceCollection AddTransient<TModel, TImplement>()
		where TModel : class
		where TImplement : class, TModel, new()
	{
		return Singleton.services.AddTransient<TModel, TImplement>();
	}

	/// <summary>
	/// 单次调用的动态注册(配置依赖)
	/// </summary>
	/// <typeparam name="TService">服务的接口</typeparam>
	public static IServiceCollection AddTransient<TService>(Func<IServiceProvider, TService> generation)
		where TService : class
	{
		return Singleton.services.AddTransient(generation);
	}

	/// <summary>
	/// 单次调用的静态注册(配置依赖)
	/// </summary>
	/// <typeparam name="TModel">模型接口</typeparam>
	/// <typeparam name="TImplement">实现类</typeparam>
	/// <param name="generation"></param>
	public static IServiceCollection AddTransient<TModel, TImplement>(Func<IServiceProvider, TImplement> generation)
		where TModel : class
		where TImplement : class, TModel
	{
		return Singleton.services.AddTransient<TModel, TImplement>(generation);
	}

	/// <summary>
	/// 提供泛型的单次调用的注册
	/// </summary>
	/// <param name="template">typeof(T)</param>
	/// <param name="implement">typeof(I)</param>
	/// <exception cref="ArgumentException"></exception>
	public static IServiceCollection AddTransient(Type template, Type implement)
	{
		if (!template.IsInterface)
		{
			throw new ArgumentException("Arg[0] should be interface");
		}

		if (!implement.IsSubclassOf(template))
		{
			throw new ArgumentException("Arg[1] should be implement of Arg[0]");
		}

		return Singleton.services.AddTransient(template, implement);
	}

	/// <summary>
	/// 替换单次调用注册(无依赖)
	/// </summary>
	/// <typeparam name="TService"></typeparam>
	/// <typeparam name="TImplement"></typeparam>
	public static IServiceCollection ReplaceTransient<TService, TImplement>()
		where TService : class
		where TImplement : class, TService, new()
	{
		return Singleton.services.Replace(ServiceDescriptor.Transient<TService, TImplement>());
	}

	/// <summary>
	/// 替换单次调用的动态注册(配置新的依赖)
	/// </summary>
	/// <typeparam name="TService">服务的接口</typeparam>
	public static IServiceCollection ReplaceTransient<TService>(Func<IServiceProvider, TService> generation)
		where TService : class
	{
		return Singleton.services.Replace(ServiceDescriptor.Transient(generation));
	}

	/// <summary>
	/// 替换单次调用注册(配置新的依赖)
	/// </summary>
	/// <typeparam name="TService"></typeparam>
	/// <typeparam name="TImplement"></typeparam>
	/// <param name="generation"></param>
	public static IServiceCollection ReplaceTransient<TService, TImplement>(
		Func<IServiceProvider, TImplement> generation)
		where TService : class
		where TImplement : class, TService
	{
		return Singleton.services.Replace(ServiceDescriptor.Transient<TService, TImplement>(generation));
	}

	/// <summary>
	/// 替换临时泛型的注册
	/// </summary>
	/// <param name="template"></param>
	/// <param name="implement"></param>
	/// <exception cref="ArgumentException"></exception>
	public static IServiceCollection ReplaceTransient(Type template, Type implement)
	{
		if (!template.IsInterface)
		{
			throw new ArgumentException("Arg[0] should be interface");
		}

		if (!implement.IsSubclassOf(template))
		{
			throw new ArgumentException("Arg[1] should be implement of Arg[0]");
		}

		return Singleton.services.Replace(ServiceDescriptor.Transient(template, implement));
	}

	#endregion

	#region Removal

	/// <summary>
	/// 移除全部与该接口相关的服务
	/// </summary>
	/// <typeparam name="TService">服务的接口</typeparam>
	public static IServiceCollection RemoveAll<TService>()
	{
		return Singleton.services.RemoveAll<TService>();
	}

	#endregion

	/// <summary>
	/// 启动/重启 服务
	/// </summary>
	public static void Build()
	{
		if (Singleton.provider == null)
		{
			Singleton.provider = Singleton.services.BuildServiceProvider();
		}
		else
		{
			Singleton.provider.Dispose();
			Singleton.provider = Singleton.services.BuildServiceProvider();
		}
	}

	/// <summary>
	/// 解析单例或临时服务
	/// </summary>
	/// <typeparam name="TService">注册的服务类型</typeparam>
	/// <returns></returns>
	public static TService Resolve<TService>() where TService : notnull
	{
		if (Singleton.provider == null)
		{
			throw new Exception("Service haven't been built");
		}

		return Singleton.provider.GetRequiredService<TService>();
	}

	/// <summary>
	/// 解析单例或临时服务
	/// </summary>
	/// <param name="type">注册的服务类型</param>
	/// <returns></returns>
	public static object Resolve(Type type)
	{
		if (Singleton.provider == null)
		{
			throw new Exception("Service haven't been built");
		}

		return Singleton.provider.GetRequiredService(type);
	}

	/// <summary>
	/// 创建一个会话服务
	/// </summary>
	/// <returns></returns>
	public static IServiceScope CreateScope()
	{
		if (Singleton.provider == null) {
			throw new Exception("Service haven't been built");
		}

		return Singleton.provider.CreateScope();
	}
}

public static class ScopeExtension
{
	public static TService Resolve<TService>(this IServiceScope scope) where TService : notnull
	{
		return scope.ServiceProvider.GetRequiredService<TService>();
	}
}