using NBitcoin;
using System.Collections.Immutable;

namespace WalletWasabi.WabiSabi.Models;
public record RoundRecommendationResponse(ImmutableSortedSet<Money>? Denomination, ImmutableList<double>? Frequencies)
{
}
