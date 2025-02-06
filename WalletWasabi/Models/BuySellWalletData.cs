using Newtonsoft.Json;
using WalletWasabi.BuySell;

namespace WalletWasabi.Models;

[JsonObject(MemberSerialization.OptIn)]
public class BuySellWalletData
{
	[JsonProperty(PropertyName = "Orders")]
	public BuySellClientModels.GetOrderResponseItem[] Orders { get; set; } = [];
}
