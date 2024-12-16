using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.DoSPrevention;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

namespace WalletWasabi.Tests.Helpers;

/// <summary>
/// Builder class for <see cref="Arena"/>.
/// </summary>
public class ArenaTestFactory
{
	public static ArenaTestFactory Default => new();

	public TimeSpan? Period { get; set; }
	public Network? Network { get; set; }
	public WabiSabiConfig? Config { get; set; }
	public IRPCClient? Rpc { get; set; }
	public Prison? Prison { get; set; }
	public RoundParameterFactory? RoundParameterFactory { get; set; }
	public ICoinJoinIdStore? CoinJoinIdStore { get; set; }

	/// <param name="rounds">Rounds to initialize <see cref="Arena"/> with.</param>
	public Arena Create(params Round[] rounds)
	{
		TimeSpan period = Period ?? TimeSpan.FromHours(1);
		Prison prison = Prison ?? WabiSabiTestFactory.CreatePrison();
		WabiSabiConfig config = Config ?? WabiSabiBackendFactory.Instance.CreateWabiSabiConfig();
		IRPCClient rpc = Rpc ?? WabiSabiTestFactory.CreatePreconfiguredRpcClient();
		Network network = Network ?? Network.Main;
		ICoinJoinIdStore coinJoinIdStore = CoinJoinIdStore ?? new CoinJoinIdStore();
		RoundParameterFactory roundParameterFactory = RoundParameterFactory ?? CreateRoundParameterFactory(config, network);

		Arena arena = WabiSabiBackendFactory.Instance.CreateArena(period, config, rpc, prison, coinJoinIdStore, roundParameterFactory);

		foreach (var round in rounds)
		{
			arena.Rounds.Add(round);
		}

		return arena;
	}

	public Task<Arena> CreateAndStartAsync(params Round[] rounds)
		=> CreateAndStartAsync(rounds, CancellationToken.None);

	public async Task<Arena> CreateAndStartAsync(Round[] rounds, CancellationToken cancellationToken = default)
	{
		Arena? toDispose = null;

		try
		{
			toDispose = Create(rounds);
			Arena arena = toDispose;
			await arena.StartAsync(cancellationToken).ConfigureAwait(false);
			toDispose = null;
			return arena;
		}
		finally
		{
			toDispose?.Dispose();
		}
	}

	public ArenaTestFactory With(ICoinJoinIdStore store)
	{
		CoinJoinIdStore = store;
		return this;
	}

	public ArenaTestFactory With(IRPCClient rpc)
	{
		Rpc = rpc;
		return this;
	}

	public ArenaTestFactory With(RoundParameterFactory roundParameterFactory)
	{
		RoundParameterFactory = roundParameterFactory;
		return this;
	}

	public static ArenaTestFactory From(WabiSabiConfig cfg) => new() { Config = cfg };

	public static ArenaTestFactory From(WabiSabiConfig cfg, Prison prison) => new() { Config = cfg, Prison = prison };

	public static ArenaTestFactory From(WabiSabiConfig cfg, IRPCClient mockRpc, Prison prison) => new() { Config = cfg, Rpc = mockRpc, Prison = prison };

	private static RoundParameterFactory CreateRoundParameterFactory(WabiSabiConfig cfg, Network network) =>
		WabiSabiTestFactory.CreateRoundParametersFactory(cfg, network, Constants.P2wpkhInputVirtualSize + Constants.P2wpkhOutputVirtualSize);
}
