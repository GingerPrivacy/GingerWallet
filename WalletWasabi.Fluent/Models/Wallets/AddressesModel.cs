using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public class AddressesModel : IDisposable
{
	private readonly CompositeDisposable _disposable = new();

	private readonly ISubject<HdPubKey> _newAddressGenerated = new Subject<HdPubKey>();
	private readonly Wallet _wallet;
	private readonly SourceList<HdPubKey> _source;

	public AddressesModel(Wallet wallet)
	{
		_wallet = wallet;
		_source = new SourceList<HdPubKey>();
		_source.AddRange(GetUnusedKeys());

		Observable.FromEventPattern<ProcessedResult>(
				h => wallet.WalletRelevantTransactionProcessed += h,
				h => wallet.WalletRelevantTransactionProcessed -= h)
			.Do(_ => UpdateUnusedKeys())
			.Subscribe()
			.DisposeWith(_disposable);

		_newAddressGenerated
			.Do(address => _source.Add(address))
			.Subscribe();

		_source
			.Connect()
			.Transform(key => new AddressModel(_wallet.KeyManager, key, Hide))
			.Bind(out var unusedAddresses)
			.Subscribe()
			.DisposeWith(_disposable);

		Unused = unusedAddresses;
	}

	private IEnumerable<HdPubKey> GetUnusedKeys() => _wallet.KeyManager.GetKeys(x => x is { IsInternal: false, KeyState: KeyState.Clean, Labels.Count: > 0 });

	public AddressModel NextReceiveAddress(IEnumerable<string> destinationLabels, ScriptPubKeyType type)
	{
		var pubKey = _wallet.GetNextReceiveAddress(destinationLabels, type);
		var nextReceiveAddress = new AddressModel(_wallet.KeyManager, pubKey, Hide);
		_newAddressGenerated.OnNext(pubKey);

		return nextReceiveAddress;
	}

	public ReadOnlyObservableCollection<AddressModel> Unused { get; }

	public void Hide(AddressModel addressModel)
	{
		_wallet.KeyManager.SetKeyState(KeyState.Locked, addressModel.HdPubKey);
		_wallet.KeyManager.ToFile();
		_source.Remove(addressModel.HdPubKey);
	}

	private void UpdateUnusedKeys()
	{
		var itemsToRemove = _source.Items
			.Where(item => item.KeyState != KeyState.Clean)
			.ToList();

		foreach (var item in itemsToRemove)
		{
			_source.Remove(item);
		}
	}

	public bool TryGetHdPubKey(string address, [NotNullWhen(true)] out HdPubKey? hdPubKey)
	{
		hdPubKey = _wallet.KeyManager
			.GetKeys(x => x is { IsInternal: false })
			.FirstOrDefault(x => x.GetAddress(_wallet.Network).ToString() == address);

		return hdPubKey is not null;
	}

	public void Dispose()
	{
		_disposable.Dispose();
		_source.Dispose();
	}
}
