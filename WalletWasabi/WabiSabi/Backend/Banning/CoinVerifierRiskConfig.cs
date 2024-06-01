using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifierRiskConfig
{
	public CoinVerifierRiskConfig(WabiSabiConfig config) : this(config.RiskFlags, config.RiskScores)
	{
	}

	public CoinVerifierRiskConfig(IEnumerable<int>? riskFlags, IEnumerable<double>? riskScores)
	{
		RiskFlags = riskFlags?.ToList() ?? new();
		RiskScores = riskScores?.ToList() ?? new();

		RiskScoreLimitDefault = RiskScores.FirstOrDefault(0.5);
		RiskScoreLimitObfuscating = RiskScores.LastOrDefault(1.0);
	}

	public List<int> RiskFlags { get; }
	public List<double> RiskScores { get; }

	public double RiskScoreLimitDefault { get; }
	public double RiskScoreLimitObfuscating { get; }
}
