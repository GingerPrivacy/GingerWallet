using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;
using static WalletWasabi.Blockchain.Keys.WpkhOutputDescriptorHelper;
using static WalletWasabi.BuySell.BuySellClientModels;

namespace WalletWasabi.Blockchain.Keys;

[JsonObject(MemberSerialization.OptIn)]
public class KeyManager
{
	public const int AbsoluteMinGapLimit = 21;
	public const int MaxGapLimit = 10_000;

	private static readonly JsonConverter[] JsonConverters =
	{
		new BitcoinEncryptedSecretNoECJsonConverter(),
		new ByteArrayJsonConverter(),
		new HDFingerprintJsonConverter(),
		new ExtPubKeyJsonConverter(),
		new KeyPathJsonConverter(),
		new MoneyBtcJsonConverter(),
		new CoinjoinSkipFactorsJsonConverter(),
	};

	[JsonConstructor]
	public KeyManager(BitcoinEncryptedSecretNoEC? encryptedSecret, byte[]? chainCode, HDFingerprint? masterFingerprint, ExtPubKey extPubKey, ExtPubKey? taprootExtPubKey, int? minGapLimit, BlockchainState blockchainState, string? filePath = null, KeyPath? segwitAccountKeyPath = null, KeyPath? taprootAccountKeyPath = null)
	{
		Attributes = new();
		EncryptedSecret = encryptedSecret;
		ChainCode = chainCode;
		MasterFingerprint = masterFingerprint;
		SegwitExtPubKey = Guard.NotNull(nameof(extPubKey), extPubKey);
		TaprootExtPubKey = taprootExtPubKey;

		MinGapLimit = Math.Max(AbsoluteMinGapLimit, minGapLimit ?? 0);

		BlockchainState = blockchainState;

		SegwitAccountKeyPath = segwitAccountKeyPath ?? GetAccountKeyPath(BlockchainState.Network, ScriptPubKeyType.Segwit);
		SegwitExternalKeyGenerator = new HdPubKeyGenerator(SegwitExtPubKey.Derive(0), SegwitAccountKeyPath.Derive(0), MinGapLimit);
		SegwitInternalKeyGenerator = new HdPubKeyGenerator(SegwitExtPubKey.Derive(1), SegwitAccountKeyPath.Derive(1), MinGapLimit);

		TaprootAccountKeyPath = taprootAccountKeyPath ?? GetAccountKeyPath(BlockchainState.Network, ScriptPubKeyType.TaprootBIP86);
		if (TaprootExtPubKey is { })
		{
			TaprootExternalKeyGenerator = new HdPubKeyGenerator(TaprootExtPubKey.Derive(0), TaprootAccountKeyPath.Derive(0), MinGapLimit);
			TaprootInternalKeyGenerator = new HdPubKeyGenerator(TaprootExtPubKey.Derive(1), TaprootAccountKeyPath.Derive(1), MinGapLimit);
		}
		SetFilePath(filePath);

		ToFile();
	}

	public KeyManager(BitcoinEncryptedSecretNoEC encryptedSecret, byte[] chainCode, string password, Network network)
	{
		Attributes = new();
		BlockchainState = new BlockchainState(network);

		password ??= "";

		MinGapLimit = AbsoluteMinGapLimit;

		EncryptedSecret = Guard.NotNull(nameof(encryptedSecret), encryptedSecret);
		ChainCode = Guard.NotNull(nameof(chainCode), chainCode);
		var extKey = new ExtKey(encryptedSecret.GetKey(password), chainCode);

		MasterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();

		SegwitAccountKeyPath = GetAccountKeyPath(network, ScriptPubKeyType.Segwit);
		SegwitExtPubKey = extKey.Derive(SegwitAccountKeyPath).Neuter();

		TaprootAccountKeyPath = GetAccountKeyPath(network, ScriptPubKeyType.TaprootBIP86);
		TaprootExtPubKey = extKey.Derive(TaprootAccountKeyPath).Neuter();

		SegwitExternalKeyGenerator = new HdPubKeyGenerator(SegwitExtPubKey.Derive(0), SegwitAccountKeyPath.Derive(0), MinGapLimit);
		SegwitInternalKeyGenerator = new HdPubKeyGenerator(SegwitExtPubKey.Derive(1), SegwitAccountKeyPath.Derive(1), MinGapLimit);
		TaprootExternalKeyGenerator = new HdPubKeyGenerator(TaprootExtPubKey.Derive(0), TaprootAccountKeyPath.Derive(0), MinGapLimit);
		TaprootInternalKeyGenerator = new HdPubKeyGenerator(TaprootExtPubKey.Derive(1), TaprootAccountKeyPath.Derive(1), MinGapLimit);
	}

	[OnDeserialized]
	private void OnDeserializedMethod(StreamingContext context)
	{
		HdPubKeyCache.AddRangeKeys(HdPubKeys);
	}

