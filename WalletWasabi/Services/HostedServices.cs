using GingerCommon.Static;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Services;

public class HostedServices : IDisposable
{
	private volatile bool _disposedValue = false; // To detect redundant calls

	private List<HostedService> Services { get; } = new();
	private ImmutableDictionary<Type, HostedService>? _services = null;

	private object ServicesLock { get; } = new();
	private bool IsStartAllAsyncStarted { get; set; } = false;
	private bool IsStopAllAsyncStarted { get; set; } = false;
	private bool IsStartAllAsyncFinished { get; set; } = false;

	private record LateCall(Type Type, Delegate Action, bool AfterAsyncStart);
	private List<LateCall> _lateCalls = new();

	public void Register<T>(Func<IHostedService> serviceFactory, string friendlyName) where T : class, IHostedService
	{
		Register(typeof(T), serviceFactory(), friendlyName);
	}

	private void Register(Type type, IHostedService service, string friendlyName)
	{
		if (!service.GetType().IsCompatible(type))
		{
			throw new ArgumentException($"Type mismatch: type is {type}, but service is {service.GetType()}.");
		}

		List<LateCall>? calls = null;
		lock (ServicesLock)
		{
			if (IsStartAllAsyncStarted)
			{
				throw new InvalidOperationException("Services are already started.");
			}

			if (Services.Any(x => x.Service.GetType().IsCompatible(type) || service.GetType().IsCompatible(x.Type)))
			{
				throw new InvalidOperationException($"{type.Name} is already registered.");
			}
			Services.Add(new HostedService(type, service, friendlyName));

			for (int idx = 0; idx < _lateCalls.Count;)
			{
				if (_lateCalls[idx].Type == type && !_lateCalls[idx].AfterAsyncStart)
				{
					calls ??= new();
					calls.Add(_lateCalls[idx]);
					_lateCalls.RemoveAt(idx);
				}
				else
				{
					idx++;
				}
			}
		}
		if (calls is not null)
		{
			foreach (LateCall call in calls)
			{
				call.Action.DynamicInvoke(service);
			}
		}
	}

	public void Call<T>(Action<T> action, bool afterAsyncStart = false) where T : class, IHostedService
	{
		Call(typeof(T), action, afterAsyncStart);
	}

	private void Call(Type type, Delegate action, bool afterAsyncStart)
	{
		// Simple case: we already finished the Async start
		HostedService? service = null;
		if (_services is not null)
		{
			service = GetOrDefault(type);
			if (service is not null)
			{
				action.DynamicInvoke(service.Service);
			}
			return;
		}
		// More careful branch
		lock (ServicesLock)
		{
			// If already running or about to finish, check
			if (IsStartAllAsyncFinished || IsStopAllAsyncStarted)
			{
				if (!afterAsyncStart || IsStartAllAsyncFinished)
				{
					service = GetOrDefault(type);
				}
			}
			else
			{
				// Early try
				if (!afterAsyncStart)
				{
					service = GetOrDefault(type);
				}
				if (service is null)
				{
					_lateCalls.Add(new LateCall(type, action, afterAsyncStart));
				}
			}
		}
		if (service is not null)
		{
			action.DynamicInvoke(service.Service);
		}
	}

	public async Task StartAllAsync(CancellationToken token = default)
	{
		HostedService[]? services = null;
		lock (ServicesLock)
		{
			if (IsStopAllAsyncStarted)
			{
				return;
			}
			if (IsStartAllAsyncStarted)
			{
				throw new InvalidOperationException("Operation is already started.");
			}
			IsStartAllAsyncStarted = true;
			services = Services.ToArray();
		}

		var exceptions = new List<Exception>();
		var exceptionsLock = new object();

		var tasks = services.Select(x => x.Service.StartAsync(token).ContinueWith(y =>
		{
			if (y.Exception is null)
			{
				Logger.LogInfo($"Started {x.FriendlyName}.");
			}
			else
			{
				lock (exceptionsLock)
				{
					exceptions.Add(y.Exception);
				}
				Logger.LogError($"Error starting {x.FriendlyName}.");
				Logger.LogError(y.Exception);
			}
		}));

		await Task.WhenAll(tasks).ConfigureAwait(false);

		if (exceptions.Count != 0)
		{
			throw new AggregateException(exceptions);
		}

		// Finalize
		List<LateCall>? lateCalls = null;
		lock (ServicesLock)
		{
			if (!IsStopAllAsyncStarted)
			{
				_services = services.ToImmutableDictionary(x => x.Type);
			}
			IsStartAllAsyncFinished = true;
			lateCalls = _lateCalls;
			_lateCalls = new();
		}
		foreach (var call in lateCalls)
		{
			var service = GetOrDefault(call.Type);
			if (service is not null)
			{
				call.Action.DynamicInvoke(service.Service);
			}
		}
	}

	/// <remarks>This method does not throw exceptions.</remarks>
	public async Task StopAllAsync(CancellationToken token = default)
	{
		HostedService[]? services = null;
		lock (ServicesLock)
		{
			if (IsStopAllAsyncStarted)
			{
				return;
			}
			IsStopAllAsyncStarted = true;
			_services = null;

			if (!IsStartAllAsyncStarted)
			{
				return;
			}
			services = Services.ToArray();
		}

		var tasks = services.Select(x => x.Service.StopAsync(token).ContinueWith(y =>
		{
			if (y.Exception is null)
			{
				Logger.LogInfo($"Stopped {x.FriendlyName}.");
			}
			else
			{
				Logger.LogError($"Error stopping {x.FriendlyName}.");
				Logger.LogError(y.Exception);
			}
		}));

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	public T? GetOrDefault<T>() where T : class, IHostedService
	{
		return (T?)GetOrDefault(typeof(T))?.Service;
	}

	public T Get<T>() where T : class, IHostedService
	{
		return (T)Get(typeof(T)).Service;
	}

	private HostedService? GetOrDefault(Type type)
	{
		var dict = _services;
		if (dict is not null)
		{
			dict.TryGetValue(type, out var service);
			return service;
		}
		// Slow mode, during the initalization
		lock (ServicesLock)
		{
			return Services.FirstOrDefault(x => x.Type == type);
		}
	}

	private HostedService Get(Type type)
	{
		var res = GetOrDefault(type);
		return res ?? throw new InvalidOperationException($"No service with type {type}.");
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				HostedService[]? services = null;
				lock (ServicesLock)
				{
					services = Services.ToArray();
				}

				foreach (var service in services)
				{
					if (service.Service is IDisposable disposable)
					{
						disposable?.Dispose();
						Logger.LogInfo($"Disposed {service.FriendlyName}.");
					}
				}
			}

			_disposedValue = true;
		}
	}

	// This code added to correctly implement the disposable pattern.
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		Dispose(true);
	}
}
