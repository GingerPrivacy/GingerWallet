using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.StandaloneTests;

public class HostedServicesTests
{
	public class ServiceA : IHostedService
	{
		public int ValueA { get; set; } = 0;

		public Task StartAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	public class ServiceB : IHostedService
	{
		public int ValueB { get; set; } = 0;

		public Task StartAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	[Fact]
	public async Task HostedServicesCallTestAsync()
	{
		using HostedServices services = new();

		int counterA = 0, counterB = 0;

		services.Call<ServiceA>(x => { counterA++; x.ValueA++; }, false);
		services.Call<ServiceB>(x => { counterB++; x.ValueB++; }, false);
		services.Call<ServiceA>(x => { counterA++; x.ValueA++; }, true);
		services.Call<ServiceB>(x => { counterB++; x.ValueB++; }, true);
		Assert.Equal(0, counterA);
		Assert.Equal(0, counterB);

		services.Register<ServiceA>(() => new ServiceA(), "ServiceA");
		Assert.Equal(1, counterA);
		Assert.Equal(0, counterB);

		services.Register<ServiceB>(() => new ServiceB(), "ServiceB");
		Assert.Equal(1, counterA);
		Assert.Equal(1, counterB);

		services.Call<ServiceA>(x => { counterA++; }, false);
		services.Call<ServiceB>(x => { counterB++; }, false);
		Assert.Equal(2, counterA);
		Assert.Equal(2, counterB);

		await services.StartAllAsync();
		Assert.Equal(3, counterA);
		Assert.Equal(3, counterB);

		services.Call<ServiceA>(x => { counterA++; }, false);
		services.Call<ServiceB>(x => { counterB++; }, false);
		services.Call<ServiceA>(x => { counterA++; }, true);
		services.Call<ServiceB>(x => { counterB++; }, true);
		Assert.Equal(5, counterA);
		Assert.Equal(5, counterB);

		Assert.Equal(2, services.Get<ServiceA>().ValueA);
		Assert.Equal(2, services.Get<ServiceB>().ValueB);

		await services.StopAllAsync();
	}
}