	[OnSerializing]
	private void OnSerializingMethod(StreamingContext context)
	{
		HdPubKeys.Clear();
		HdPubKeys.AddRange(HdPubKeyCache.HdPubKeys.ToHashSet().OrderBy(x => x.FullKeyPath, new NBitcoinExtensions.KeyPathComparer()));
		MinGapLimit = Math.Max(SegwitExternalKeyGenerator.MinGapLimit, TaprootExternalKeyGenerator?.MinGapLimit ?? 0);
	}

	public static KeyPath GetAccountKeyPath(Network network, ScriptPubKeyType scriptPubKeyType) =>
		new((network.Name, scriptPubKeyType) switch
		{
			("TestNet", ScriptPubKeyType.Segwit) => "m/84h/1h/0h",
			("RegTest", ScriptPubKeyType.Segwit) => "m/84h/0h/0h",
			("Main", ScriptPubKeyType.Segwit) => "m/84h/0h/0h",
			("TestNet", ScriptPubKeyType.TaprootBIP86) => "m/86h/1h/0h",
			("RegTest", ScriptPubKeyType.TaprootBIP86) => "m/86h/0h/0h",
			("Main", ScriptPubKeyType.TaprootBIP86) => "m/86h/0h/0h",
			_ => throw new ArgumentException($"Unknown account for network '{network}' and script type '{scriptPubKeyType}'.")
		});

	public WpkhDescriptors GetOutputDescriptors(string password, Network network)
	{
		if (!MasterFingerprint.HasValue)
		{
			throw new InvalidOperationException($"{nameof(MasterFingerprint)} is not defined.");
		}

		return WpkhOutputDescriptorHelper.GetOutputDescriptors(network, MasterFingerprint.Value, GetMasterExtKey(password), SegwitAccountKeyPath);
	}

	#region Properties

	private WalletAttributes _attributes;

	public WalletAttributes Attributes
	{
		get => _attributes;
		[MemberNotNull(nameof(_attributes))]
		set
		{
			_attributes = value; _attributes.KeyManager = this;
		}
	}

	/// <remarks><c>null</c> if the watch-only mode is on.</remarks>
	[JsonProperty(PropertyName = "EncryptedSecret")]
	public BitcoinEncryptedSecretNoEC? EncryptedSecret { get; }

	/// <remarks><c>null</c> if the watch-only mode is on.</remarks>
	[JsonProperty(PropertyName = "ChainCode")]
	public byte[]? ChainCode { get; }

	[JsonProperty(PropertyName = "MasterFingerprint")]
	public HDFingerprint? MasterFingerprint { get; private set; }

	[JsonProperty(PropertyName = "ExtPubKey")]
	public ExtPubKey SegwitExtPubKey { get; }

	[JsonProperty(PropertyName = "TaprootExtPubKey")]
	public ExtPubKey? TaprootExtPubKey { get; private set; }

	[JsonProperty(PropertyName = "UseTurboSync")]
	public bool UseTurboSync { get; private set; } = true;

	[JsonProperty(PropertyName = "MinGapLimit")]
	public int MinGapLimit { get; private set; }

	[JsonProperty(PropertyName = "AccountKeyPath")]
	public KeyPath SegwitAccountKeyPath { get; private set; }

	[JsonProperty(PropertyName = "TaprootAccountKeyPath")]
	public KeyPath TaprootAccountKeyPath { get; private set; }

	[JsonProperty(PropertyName = "BlockchainState")]
	private BlockchainState BlockchainState { get; }

	[JsonProperty(PropertyName = "PreferPsbtWorkflow")]
	public bool PreferPsbtWorkflow { get; set; }

	[JsonProperty(PropertyName = "AutoCoinJoin")]
	public bool AutoCoinJoin { get => Attributes.AutoCoinJoin; set => Attributes.AutoCoinJoin = value; }

	/// <summary>
	/// Won't coinjoin automatically if the wallet balance is less than this.
	/// </summary>
	[JsonProperty(PropertyName = "PlebStopThreshold")]
	[JsonConverter(typeof(MoneyBtcJsonConverter))]
	public Money PlebStopThreshold { get => Attributes.PlebStopThreshold; set => Attributes.PlebStopThreshold = value; }

	[JsonProperty(PropertyName = "Icon")]
	public string? Icon { get => Attributes.Icon; private set => Attributes.Icon = value; }

	[JsonProperty(PropertyName = "AnonScoreTarget")]
	public int AnonScoreTarget { get => Attributes.AnonScoreTarget; set => Attributes.AnonScoreTarget = value; }

	[JsonProperty(PropertyName = "SafeMiningFeeRate")]
	public int SafeMiningFeeRate { get => Attributes.SafeMiningFeeRate; set => Attributes.SafeMiningFeeRate = value; }

	[JsonProperty(PropertyName = "FeeRateMedianTimeFrameHours")]
	public int FeeRateMedianTimeFrameHours { get => Attributes.FeeRateMedianTimeFrameHours; private set => Attributes.FeeRateMedianTimeFrameHours = value; }

