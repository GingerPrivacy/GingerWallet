using System.Reactive;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.TransactionViewer.ViewModels;

public partial class TransactionViewerNode : ReactiveObject
{
	[AutoNotify] private bool _isSelected;

	public TransactionViewerNode(TransactionCoinRow data, int index, bool isInput, IObservable<decimal> exchangeRate, Action<TransactionViewerNode> select)
	{
		Data = data;
		Index = index;
		IsInput = isInput;
		Fiat = TransactionViewerViewModel.FormatFiat(data.Amount, exchangeRate);
		SelectCommand = ReactiveCommand.Create(() => select(this));
	}

	public TransactionCoinRow Data { get; }
	public int Index { get; }
	public bool IsInput { get; }
	public bool IsOwn => Data.IsOwn;
	public string Number => $"#{Index}";
	public string Title => Data.Labels.Count > 0 ? Data.Labels.ToString() : Data.Ownership;
	public string ShortAddress => TransactionViewerViewModel.Shorten(Data.Address);
	public string Amount => Data.AmountText;
	public string AnonymityScoreText => Data.AnonymityScore?.ToString(Resources.Culture) ?? Resources.Unknown;
	public IObservable<string> Fiat { get; }
	public ReactiveCommand<Unit, Unit> SelectCommand { get; }
	public string ScriptType => Data.ScriptPubKey is not { } script ? Resources.Unknown
		: script.IsScriptType(NBitcoin.ScriptType.P2WPKH) ? "P2WPKH"
		: script.IsScriptType(NBitcoin.ScriptType.Taproot) ? "P2TR"
		: script.IsScriptType(NBitcoin.ScriptType.P2PKH) ? "P2PKH"
		: script.IsScriptType(NBitcoin.ScriptType.P2SH) ? "P2SH" : "Script";
}
