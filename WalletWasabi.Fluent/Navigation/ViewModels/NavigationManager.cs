using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Common.ViewModels;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Navigation.ViewModels;

public static class NavigationManager
{
	private static readonly Dictionary<NavigationMetaData, InstanceGeneratorBase> NavigationEntries = new();

	private static readonly Dictionary<Type, ViewModelBase> TypeRegistry = new();

	public static IEnumerable<NavigationMetaData> MetaData => NavigationEntries.Keys.Select(x => x);

	public static void RegisterType<T>(T instance) where T : ViewModelBase
	{
		if (!TypeRegistry.ContainsKey(typeof(T)))
		{
			TypeRegistry.Add(typeof(T), instance);
		}
	}

	public static T? Get<T>() where T : ViewModelBase
	{
		if (TypeRegistry.TryGetValue(typeof(T), out ViewModelBase? value) && value is T vmb)
		{
			return vmb;
		}

		return null;
	}

	public static async Task<RoutableViewModel?> MaterializeViewModelAsync(NavigationMetaData metaData)
	{
		if (NavigationEntries.TryGetValue(metaData, out InstanceGeneratorBase? generator))
		{
			if (generator is InstanceGenerator instanceGenerator)
			{
				return instanceGenerator.Generate;
			}
			else if (generator is SynchronousInstanceGenerator synchronousInstanceGenerator)
			{
				return synchronousInstanceGenerator.Generate();
			}
			else if (generator is AsyncInstanceGenerator asyncInstanceGenerator)
			{
				return await asyncInstanceGenerator.Generate();
			}
		}

		throw new Exception("ViewModel metadata not registered.");
	}

	public static void RegisterAsyncLazy(NavigationMetaData metaData, Func<Task<RoutableViewModel?>> generator)
	{
		if (metaData.Searchable && (metaData.Category is SearchCategory.Default || metaData.Title is null))
		{
			throw new Exception("Searchable entries must have both a Category and a Title");
		}

		if (!NavigationEntries.ContainsKey(metaData))
		{
			NavigationEntries.Add(metaData, new AsyncInstanceGenerator(generator));
		}
	}

	public static void RegisterLazy(NavigationMetaData metaData, Func<RoutableViewModel?> generator)
	{
		if (metaData.Searchable && (metaData.Category is SearchCategory.Default || metaData.Title is null))
		{
			throw new Exception("Searchable entries must have both a Category and a Title");
		}

		if (!NavigationEntries.ContainsKey(metaData))
		{
			NavigationEntries.Add(metaData, new SynchronousInstanceGenerator(generator));
		}
	}

	public static void Register(NavigationMetaData metaData, RoutableViewModel instance)
	{
		if (metaData.Searchable && (metaData.Category is SearchCategory.Default || metaData.Title is null))
		{
			throw new Exception("Searchable entries must have both a Category and a Title");
		}

		if (!NavigationEntries.ContainsKey(metaData))
		{
			NavigationEntries.Add(metaData, new InstanceGenerator(instance));
		}
	}

	private class InstanceGeneratorBase
	{
	}

	private class AsyncInstanceGenerator : InstanceGeneratorBase
	{
		public AsyncInstanceGenerator(Func<Task<RoutableViewModel?>> generate)
		{
			Generate = generate;
		}

		public Func<Task<RoutableViewModel?>> Generate { get; }
	}

	private class SynchronousInstanceGenerator : InstanceGeneratorBase
	{
		public SynchronousInstanceGenerator(Func<RoutableViewModel?> generate)
		{
			Generate = generate;
		}

		public Func<RoutableViewModel?> Generate { get; }
	}

	private class InstanceGenerator : InstanceGeneratorBase
	{
		public InstanceGenerator(RoutableViewModel generate)
		{
			Generate = generate;
		}

		public RoutableViewModel Generate { get; }
	}
}
