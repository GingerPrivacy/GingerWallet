using NBitcoin;
using System.IO;
using System.Text.Json;
using WalletWasabi.Bases;
using WalletWasabi.Daemon;
using WalletWasabi.Tests.TestCommon;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.StandaloneTests;

public class ConfigTests
{
	[Fact]
	public void CheckConfigFileChangeTest()
	{
		string workDirectory = TestDirectory.Get();
		string configPath = Path.Combine(workDirectory, $"{nameof(CheckConfigFileChangeTest)}.json");

		// Create config and store it.
		WabiSabiConfig config = new WabiSabiConfig();
		config.SetFilePath(configPath);
		config.ToFile();

		// Check that the stored config corresponds to the expected default config.
		{
			string expectedFileContents = GetWasabiConfigString();
			string actualFileContents = ReadAllTextAndNormalize(configPath);

			Assert.Equal(expectedFileContents, actualFileContents);

			// No change was done.
			Assert.False(ConfigManager.CheckFileChange(configPath, config));
		}
		{
			// Change coordination fee rate.
			config.CoordinationFeeRate = new CoordinationFeeRate(rate: 0.006m, plebsDontPayThreshold: Money.Coins(0.03m));

			// Change should be detected.
			Assert.True(ConfigManager.CheckFileChange(configPath, config));

			// Now store and check that JSON is as expected.
			config.ToFile();

			string expectedFileContents = GetWasabiConfigString(coordinationFeeRate: 0.006m);
			string actualFileContents = ReadAllTextAndNormalize(configPath);

			Assert.Equal(expectedFileContents, actualFileContents);
		}
	}

	public static string ReadAllTextAndNormalize(string configPath) => File.ReadAllText(configPath).ReplaceLineEndings("\n");

	[Fact]
	public void ToFileAndLoadFileTest()
	{
		string workDirectory = TestDirectory.Get();
		string configPath = Path.Combine(workDirectory, $"{nameof(ToFileAndLoadFileTest)}.json");

		// We need OS culture dependent values
		PersistentConfig config = new() { LocalBitcoinCoreDataDir = TestLocalBitcoinCoreDataDir };

		string expected = GetPersistentConfigString(config.GroupSeparator, config.DecimalSeparator, config.ExchangeCurrency);

		string storedJson = ConfigManagerNg.ToFile(configPath, config);
		Assert.Equal(expected, storedJson.ReplaceLineEndings("\n"));

		PersistentConfig readConfig = ConfigManagerNg.LoadFile<PersistentConfig>(configPath);

		Assert.Equal(TestLocalBitcoinCoreDataDir, readConfig.LocalBitcoinCoreDataDir);
		Assert.True(config.DeepEquals(readConfig));

		string reserialized = JsonSerializer.Serialize(readConfig, ConfigManagerNg.DefaultOptions);
		Assert.Equal(expected, reserialized.ReplaceLineEndings("\n"));
	}

