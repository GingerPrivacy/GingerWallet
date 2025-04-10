using Microsoft.Extensions.Hosting;
using WalletWasabi.Helpers;

namespace WalletWasabi.Services;

public class HostedService
{
	public HostedService(Type type, IHostedService service, string friendlyName)
	{
		Type = type;
		Service = Guard.NotNull(nameof(service), service);
		FriendlyName = Guard.NotNull(nameof(friendlyName), friendlyName);
	}

	public Type Type { get; }
	public IHostedService Service { get; }
	public string FriendlyName { get; }

	public override string ToString()
	{
		return FriendlyName;
	}
}