	[JsonProperty(PropertyName = "IsCoinjoinProfileSelected")]
	public bool IsCoinjoinProfileSelected { get => Attributes.IsCoinjoinProfileSelected; set => Attributes.IsCoinjoinProfileSelected = value; }

	[JsonProperty(PropertyName = "RedCoinIsolation")]
	public bool RedCoinIsolation { get => Attributes.RedCoinIsolation; set => Attributes.RedCoinIsolation = value; }

	[JsonProperty(PropertyName = "CoinjoinSkipFactors")]
	public CoinjoinSkipFactors CoinjoinSkipFactors { get => Attributes.CoinjoinSkipFactors; set => Attributes.CoinjoinSkipFactors = value; }

	[JsonProperty(Order = 999, PropertyName = "HdPubKeys")]
	private List<HdPubKey> HdPubKeys { get; } = new();

	[JsonProperty(ItemConverterType = typeof(OutPointJsonConverter), PropertyName = "ExcludedCoinsFromCoinJoin")]
	public List<OutPoint> ExcludedCoinsFromCoinJoin { get => Attributes.ExcludedCoinsFromCoinJoin; private set => Attributes.ExcludedCoinsFromCoinJoin = value; }

	[JsonProperty(PropertyName = "BuySellWalletData")]
	public BuySellWalletData BuySellWalletData { get => Attributes.BuySellWalletData; set => Attributes.BuySellWalletData = value; }

	public string? FilePath { get; private set; }

	[MemberNotNullWhen(returnValue: false, nameof(EncryptedSecret))]
	[MemberNotNullWhen(returnValue: false, nameof(ChainCode))]
	public bool IsWatchOnly => EncryptedSecret is null;

	[MemberNotNullWhen(returnValue: true, nameof(MasterFingerprint))]
	public bool IsHardwareWallet => EncryptedSecret is null && MasterFingerprint is not null;

	internal HdPubKeyCache HdPubKeyCache { get; } = new();

	// `CriticalStateLock` is aimed to synchronize read/write access to the "critical" properties:
	// keys (stored in the `HdPubKeyCache`), minGapLimit, secrets, height, network.
	private object CriticalStateLock { get; } = new();

	public string? EncryptionKey { get; set; }

	#endregion Properties

	private HdPubKeyGenerator SegwitExternalKeyGenerator { get; set; }
	private HdPubKeyGenerator SegwitInternalKeyGenerator { get; }
	private HdPubKeyGenerator? TaprootExternalKeyGenerator { get; set; }
	private HdPubKeyGenerator? TaprootInternalKeyGenerator { get; }

	public string WalletName => string.IsNullOrWhiteSpace(FilePath) ? "" : Path.GetFileNameWithoutExtension(FilePath);

	public static KeyManager CreateNew(out Mnemonic mnemonic, string password, Network network, string? filePath = null)
	{
		mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
		return CreateNew(mnemonic, password, network, filePath);
	}

	public static KeyManager CreateNew(Mnemonic mnemonic, string password, Network network, string? filePath = null)
	{
		password ??= "";

		ExtKey extKey = mnemonic.DeriveExtKey(password);
		var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

		HDFingerprint masterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();
		BlockchainState blockchainState = new(network);
		KeyPath segwitAccountKeyPath = GetAccountKeyPath(network, ScriptPubKeyType.Segwit);
		ExtPubKey segwitExtPubKey = extKey.Derive(segwitAccountKeyPath).Neuter();

		KeyPath taprootAccountKeyPath = GetAccountKeyPath(network, ScriptPubKeyType.TaprootBIP86);
		ExtPubKey taprootExtPubKey = extKey.Derive(taprootAccountKeyPath).Neuter();

		return new KeyManager(encryptedSecret, extKey.ChainCode, masterFingerprint, segwitExtPubKey, taprootExtPubKey, AbsoluteMinGapLimit, blockchainState, filePath, segwitAccountKeyPath, taprootAccountKeyPath);
	}

	public static KeyManager CreateNewWatchOnly(ExtPubKey segwitExtPubKey, ExtPubKey taprootExtPubKey, string? filePath = null, int? minGapLimit = null)
	{
		return new KeyManager(null, null, null, segwitExtPubKey, taprootExtPubKey, minGapLimit ?? AbsoluteMinGapLimit, new BlockchainState(), filePath);
	}

	public static KeyManager CreateNewHardwareWalletWatchOnly(HDFingerprint masterFingerprint, ExtPubKey segwitExtPubKey, ExtPubKey? taprootExtPubKey, Network network, string? filePath = null)
	{
		return new KeyManager(null, null, masterFingerprint, segwitExtPubKey, taprootExtPubKey, AbsoluteMinGapLimit, new BlockchainState(network), filePath);
	}