	public static string GetWasabiConfigString(decimal coordinationFeeRate = 0.003m)
		=> $$"""
			{
			  "ConfirmationTarget": 108,
			  "DoSSeverity": "0.10",
			  "DoSMinTimeForFailedToVerify": "31d 0h 0m 0s",
			  "DoSSimplifiedPunishment": "1d 0h 0m 0s",
			  "DoSMinTimeForCheating": "1d 0h 0m 0s",
			  "DoSPenaltyFactorForDisruptingConfirmation": 0.2,
			  "DoSPenaltyFactorForDisruptingSignalReadyToSign": 1.0,
			  "DoSPenaltyFactorForDisruptingSigning": 1.0,
			  "DoSPenaltyFactorForDisruptingByDoubleSpending": 3.0,
			  "DoSMinTimeInPrison": "0d 0h 20m 0s",
			  "MinRegistrableAmount": "0.00005",
			  "MaxRegistrableAmount": "43000.00",
			  "MinFeeAmount": "0.00002",
			  "AllowNotedInputRegistration": true,
			  "StandardInputRegistrationTimeout": "0d 1h 0m 0s",
			  "BlameInputRegistrationTimeout": "0d 0h 3m 0s",
			  "CreateNewRoundBeforeInputRegEnd": "0d 0h 1m 0s",
			  "ConnectionConfirmationTimeout": "0d 0h 1m 0s",
			  "OutputRegistrationTimeout": "0d 0h 1m 0s",
			  "TransactionSigningTimeout": "0d 0h 1m 0s",
			  "FailFastOutputRegistrationTimeout": "0d 0h 3m 0s",
			  "FailFastTransactionSigningTimeout": "0d 0h 1m 0s",
			  "RoundExpiryTimeout": "0d 0h 5m 0s",
			  "MaxInputCountByRound": 100,
			  "MinInputCountByRoundMultiplier": 0.5,
			  "MinInputCountByBlameRoundMultiplier": 0.4,
			  "RoundDestroyerThreshold": 375,
			  "CoordinationFeeRate": {
			    "Rate": {{coordinationFeeRate}},
			    "PlebsDontPayThreshold": 3000000
			  },
			  "CoordinatorExtPubKey": "xpub6C13JhXzjAhVRgeTcRSWqKEPe1vHi3Tmh2K9PN1cZaZFVjjSaj76y5NNyqYjc2bugj64LVDFYu8NZWtJsXNYKFb9J94nehLAPAKqKiXcebC",
			  "CoordinatorExtPubKeyCurrentDepth": 1,
			  "MaxSuggestedAmountBase": "0.10",
			  "IsCoinVerifierEnabled": false,
			  "CoinVerifiers": [
			    {
			      "Name": "",
			      "ApiUrl": "",
			      "ApiKey": "",
			      "ApiSecret": "",
			      "RiskSettings": ""
			    }
			  ],
			  "CoinVerifierStartBefore": "0d 0h 2m 0s",
			  "CoinVerifierRequiredConfirmations": 3,
			  "CoinVerifierRequiredConfirmationAmount": "1.00",
			  "ReleaseFromWhitelistAfter": "31d 0h 0m 0s",
			  "RoundParallelization": 1,
			  "CoordinatorIdentifier": "CoinJoinCoordinatorIdentifier",
			  "AllowP2wpkhInputs": true,
			  "AllowP2trInputs": true,
			  "AllowP2wpkhOutputs": true,
			  "AllowP2trOutputs": true,
			  "AllowP2pkhOutputs": false,
			  "AllowP2shOutputs": false,
			  "AllowP2wshOutputs": false,
			  "DelayTransactionSigning": false,
			  "IsCoordinationEnabled": true
			}
			""".ReplaceLineEndings("\n");

	private static string TestLocalBitcoinCoreDataDir = "LocalBitcoinCoreDataDir";

	public static string GetPersistentConfigString(string groupSeparator, string decimalSeparator, string exchangeCurrency)
	=> $$"""
			{
			  "Network": "Main",
			  "MainNetBackendUri": "https://api.gingerwallet.io/",
			  "TestNetClearnetBackendUri": "https://api.gingerwallet.co/",
			  "RegTestBackendUri": "http://localhost:37127/",
			  "MainNetCoordinatorUri": "https://api.gingerwallet.io/",
			  "TestNetCoordinatorUri": "https://api.gingerwallet.co/",
			  "RegTestCoordinatorUri": "http://localhost:37127/",
			  "UseTor": "Enabled",
			  "TerminateTorOnExit": false,
			  "TorBridges": [],
			  "DownloadNewVersion": true,
			  "StartLocalBitcoinCoreOnStartup": false,
			  "StopLocalBitcoinCoreOnShutdown": true,
			  "LocalBitcoinCoreDataDir": "{{TestLocalBitcoinCoreDataDir}}",
			  "MainNetBitcoinP2pEndPoint": "127.0.0.1:8333",
			  "TestNetBitcoinP2pEndPoint": "127.0.0.1:18333",
			  "RegTestBitcoinP2pEndPoint": "127.0.0.1:18444",
			  "JsonRpcServerEnabled": false,
			  "JsonRpcUser": "",
			  "JsonRpcPassword": "",
			  "JsonRpcServerPrefixes": [
			    "http://127.0.0.1:37128/",
			    "http://localhost:37128/"
			  ],
			  "DustThreshold": "0.00005",
			  "EnableGpu": true,
			  "CoordinatorIdentifier": "CoinJoinCoordinatorIdentifier",
			  "MaxCoordinationFeeRate": 0.003,
			  "MaxCoinJoinMiningFeeRate": 300.0,
			  "AbsoluteMinInputCount": 6,
			  "MaxBlockRepositorySize": 1000,
			  "Language": 1,
			  "ExchangeCurrency": "{{exchangeCurrency}}",
			  "GroupSeparator": "{{groupSeparator}}",
			  "DecimalSeparator": "{{decimalSeparator}}",
			  "ExtraNostrPubKey": "",
			  "BtcFractionGroup": [
			    4,
			    4
			  ],
			  "FeeRateEstimationProvider": "MempoolSpace"
			}
			""".ReplaceLineEndings("\n");
}
