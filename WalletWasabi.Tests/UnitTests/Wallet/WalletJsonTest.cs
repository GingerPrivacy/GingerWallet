using GingerCommon.Static;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.BuySell;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class WalletJsonTest
{
	[Fact]
	public void WalletJsonSaveTest()
	{
		var random = TestRandom.Get(0);
		var mnemonic = new Mnemonic(Wordlist.English, random.GetBytes(16));

		string password = "topsecret1234";
		var keyManager = KeyManager.CreateNew(mnemonic, password, Network.Main);
		keyManager.GetNextReceiveKey(new LabelsArray(["Alice", "Bob"]));
		keyManager.BuySellWalletData.Orders = [SampleOrder];
		keyManager.ExcludedCoinsFromCoinJoin.Add(new OutPoint(new uint256(random.GetBytes(32)), 27));
		keyManager.Attributes.CoinJoinTransactions.Add(new uint256(random.GetBytes(32)));

		var jsonKeyManager = System.Text.Json.JsonSerializer.Serialize(keyManager, KeyManager.JsonOptions).StandardLineEndings();
		Assert.Equal(JsonKeyManager, jsonKeyManager);
		var jsonWalletAttributes = System.Text.Json.JsonSerializer.Serialize(keyManager.Attributes, KeyManager.JsonOptions).StandardLineEndings();
		Assert.Equal(JsonWalletAttributes, jsonWalletAttributes);

		var keyManagerLoaded = System.Text.Json.JsonSerializer.Deserialize<KeyManager>(jsonKeyManager, KeyManager.JsonOptions);
		Assert.NotNull(keyManagerLoaded);
		var walletAttributesLoaded = System.Text.Json.JsonSerializer.Deserialize<WalletAttributes>(jsonKeyManager, KeyManager.JsonOptions);
		Assert.NotNull(walletAttributesLoaded);
		keyManagerLoaded.Attributes = walletAttributesLoaded;

		var jsonKeyManagerResave = System.Text.Json.JsonSerializer.Serialize(keyManager, KeyManager.JsonOptions).StandardLineEndings();
		var jsonWalletAttributesResave = System.Text.Json.JsonSerializer.Serialize(keyManager.Attributes, KeyManager.JsonOptions).StandardLineEndings();

		Assert.Equal(jsonKeyManager, jsonKeyManagerResave);
		Assert.Equal(jsonWalletAttributes, jsonWalletAttributesResave);
	}

	public static BuySellClientModels.GetOrderResponseItem SampleOrder = new()
	{
		RedirectUrl = new Uri("https://buyfoo.invalid"),
		OrderId = "1234",
		OrderType = BuySellClientModels.SupportedFlow.Buy,
		ProviderCode = "abcd",
		CurrencyFrom = "USD",
		CurrencyTo = "EUR",
		AmountFrom = 100,
		Country = "NONE",
		PaymentMethod = "card",
		CreatedAt = DateTimeOffset.UnixEpoch,
		Status = BuySellClientModels.OrderStatus.Failed,
		UpdatedAt = DateTimeOffset.UnixEpoch,
		ExternalUserId = "me",
		ExternalOrderId = "test",
		State = null,
		Ip = "127.0.0.1",
		WalletAddress = "bcp1234",
		WalletExtraId = "pass",
		RefundAddress = "bcp4567",
		UserAgent = "secret",
		Metadata = "none",
		PayinAmount = 1.1m,
		PayoutAmount = 2.2m,
		PayinCurrency = "ABC",
		PayoutCurrency = "CDE",
	};

	public static string JsonKeyManager = """
		{
		  "EncryptedSecret": "6PYSocGEa4k2XEDypqgMLSVfE4pNemeiioTNzgdMb1H4X2Geny2gpjQsgC",
		  "ChainCode": "ZMqSU1H1VTgS1XokqYMyKCQfD5zxuF0OFqdJIhzab54=",
		  "MasterFingerprint": "a60e43c8",
		  "ExtPubKey": "xpub6BkRV4gFz75r7rp43Qcn6pZ38EVwk4R3aseBx4pccRxAKHLsP5myL2oLDcXg5zH8sLoc9yV938k5zzu2QBQefhRc9gzqqrMcWooT1gmuVbj",
		  "TaprootExtPubKey": "xpub6CdWEHSNG669jMU4JTT7GdsuFjAr5m4tCuUf7nynEpFoe2rqjnt8CXCJeBuvY3hV7NxSJ2gtf9Z4Kfusn2bw6LYFbtKnJWT2JgZJUA6bHdy",
		  "MinGapLimit": 22,
		  "AccountKeyPath": "84'/0'/0'",
		  "TaprootAccountKeyPath": "86'/0'/0'",
		  "BlockchainState": {
		    "Network": "Main",
		    "Height": "0"
		  },
		  "PreferPsbtWorkflow": false,
		  "AutoCoinJoin": false,
		  "PlebStopThreshold": "0.003",
		  "Icon": null,
		  "AnonScoreTarget": 20,
		  "SafeMiningFeeRate": 10,
		  "FeeRateMedianTimeFrameHours": 0,
		  "IsCoinjoinProfileSelected": false,
		  "RedCoinIsolation": false,
		  "CoinjoinSkipFactors": "1_1_1",
		  "ExcludedCoinsFromCoinJoin": [
		    "4C77F313AE307CA91B188AABBC386C34BA563A10E83908732FC1523BE81E22B21B000000"
		  ],
		  "BuySellWalletData": {
		    "Orders": [
		      {
		        "RedirectUrl": "https://buyfoo.invalid",
		        "OrderId": "1234",
		        "ExternalUserId": "me",
		        "ExternalOrderId": "test",
		        "OrderType": 0,
		        "ProviderCode": "abcd",
		        "CurrencyFrom": "USD",
		        "CurrencyTo": "EUR",
		        "AmountFrom": 100,
		        "Country": "NONE",
		        "State": null,
		        "Ip": "127.0.0.1",
		        "WalletAddress": "bcp1234",
		        "WalletExtraId": "pass",
		        "RefundAddress": "bcp4567",
		        "PaymentMethod": "card",
		        "UserAgent": "secret",
		        "Metadata": "none",
		        "CreatedAt": "1970-01-01T00:00:00+00:00",
		        "Status": 5,
		        "PayinAmount": 1.1,
		        "PayoutAmount": 2.2,
		        "PayinCurrency": "ABC",
		        "PayoutCurrency": "CDE",
		        "UpdatedAt": "1970-01-01T00:00:00+00:00"
		      }
		    ]
		  },
		  "HdPubKeys": [
		    {
		      "PubKey": "0329d4d8998d9364a21bbf1a181b3593d99450376bf3ade27e8b5f797251b6071b",
		      "FullKeyPath": "84'/0'/0'/0/0",
		      "Label": "Alice, Bob",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "028048fcf3e50e80621e0e24416507804900778d0b748e0cd440f5ede8a3615c74",
		      "FullKeyPath": "84'/0'/0'/0/1",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "0372e7279e51aa9390d186acb23ccd53db90d36ed047897dd684162f02da2307e1",
		      "FullKeyPath": "84'/0'/0'/0/2",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "034d0ca0a421604c2b2dfaaf3b4910bee76dc03c2d64d555b27af61e3f0ac5cf03",
		      "FullKeyPath": "84'/0'/0'/0/3",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "03d35ddef29d4364001c434340187586634ba5b1006c69386ebd63b0273ca9565c",
		      "FullKeyPath": "84'/0'/0'/0/4",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "03e09b9f7607141fc0ee26848d447545baea720bbc1eeabd0644cf9583b2d2189f",
		      "FullKeyPath": "84'/0'/0'/0/5",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "02b7140dc947e784b9d4fa919315c219007968e41f437b425cea8f86a0c857d2f9",
		      "FullKeyPath": "84'/0'/0'/0/6",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "03894d801e86245942b07c343c174687469853d1fc9611670255c406a8d1127915",
		      "FullKeyPath": "84'/0'/0'/0/7",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "020071fbb429f74c66c79c6b29f92102d063941bcb2175bf11baac198c62c326ad",
		      "FullKeyPath": "84'/0'/0'/0/8",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "035e03b6f30593531ce67986e10c13db969ab14bfa2ff75e8cf31458032229e96c",
		      "FullKeyPath": "84'/0'/0'/0/9",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "03985fdc4b6673b0cfeecad6cd24bad60a3bc20172487df47cd49281b3707d23de",
		      "FullKeyPath": "84'/0'/0'/0/10",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "03583ed6cc5e873b5ef6e8727fcf52b21f2052db2094b8b9297788bf93192470c8",
		      "FullKeyPath": "84'/0'/0'/0/11",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "031b93cc2f7ce59e8d87c77d08c806cfc6f1c827a18a26d4574dbf12df410bc184",
		      "FullKeyPath": "84'/0'/0'/0/12",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "02e2aae2894430aa0bac9fa74d6d76c7847e411ed4c40f4ef679241b1a647eae5a",
		      "FullKeyPath": "84'/0'/0'/0/13",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "0286a8cf06eda1280f60885347110cae8ca945462c9aedbe5cd37edead50dc25ff",
		      "FullKeyPath": "84'/0'/0'/0/14",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "025019512fc7efe5ce78bebe435e088b6de40f9ca13ee102b69820ebd209c56def",
		      "FullKeyPath": "84'/0'/0'/0/15",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "03bb08805e0625299ec35484c8d986e447a4da9c270d7e66c3116cb0c902fdf432",
		      "FullKeyPath": "84'/0'/0'/0/16",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "025f8c96df79854b8de3085982f0179737212dc623a0e6b3ce250cfa999971a76f",
		      "FullKeyPath": "84'/0'/0'/0/17",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "0369daa60d4c4041f18d23612b95d704a8a70ce35be5058f18eb123db9e7d00085",
		      "FullKeyPath": "84'/0'/0'/0/18",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "03b2a59580e63414d83baff5a8be3d5d9d2a5a9c2336f684dc26bcb74d31ed0cd6",
		      "FullKeyPath": "84'/0'/0'/0/19",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "030334fd2f1c388ccb951e6d77c60d8fd36629566e3938afa9114d848403cfe192",
		      "FullKeyPath": "84'/0'/0'/0/20",
		      "Label": "",
		      "KeyState": 0
		    },
		    {
		      "PubKey": "02456f292d9376fadf894ec34b110177044df4602ff07fd1169a3fe517438d088b",
		      "FullKeyPath": "84'/0'/0'/0/21",
		      "Label": "",
		      "KeyState": 0
		    }
		  ]
		}
		""";

	public static string JsonWalletAttributes = """
		{
		  "AutoCoinJoin": false,
		  "PlebStopThreshold": "0.003",
		  "Icon": null,
		  "AnonScoreTarget": 20,
		  "SafeMiningFeeRate": 10,
		  "FeeRateMedianTimeFrameHours": 0,
		  "IsCoinjoinProfileSelected": false,
		  "RedCoinIsolation": false,
		  "CoinjoinSkipFactors": "1_1_1",
		  "BuySellWalletData": {
		    "Orders": [
		      {
		        "RedirectUrl": "https://buyfoo.invalid",
		        "OrderId": "1234",
		        "ExternalUserId": "me",
		        "ExternalOrderId": "test",
		        "OrderType": 0,
		        "ProviderCode": "abcd",
		        "CurrencyFrom": "USD",
		        "CurrencyTo": "EUR",
		        "AmountFrom": 100,
		        "Country": "NONE",
		        "State": null,
		        "Ip": "127.0.0.1",
		        "WalletAddress": "bcp1234",
		        "WalletExtraId": "pass",
		        "RefundAddress": "bcp4567",
		        "PaymentMethod": "card",
		        "UserAgent": "secret",
		        "Metadata": "none",
		        "CreatedAt": "1970-01-01T00:00:00+00:00",
		        "Status": 5,
		        "PayinAmount": 1.1,
		        "PayoutAmount": 2.2,
		        "PayinCurrency": "ABC",
		        "PayoutCurrency": "CDE",
		        "UpdatedAt": "1970-01-01T00:00:00+00:00"
		      }
		    ]
		  },
		  "CoinJoinCoinSelectionSettings": {
		    "UseExperimentalCoinSelector": false,
		    "ForceUsingLowPrivacyCoins": false,
		    "WeightedAnonymityLossNormal": 3,
		    "ValueLossRateNormal": 0.005,
		    "TargetCoinCountPerBucket": 10,
		    "UseOldCoinSelectorAsFallback": true
		  },
		  "CoinJoinTransactions": [
		    "26840cbedaa0698afeb5acae887e487ec658dc810b2ca36cfb358de4d8c36b7d"
		  ],
		  "ExcludedCoinsFromCoinJoin": [
		    "4C77F313AE307CA91B188AABBC386C34BA563A10E83908732FC1523BE81E22B21B000000"
		  ],
		  "CoinJoinOutputs": [],
		  "HdPubKeys": [
		    {
		      "PubKey": "0329d4d8998d9364a21bbf1a181b3593d99450376bf3ade27e8b5f797251b6071b",
		      "FullKeyPath": "84'/0'/0'/0/0",
		      "Label": "Alice, Bob",
		      "KeyState": 0
		    }
		  ]
		}
		""";
}
