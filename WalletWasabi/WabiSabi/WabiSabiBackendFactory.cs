using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.DoSPrevention;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Recommendation;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WabiSabi.Backend.Models;
using System.Net.Http;

namespace WalletWasabi.WabiSabi;

public class WabiSabiBackendFactory
{
	public WabiSabiConfig CreateWabiSabiConfig(string filePath)
	{
		var config = CreateWabiSabiConfig();
		config.SetFilePath(filePath);
		return config;
	}

	public virtual WabiSabiCoordinator CreateCoordinator(CoordinatorParameters parameters, IRPCClient rpc, ICoinJoinIdStore coinJoinIdStore, CoinJoinScriptStore coinJoinScriptStore, IHttpClientFactory httpClientFactory, DenominationFactory? denominationFactory, CoinVerifier? coinVerifier, MiningFeeRateEstimator? miningFeeRateEstimator)
	{
		return new WabiSabiCoordinator(parameters, rpc, coinJoinIdStore, coinJoinScriptStore, httpClientFactory, denominationFactory, coinVerifier, miningFeeRateEstimator);
	}

	public virtual WabiSabiConfig CreateWabiSabiConfig()
	{
		return new WabiSabiConfig();
	}

	public virtual Arena CreateArena(
		TimeSpan period,
		WabiSabiConfig config,
		IRPCClient rpc,
		Prison prison,
		ICoinJoinIdStore coinJoinIdStore,
		RoundParameterFactory roundParameterFactory,
		DenominationFactory? denominationFactory = null,
		CoinJoinTransactionArchiver? archiver = null,
		CoinJoinScriptStore? coinJoinScriptStore = null,
		CoinVerifier? coinVerifier = null,
		MiningFeeRateEstimator? miningFeeRateEstimator = null)
	{
		return new Arena(period, config, rpc, prison, coinJoinIdStore, roundParameterFactory, denominationFactory, archiver, coinJoinScriptStore, coinVerifier, miningFeeRateEstimator);
	}

	// Allow extra checks
	public virtual SigningState FinalizeConstructionState(ConstructionState constructionState)
	{
		var parameters = constructionState.Parameters;
		if (constructionState.EstimatedVsize > parameters.MaxTransactionSize)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.SizeLimitExceeded, $"Transaction size is {constructionState.EstimatedVsize} bytes, which exceeds the limit of {parameters.MaxTransactionSize} bytes.");
		}

		var signingState = new SigningState(parameters, constructionState.Events);

		if (constructionState.EffectiveFeeRate < parameters.MiningFeeRate)
		{
			var tx = signingState.CreateUnsignedTransaction();
			var txHex = tx.ToHex();
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InsufficientFees, $"Effective fee rate {constructionState.EffectiveFeeRate} is less than required {parameters.MiningFeeRate}. RawTx: {txHex}");
		}

		return signingState;
	}

	// Singleton creation

	private static object FactoryLock = new();
	private static WabiSabiBackendFactory? InternalIstance = null;

	public static WabiSabiBackendFactory Instance
	{
		get
		{
			if (InternalIstance is null)
			{
				lock (FactoryLock)
				{
					Type factoryType = typeof(WabiSabiBackendFactory);
					var assembly = Assembly.GetAssembly(factoryType);
					if (assembly is not null)
					{
						var types = assembly.GetTypes().Where(type => type.IsSubclassOf(factoryType) && !type.IsAbstract);
						if (types.Any())
						{
							InternalIstance ??= Activator.CreateInstance(types.Single()) as WabiSabiBackendFactory;
						}
					}

					InternalIstance ??= new WabiSabiBackendFactory();
				}
			}
			return InternalIstance;
		}
	}
}