	public static KeyManager Recover(Mnemonic mnemonic, string password, Network network, KeyPath swAccountKeyPath, KeyPath? trAccountKeyPath = null, string? filePath = null, int minGapLimit = AbsoluteMinGapLimit)
	{
		Guard.NotNull(nameof(mnemonic), mnemonic);
		password ??= "";

		ExtKey extKey = mnemonic.DeriveExtKey(password);
		var encryptedSecret = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network.Main);

		HDFingerprint masterFingerprint = extKey.Neuter().PubKey.GetHDFingerPrint();

		KeyPath segwitAccountKeyPath = swAccountKeyPath ?? GetAccountKeyPath(network, ScriptPubKeyType.Segwit);
		ExtPubKey segwitExtPubKey = extKey.Derive(segwitAccountKeyPath).Neuter();
		KeyPath taprootAccountKeyPath = trAccountKeyPath ?? GetAccountKeyPath(network, ScriptPubKeyType.TaprootBIP86);
		ExtPubKey taprootExtPubKey = extKey.Derive(taprootAccountKeyPath).Neuter();

		var km = new KeyManager(encryptedSecret, extKey.ChainCode, masterFingerprint, segwitExtPubKey, taprootExtPubKey, minGapLimit, new BlockchainState(network), filePath, segwitAccountKeyPath, taprootAccountKeyPath);
		km.AssertCleanKeysIndexed();
		return km;
	}

	public static KeyManager FromFile(string filePath, string? secret = null)
	{
		filePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath);

		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException($"Wallet file not found at: `{filePath}`.");
		}

		SafeIoManager safeIoManager = new(filePath);
		string jsonString = safeIoManager.ReadAllText(Encoding.UTF8);

		if (!string.IsNullOrWhiteSpace(secret))
		{
			// For the first time after 2FA was enabled the file won't be encrypted, so this will throw.
			jsonString = TwoFactorAuthenticationHelpers.DecryptString(jsonString, secret);
		}

		KeyManager km = JsonConvert.DeserializeObject<KeyManager>(jsonString, JsonConverters)
			?? throw new JsonSerializationException($"Wallet file at: `{filePath}` is not a valid wallet file or it is corrupted.");

		km.SetFilePath(filePath);
		km.EncryptionKey = secret;

		var attributeFilePath = GetWalletAttributeFilePath(filePath);
		if (attributeFilePath.Length > 0 && File.Exists(attributeFilePath))
		{
			SafeIoManager safeIoManagerAttributes = new(attributeFilePath);
			string jsonStringAttributes = safeIoManagerAttributes.ReadAllText(Encoding.UTF8);
			WalletAttributes? walletAttributes;
			try
			{
				if (!jsonStringAttributes.Contains('{'))
				{
					jsonStringAttributes = TwoFactorAuthenticationHelpers.DecryptString(jsonStringAttributes, km.SegwitExtPubKey.ToString(km.BlockchainState.Network));
				}

				walletAttributes = JsonConvert.DeserializeObject<WalletAttributes>(jsonStringAttributes, JsonConverters);
				if (walletAttributes is not null)
				{
					km.Attributes = walletAttributes;
				}
			}
			catch (Exception ex)
			{
				Logger.LogInfo($"Unable to load Wallet Attributes file at: `{attributeFilePath}` is not a valid wallet attributes file or it is corrupted. ({ex.Message})");
			}
		}

		return km;
	}

	public void SetFilePath(string? filePath)
	{
		FilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
		if (FilePath is null)
		{
			return;
		}

		IoHelpers.EnsureContainingDirectoryExists(FilePath);
	}

	internal HdPubKey GenerateNewKey(LabelsArray labels, KeyState keyState, bool isInternal, ScriptPubKeyType scriptPubKeyType = ScriptPubKeyType.Segwit)
	{
		var hdPubKeyRegistry = GetHdPubKeyGenerator(isInternal, scriptPubKeyType)
							   ?? throw new NotSupportedException($"Script type '{scriptPubKeyType}' is not supported.");

		lock (CriticalStateLock)
		{
			var view = HdPubKeyCache.GetView(hdPubKeyRegistry.KeyPath);
			var (keyPath, extPubKey) = hdPubKeyRegistry.GenerateNewKey(view);
			var hdPubKey = new HdPubKey(extPubKey.PubKey, keyPath, labels, keyState);
			HdPubKeyCache.AddKey(hdPubKey, scriptPubKeyType);
			return hdPubKey;
		}
	}

	public HdPubKey GetNextReceiveKey(LabelsArray labels, ScriptPubKeyType scriptPubKeyType = ScriptPubKeyType.Segwit)
	{
		lock (CriticalStateLock)
		{
			var newKey = scriptPubKeyType switch
			{
				ScriptPubKeyType.Segwit => GetNextReceiveSegwitKey(),
				ScriptPubKeyType.TaprootBIP86 => GetNextReceiveTaprootKey(),
				_ => throw new NotSupportedException($"Script type '{scriptPubKeyType}' is not supported.")
			};

			newKey.SetLabel(labels);

			ToFile();
			return newKey;
		}
	}

	private HdPubKey GetNextReceiveSegwitKey()
	{
		var (newKey, newlyGeneratedKeySet, newHdPubKeyGenerator) = GetNextReceiveKey(SegwitExternalKeyGenerator);
		SegwitExternalKeyGenerator = newHdPubKeyGenerator;
		HdPubKeyCache.AddRangeKeys(newlyGeneratedKeySet);
		return newKey;
	}

	private HdPubKey GetNextReceiveTaprootKey()
	{
		if (TaprootExternalKeyGenerator is not { } nonNullTaprootExternalKeyGenerator)
		{
			throw new NotSupportedException("Taproot is not supported in this wallet.");
		}
		var (newKey, newlyGeneratedKeySet, newHdPubKeyGenerator) = GetNextReceiveKey(nonNullTaprootExternalKeyGenerator);
		TaprootExternalKeyGenerator = newHdPubKeyGenerator;
		HdPubKeyCache.AddRangeKeys(newlyGeneratedKeySet);
		return newKey;
	}

	private (HdPubKey, HdPubKey[], HdPubKeyGenerator) GetNextReceiveKey(HdPubKeyGenerator hdPubKeyGenerator)
	{
		// Find the next clean external key with empty label.
		var externalView = HdPubKeyCache.GetView(hdPubKeyGenerator.KeyPath);
		if (externalView.CleanKeys.FirstOrDefault(x => x.Labels.IsEmpty) is { } cachedKey)
		{
			return (cachedKey, Array.Empty<HdPubKey>(), hdPubKeyGenerator);
		}

		var newHdPubKeyGenerator = hdPubKeyGenerator with { MinGapLimit = hdPubKeyGenerator.MinGapLimit + 1 };
		var newHdPubKeys = newHdPubKeyGenerator.AssertCleanKeysIndexed(externalView).Select(CreateHdPubKey).ToArray();

		var newKey = newHdPubKeys.First();
		return (newKey, newHdPubKeys, newHdPubKeyGenerator);
	}

	public HdPubKey GetNextChangeKey() =>
		GetKeys(x =>
			x.KeyState == KeyState.Clean &&
			x.IsInternal == true)
			.First();

	public IEnumerable<HdPubKey> GetNextCoinJoinKeys() =>
		GetKeys(x =>
				x.KeyState == KeyState.Locked &&
				x.IsInternal == true);

	public IEnumerable<HdPubKey> GetKeys(Func<HdPubKey, bool>? wherePredicate)
	{
		// BIP44-ish derivation scheme
		// m / purpose' / coin_type' / account' / change / address_index
		lock (CriticalStateLock)
		{
			AssertCleanKeysIndexed();
			var predicate = wherePredicate ?? (_ => true);
			return HdPubKeyCache.HdPubKeys.Where(predicate).OrderBy(x => x.Index);
		}
	}

	public IEnumerable<HdPubKey> GetKeys(KeyState? keyState = null, bool? isInternal = null) =>
		(keyState, isInternal) switch
		{
			(null, null) => GetKeys(x => true),
			(null, { } i) => GetKeys(x => x.IsInternal == i),
			({ } k, null) => GetKeys(x => x.KeyState == k),
			({ } k, { } i) => GetKeys(x => x.IsInternal == i && x.KeyState == k)
		};

	/// <summary>
	/// This function can only be called for wallet synchronization.
	/// It's unsafe because it doesn't assert that the GapLimit is respected.
	/// GapLimit should be enforced whenever a transaction is discovered.
	/// </summary>
	public record ScriptPubKeySpendingInfo(byte[] CompressedScriptPubKey, Height? LatestSpendingHeight);

	public IEnumerable<ScriptPubKeySpendingInfo> UnsafeGetSynchronizationInfos()
	{
		lock (CriticalStateLock)
		{
			return HdPubKeyCache.Select(x => new ScriptPubKeySpendingInfo(x.CompressedScriptPubKey, x.HdPubKey.LatestSpendingHeight));
		}
	}

	public bool TryGetKeyForScriptPubKey(Script scriptPubKey, [NotNullWhen(true)] out HdPubKey? hdPubKey)
	{
		lock (CriticalStateLock)
		{
			return HdPubKeyCache.TryGetPubKey(scriptPubKey, out hdPubKey);
		}
	}

	public IEnumerable<ExtKey> GetSecrets(string password, params Script[] scripts)
	{
		ExtKey extKey = GetMasterExtKey(password);
		var extKeysAndPubs = new List<ExtKey>();

		lock (CriticalStateLock)
		{
			foreach (HdPubKey key in GetKeys(x =>
				scripts.Contains(x.P2wpkhScript)
				|| scripts.Contains(x.P2Taproot)))
			{
				ExtKey ek = extKey.Derive(key.FullKeyPath);
				extKeysAndPubs.Add(ek);
			}
		}
		return extKeysAndPubs;
	}

	private (int PasswordHash, ExtKey MasterKey)? MasterKeyAndPasswordHash { get; set; }

	public ExtKey GetMasterExtKey(string password)
	{
		if (IsWatchOnly)
		{
			throw new SecurityException("This is a watch-only wallet.");
		}

		password ??= "";

		var passwordHash = password.GetHashCode();

		if (MasterKeyAndPasswordHash is { MasterKey: var masterKey, PasswordHash: var storedPasswordHash })
		{
			if (passwordHash != storedPasswordHash)
			{
				throw new SecurityException("Invalid passphrase.");
			}

			return masterKey;
		}

		try
		{
			Key secret = EncryptedSecret.GetKey(password);
			var extKey = new ExtKey(secret, ChainCode);

			// Backwards compatibility:
			MasterFingerprint ??= secret.PubKey.GetHDFingerPrint();
			DeriveTaprootExtPubKey(extKey);

			MasterKeyAndPasswordHash = (passwordHash, extKey);

			return extKey;
		}
		catch (SecurityException ex)
		{
			throw new SecurityException("Invalid passphrase.", ex);
		}
	}

	private void DeriveTaprootExtPubKey(ExtKey extKey)
	{
		if (TaprootExtPubKey is null)
		{
			TaprootAccountKeyPath = GetAccountKeyPath(GetNetwork(), ScriptPubKeyType.TaprootBIP86);
			TaprootExtPubKey = extKey.Derive(TaprootAccountKeyPath).Neuter();
		}
	}

	internal void AddOrUpdateKey(HdPubKey hdPubKey)
	{
		var info = new HdPubKeyInfo(hdPubKey, hdPubKey.FullKeyPath.GetScriptTypeFromKeyPath());
		if (HdPubKeyCache.TryGetPubKey(info.ScriptPubKey, out HdPubKey? origKey))
		{
			if (hdPubKey.KeyState == KeyState.Locked && origKey.KeyState == KeyState.Clean)
			{
				origKey.SetKeyState(KeyState.Locked);
			}
			if (hdPubKey.Labels.Count > 0 && origKey.Labels.Count == 0)
			{
				origKey.SetLabel(hdPubKey.Labels);
			}
		}
		else
		{
			// We have to be sure that all the lower index keys are there or the wallet will miss those values!
			var parentKeyPath = hdPubKey.FullKeyPath.Parent;
			if (parentKeyPath is not null)
			{
				var view = HdPubKeyCache.GetView(parentKeyPath);
				var idx = view.Select(x => x.Index).MaxOrDefault(-1) + 1;

				var keySource = GetHdPubKeyGenerator(hdPubKey.IsInternal, hdPubKey.FullKeyPath.GetScriptTypeFromKeyPath());
				if (keySource is not null)
				{
					HdPubKeyCache.AddRangeKeys(keySource.GenerateKeysByIndexRange(idx, hdPubKey.Index - idx).Select(CreateHdPubKey));
					HdPubKeyCache.AddKey(hdPubKey, info.ScriptPubKeyType);
				}
			}
		}
	}

	public void SetKeyState(KeyState newKeyState, HdPubKey hdPubKey)
	{
		if (hdPubKey.KeyState == newKeyState)
		{
			return;
		}

		hdPubKey.SetKeyState(newKeyState);
		if (newKeyState is KeyState.Locked or KeyState.Used)
		{
			var keySource = GetHdPubKeyGenerator(hdPubKey.IsInternal, hdPubKey.FullKeyPath.GetScriptTypeFromKeyPath());

			// This can happen after downgrading to pre-taproot wasabi version the switching back to a supporting
			// version so taproot keys are detected. However, the user has not login yet so taprootextpubkey is
			// not derived yet (because pre-taproot wasabi do not serialize fields that it doesn't know)
			if (keySource is { })
			{
				var view = HdPubKeyCache.GetView(keySource.KeyPath);
				HdPubKeyCache.AddRangeKeys(keySource.AssertCleanKeysIndexed(view).Select(CreateHdPubKey));
			}
		}
	}

	private HdPubKeyGenerator? GetHdPubKeyGenerator(bool isInternal, ScriptPubKeyType scriptPubKeyType) =>
		(isInternal, scriptPubKeyType) switch
		{
			(true, ScriptPubKeyType.Segwit) => SegwitInternalKeyGenerator,
			(false, ScriptPubKeyType.Segwit) => SegwitExternalKeyGenerator,
			(true, ScriptPubKeyType.TaprootBIP86) => TaprootInternalKeyGenerator,
			(false, ScriptPubKeyType.TaprootBIP86) => TaprootExternalKeyGenerator,
			_ => throw new NotSupportedException($"There is not available generator for '{scriptPubKeyType}.")
		};

	private IEnumerable<HdPubKey> AssertCleanKeysIndexed()
	{
		var keys = new[]
			{
				SegwitInternalKeyGenerator,
				SegwitExternalKeyGenerator,
				TaprootInternalKeyGenerator,
				TaprootExternalKeyGenerator
			}
			.Where(x => x is not null)
			.SelectMany(gen => gen!.AssertCleanKeysIndexed(HdPubKeyCache.GetView(gen.KeyPath)))
			.Select(CreateHdPubKey);

		return HdPubKeyCache.AddRangeKeys(keys);
	}

	/// <summary>
	/// Make sure there's always locked internal keys generated and indexed.
	/// </summary>
	public void AssertLockedInternalKeysIndexedAndPersist(int howMany, bool preferTaproot)
	{
		if (AssertLockedInternalKeysIndexed(howMany, preferTaproot))
		{
			ToFile();
		}
	}

	public bool AssertLockedInternalKeysIndexed(int howMany, bool preferTaproot)
	{
		var hdPubKeyGenerator = (TaprootInternalKeyGenerator, preferTaproot) switch
		{
			({ }, true) => TaprootInternalKeyGenerator,
			_ => SegwitInternalKeyGenerator
		};

		Guard.InRangeAndNotNull(nameof(howMany), howMany, 0, hdPubKeyGenerator.MinGapLimit);
		var internalView = HdPubKeyCache.GetView(hdPubKeyGenerator.KeyPath);
		var lockedKeyCount = internalView.LockedKeys.Count();
		var missingLockedKeys = Math.Max(howMany - lockedKeyCount, 0);

		HdPubKeyCache.AddRangeKeys(hdPubKeyGenerator.AssertCleanKeysIndexed(internalView).Select(CreateHdPubKey));

		var availableCandidates = HdPubKeyCache
			.GetView(hdPubKeyGenerator.KeyPath)
			.CleanKeys
			.Where(x => x.Labels.IsEmpty)
			.Take(missingLockedKeys)
			.ToList();

		foreach (var hdPubKeys in availableCandidates)
		{
			SetKeyState(KeyState.Locked, hdPubKeys);
		}

		return availableCandidates.Count > 0;
	}

	public void ToFile()
	{
		if (FilePath is { } filePath)
		{
			ToFile(filePath);
		}
	}

	public void ToFile(string filePath)
	{
		string jsonString = string.Empty;
		string jsonStringAttributes = string.Empty;

		lock (CriticalStateLock)
		{
			jsonString = JsonConvert.SerializeObject(this, Formatting.Indented, JsonConverters);
			jsonStringAttributes = JsonConvert.SerializeObject(Attributes, Formatting.Indented, JsonConverters);
		}

		IoHelpers.EnsureContainingDirectoryExists(filePath);

		SafeIoManager safeIoManager = new(filePath);
		if (!string.IsNullOrWhiteSpace(EncryptionKey))
		{
			jsonString = TwoFactorAuthenticationHelpers.EncryptString(jsonString, EncryptionKey);
		}
		safeIoManager.WriteAllText(jsonString, Encoding.UTF8);

		var attributeFilePath = GetWalletAttributeFilePath(filePath);
		if (attributeFilePath.Length > 0)
		{
			SafeIoManager safeIoManagerAttributes = new(attributeFilePath);
			if (!string.IsNullOrWhiteSpace(EncryptionKey))
			{
				jsonStringAttributes = TwoFactorAuthenticationHelpers.EncryptString(jsonStringAttributes, SegwitExtPubKey.ToString(BlockchainState.Network));
			}

			safeIoManagerAttributes.WriteAllText(jsonStringAttributes, Encoding.UTF8);
		}
	}

	protected static string GetWalletAttributeFilePath(string filePath)
	{
		if (filePath.EndsWith(WalletDirectories.WalletFileExtension))
		{
			return filePath[..(filePath.Length - WalletDirectories.WalletFileExtension.Length)] + WalletDirectories.WalletAttributesFileExtension;
		}
		return "";
	}

	#region BlockchainState

	public Height GetBestHeight(SyncType syncType)
	{
		lock (CriticalStateLock)
		{
			return syncType == SyncType.Turbo ? BlockchainState.TurboSyncHeight : BlockchainState.Height;
		}
	}

	public Network GetNetwork()
	{
		return BlockchainState.Network;
	}

	public void SetBestHeight(SyncType syncType, Height height, bool toFile = true)
	{
		if (syncType == SyncType.Turbo)
		{
			// Only keys in TurboSync subset (external + internal that didn't receive or fully spent coins) were tested, update TurboSyncHeight.
			SetBestTurboSyncHeight(height, toFile);
		}
		else
		{
			// All keys were tested at this height, update the Height.
			SetBestHeight(height, toFile);
		}
	}

	public void SetBestHeight(Height height, bool toFile = true)
	{
		lock (CriticalStateLock)
		{
			BlockchainState.Height = height;
			EnsureTurboSyncHeightConsistency(false);
			if (toFile)
			{
				ToFile();
			}
		}
	}

	public void SetBestTurboSyncHeight(Height height, bool toFile = true)
	{
		lock (CriticalStateLock)
		{
			BlockchainState.TurboSyncHeight = height;

			if (toFile)
			{
				ToFile();
			}
		}
	}

	public void SetBestHeights(Height height, Height turboSyncHeight)
	{
		lock (CriticalStateLock)
		{
			SetBestTurboSyncHeight(turboSyncHeight, false);
			SetBestHeight(height, false);
			ToFile();
		}
	}

	public void SetMaxBestHeight(Height newHeight)
	{
		lock (CriticalStateLock)
		{
			var prevHeight = BlockchainState.Height;
			var prevTurboSyncHeight = BlockchainState.TurboSyncHeight;
			if (newHeight < prevHeight)
			{
				SetBestHeights(newHeight, newHeight);
				Logger.LogWarning($"Wallet ({WalletName}) height has been set back by {prevHeight - (int)newHeight}. From {prevHeight} to {newHeight}.");
			}
			else if (newHeight < prevTurboSyncHeight)
			{
				SetBestTurboSyncHeight(newHeight);
				Logger.LogWarning($"Wallet ({WalletName}) turbo sync height has been set back by {prevTurboSyncHeight - (int)newHeight}. From {prevTurboSyncHeight} to {newHeight}.");
			}
		}
	}

	public void EnsureTurboSyncHeightConsistency(bool toFile = true)
	{
		lock (CriticalStateLock)
		{
			if (BlockchainState.TurboSyncHeight < BlockchainState.Height)
			{
				// TurboSyncHeight can't be behind BestHeight
				BlockchainState.TurboSyncHeight = BlockchainState.Height;
			}

			if (toFile)
			{
				ToFile();
			}
		}
	}

	public void SetIcon(string icon)
	{
		Icon = icon;
		ToFile();
	}

	public void SetIcon(WalletType type)
	{
		SetIcon(type.ToString());
	}

	public void SetFeeRateMedianTimeFrame(int hours)
	{
		if (hours != 0 && !Constants.CoinJoinFeeRateMedianTimeFrames.Contains(hours))
		{
			throw new ArgumentOutOfRangeException(nameof(hours), $"Hours can be only one of {string.Join(",", Constants.CoinJoinFeeRateMedianTimeFrames)}.");
		}

		FeeRateMedianTimeFrameHours = hours;
	}

	public void AssertNetworkOrClearBlockState(Network expectedNetwork)
	{
		lock (CriticalStateLock)
		{
			var lastNetwork = BlockchainState.Network;
			if (lastNetwork is null || lastNetwork != expectedNetwork)
			{
				BlockchainState.Network = expectedNetwork;
				SetBestHeights(0, 0);

				if (lastNetwork is { })
				{
					Logger.LogWarning($"Wallet is opened on {expectedNetwork}. Last time it was opened on {lastNetwork}.");
				}
				Logger.LogInfo("Blockchain cache is cleared.");
			}
		}
	}

	#endregion BlockchainState

	private static HdPubKey CreateHdPubKey((KeyPath KeyPath, ExtPubKey ExtPubKey) x) =>
		new(x.ExtPubKey.PubKey, x.KeyPath, LabelsArray.Empty, KeyState.Clean);

	internal void UpdateFromCoins(CoinsRegistry coins)
	{
		Attributes.ExcludedCoinsFromCoinJoin = coins.Where(c => c.IsExcludedFromCoinJoin).Select(c => c.Outpoint).ToList();
		Attributes.CoinJoinOutputs = coins.Where(c => c.IsCoinJoinOutput).Select(c => c.Outpoint).ToList();
		ToFile();
	}

	internal void AddCoinJoinTransaction(uint256 txHash)
	{
		if (!Attributes.CoinJoinTransactions.Contains(txHash))
		{
			Attributes.CoinJoinTransactions.Add(txHash);
			ToFile();
		}
	}
}

public static class KeyPathExtensions
{
	public static ScriptPubKeyType GetScriptTypeFromKeyPath(this KeyPath keyPath) =>
		keyPath.ToBytes().First() switch
		{
			84 => ScriptPubKeyType.Segwit,
			86 => ScriptPubKeyType.TaprootBIP86,
			_ => ScriptPubKeyType.Segwit // User can specify a specify whatever (like m/999'/999'/999')
										 // throw new NotSupportedException("Unknown script type.")
		};
}

public static class HdPubKeyExtensions
{
	public static BitcoinAddress GetAddress(this HdPubKey me, Network network) =>
		me.PubKey.GetAddress(me.FullKeyPath.GetScriptTypeFromKeyPath(), network);

	public static Script GetAssumedScriptPubKey(this HdPubKey me) =>
		me.PubKey.GetScriptPubKey(me.FullKeyPath.GetScriptTypeFromKeyPath());
}
