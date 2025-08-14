using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.Banning;

public record PrisonedCoinRecord
{
	public PrisonedCoinRecord(OutPoint outpoint, DateTimeOffset bannedUntil, InputBannedReasonEnum[] reasons)
	{
		Outpoint = outpoint;
		BannedUntil = bannedUntil;
		Reasons = reasons;
	}

	[JsonProperty]
	[JsonConverter(typeof(OutPointJsonConverter))]
	public OutPoint Outpoint { get; set; }

	public DateTimeOffset BannedUntil { get; set; }
	public InputBannedReasonEnum[] Reasons { get; set; }
}
