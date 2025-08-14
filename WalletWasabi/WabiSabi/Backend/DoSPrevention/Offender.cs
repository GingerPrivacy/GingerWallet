using NBitcoin;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.DoSPrevention;

public enum RoundDisruptionMethod
{
	DidNotConfirm,
	DidNotSignalReadyToSign,
	DidNotSign,
	DoubleSpent,
	BackendStabilitySafety
}

public abstract record Offense();

public record RoundDisruption(IEnumerable<uint256> DisruptedRoundIds, Money Value, RoundDisruptionMethod Method) : Offense
{
	public RoundDisruption(uint256 disruptedRoundId, Money value, RoundDisruptionMethod method)
		: this(disruptedRoundId.Singleton(), value, method) { }
}
public record BackendStabilitySafety(uint256 RoundId) : Offense;
public record FailedToVerify(uint256 VerifiedInRoundId, TimeSpan RecommendedBanTime, string Provider) : Offense;
public record Inherited(OutPoint[] Ancestors, InputBannedReasonEnum[] InputBannedReasonEnums) : Offense;
public record Cheating(uint256 RoundId) : Offense;

public record Offender(OutPoint OutPoint, DateTimeOffset StartedTime, Offense Offense)
{
	private const string Separator = ",";
	public string ToStringLine()
	{
		IEnumerable<string> SerializedElements()
		{
			yield return StartedTime.ToUnixTimeSeconds().ToString();
			yield return OutPoint.ToString();
			switch (Offense)
			{
				case RoundDisruption rd:
					yield return nameof(RoundDisruption);
					yield return rd.Value.Satoshi.ToString();
					yield return rd.DisruptedRoundIds.First().ToString();
					yield return rd.Method switch
					{
						RoundDisruptionMethod.DidNotConfirm => "didn't confirm",
						RoundDisruptionMethod.DidNotSignalReadyToSign => "didn't signal ready to sign",
						RoundDisruptionMethod.DidNotSign => "didn't sign",
						RoundDisruptionMethod.DoubleSpent => "double spent",
						_ => throw new NotImplementedException("Unknown round disruption method.")
					};
					foreach (var disruptedRoundId in rd.DisruptedRoundIds.Skip(1))
					{
						yield return disruptedRoundId.ToString();
					}
					break;

				case BackendStabilitySafety backendStabilitySafety:
					yield return nameof(BackendStabilitySafety);
					yield return backendStabilitySafety.RoundId.ToString();
					break;

				case FailedToVerify fv:
					yield return nameof(FailedToVerify);
					yield return fv.VerifiedInRoundId.ToString();
					yield return fv.RecommendedBanTime.ToString();
					yield return fv.Provider;
					break;

				case Inherited inherited:
					yield return nameof(Inherited);
					foreach (var ancestor in inherited.Ancestors)
					{
						yield return ancestor.ToString();
					}
					foreach (var reason in inherited.InputBannedReasonEnums)
					{
						yield return reason.ToString();
					}
					break;

				case Cheating cheating:
					yield return nameof(Cheating);
					yield return cheating.RoundId.ToString();
					break;

				default:
					throw new NotImplementedException("Cannot serialize an unknown offense type.");
			}
		}

		return string.Join(Separator, SerializedElements());
	}

	public static Offender FromStringLine(string str)
	{
		var parts = str.Split(Separator);
		var startedTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[0]));
		var outpoint = OutPoint.Parse(parts[1]);

		Offense offense = parts[2] switch
		{
			nameof(RoundDisruption) =>
				new RoundDisruption(
					parts.Skip(6).Select(x => uint256.Parse(x)).Prepend(uint256.Parse(parts[4])),
					Money.Satoshis(long.Parse(parts[3])),

					parts[5] switch
					{
						"didn't confirm" => RoundDisruptionMethod.DidNotConfirm,
						"didn't signal ready to sign" => RoundDisruptionMethod.DidNotSignalReadyToSign,
						"didn't sign" => RoundDisruptionMethod.DidNotSign,
						"double spent" => RoundDisruptionMethod.DoubleSpent,
						_ => throw new NotImplementedException("Unknown round disruption method.")
					}),
			nameof(BackendStabilitySafety) =>
				new BackendStabilitySafety(uint256.Parse(parts[3])),
			nameof(FailedToVerify) =>
				new FailedToVerify(
					VerifiedInRoundId: uint256.Parse(parts[3]),
					RecommendedBanTime: parts.Length > 4 ? TimeSpan.Parse(parts[4], CultureInfo.InvariantCulture) : TimeSpan.Zero,
					Provider: parts.Length > 5 ? parts[5] : ""),
			nameof(Inherited) =>
				ParseInheritedOffense(),
			nameof(Cheating) =>
				new Cheating(uint256.Parse(parts[3])),
			_ => throw new NotImplementedException("Cannot deserialize an unknown offense type.")
		};

		return new Offender(outpoint, startedTime, offense);

		Offense ParseInheritedOffense()
		{
			List<OutPoint> ancestors = [];
			List<InputBannedReasonEnum> reasons = [];

			foreach (var ancestorOrReason in parts.Skip(3))
			{
				if (OutPoint.TryParse(ancestorOrReason, out var outPoint))
				{
					ancestors.Add(outPoint!);
				}
				else if (Enum.TryParse<InputBannedReasonEnum>(ancestorOrReason, true, out var reason))
				{
					reasons.Add(reason);
				}
			}

			return new Inherited(ancestors.ToArray(), reasons.ToArray());
		}
	}

	public InputBannedReasonEnum[] GetReasonEnums()
	{
		return GetReasonEnums(this);
	}

	private static InputBannedReasonEnum[] GetReasonEnums(Offender offender)
	{
		List<InputBannedReasonEnum> reasonEnums = [];

		switch (offender.Offense)
		{
			case RoundDisruption rd:
				switch (rd.Method)
				{
					case RoundDisruptionMethod.DidNotConfirm:
						reasonEnums.Add(InputBannedReasonEnum.RoundDisruptionMethodDidNotConfirm);
						break;

					case RoundDisruptionMethod.DidNotSignalReadyToSign:
						reasonEnums.Add(InputBannedReasonEnum.RoundDisruptionMethodDidNotSignalReadyToSign);
						break;

					case RoundDisruptionMethod.DidNotSign:
						reasonEnums.Add(InputBannedReasonEnum.RoundDisruptionMethodDidNotSign);
						break;

					case RoundDisruptionMethod.DoubleSpent:
						reasonEnums.Add(InputBannedReasonEnum.RoundDisruptionMethodDoubleSpent);
						break;

					default:
						throw new NotImplementedException("Unknown round disruption method.");
				}
				break;

			case BackendStabilitySafety:
				reasonEnums.Add(InputBannedReasonEnum.BackendStabilitySafety);
				break;

			case FailedToVerify ftv:
				if (string.Equals(ftv.Provider, "local", StringComparison.InvariantCultureIgnoreCase))
				{
					reasonEnums.Add(InputBannedReasonEnum.LocalCoinVerifier);
				}
				else
				{
					reasonEnums.Add(InputBannedReasonEnum.FailedToVerify);
				}
				break;

			case Inherited inherited:
				reasonEnums.Add(InputBannedReasonEnum.Inherited);
				reasonEnums.AddRange(inherited.InputBannedReasonEnums);
				break;

			case Cheating:
				reasonEnums.Add(InputBannedReasonEnum.Cheating);
				break;

			default:
				throw new NotImplementedException("Unknown offense type.");
		}

		return reasonEnums.Distinct().ToArray();
	}
}
