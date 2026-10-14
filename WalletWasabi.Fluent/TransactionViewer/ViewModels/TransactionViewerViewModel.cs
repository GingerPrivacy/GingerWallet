using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.TransactionViewer.ViewModels;

public partial class TransactionViewerViewModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _subscriptions = new();
	private TransactionViewerNode? _selectedNode;
	[AutoNotify] private bool _privacyMode;

	public TransactionViewerViewModel(TransactionCoinsModel model, IObservable<decimal> exchangeRate, IObservable<bool> privacyMode)
	{
		TransactionId = model.Transaction.GetHash().ToString();
		Date = model.Transaction.FirstSeen.LocalDateTime.ToString("g", Resources.Culture);
		Status = model.IsPreview ? Resources.PreviewTransaction : model.Transaction.Confirmed ? Resources.Confirmed : Resources.Pending;
		VirtualSize = $"{model.VirtualSize} vB";
		FeeText = model.Fee is { } fee ? $"{fee.Satoshi.ToString("N0", Resources.Culture)} sat" : Resources.Unknown;
		FeeRate = model.Fee is { } knownFee && model.VirtualSize > 0
			? $"{(knownFee.Satoshi / (decimal)model.VirtualSize).ToString("0.##", Resources.Culture)} sat/vB" : Resources.Unknown;
		Inputs = model.Inputs.Select((row, index) => new TransactionViewerNode(row, index, true, exchangeRate, Select)).ToArray();
		Outputs = model.Outputs.Select((row, index) => new TransactionViewerNode(row, index, false, exchangeRate, Select)).ToArray();
		Rows = Enumerable.Range(0, Math.Max(Inputs.Count, Outputs.Count))
			.Select(i => new TransactionViewerRow(Inputs.ElementAtOrDefault(i), Outputs.ElementAtOrDefault(i))).ToArray();
		InputTotal = FormatTotal(model.Inputs);
		OutputTotal = FormatTotal(model.Outputs);
		InputFiat = FormatFiat(Total(model.Inputs), exchangeRate);
		OutputFiat = FormatFiat(Total(model.Outputs), exchangeRate);
		FeeFiat = FormatFiat(model.Fee, exchangeRate);
		privacyMode.Subscribe(value => PrivacyMode = value).DisposeWith(_subscriptions);
		SelectedNode = Outputs.FirstOrDefault(n => n.Data.IsOwn) ?? Outputs.FirstOrDefault() ?? Inputs.FirstOrDefault();
	}

	public string TransactionId { get; }
	public string ShortTransactionId => Shorten(TransactionId);
	public string Date { get; }
	public string Status { get; }
	public string VirtualSize { get; }
	public string FeeText { get; }
	public string FeeRate { get; }
	public string InputTotal { get; }
	public string OutputTotal { get; }
	public IObservable<string> InputFiat { get; }
	public IObservable<string> OutputFiat { get; }
	public IObservable<string> FeeFiat { get; }
	public IReadOnlyList<TransactionViewerNode> Inputs { get; }
	public IReadOnlyList<TransactionViewerNode> Outputs { get; }
	public IReadOnlyList<TransactionViewerRow> Rows { get; }
	public string SelectionTitle => SelectedNode?.IsInput is true ? Resources.TransactionViewerSelectedInput : Resources.TransactionViewerSelectedOutput;

	public TransactionViewerNode? SelectedNode
	{
		get => _selectedNode;
		set
		{
			if (_selectedNode == value)
			{
				return;
			}
			if (_selectedNode is { } previous)
			{
				previous.IsSelected = false;
			}
			this.RaiseAndSetIfChanged(ref _selectedNode, value);
			if (value is { })
			{
				value.IsSelected = true;
			}
			this.RaisePropertyChanged(nameof(SelectionTitle));
		}
	}

	private void Select(TransactionViewerNode node) => SelectedNode = node;

	internal static Money? Total(IReadOnlyList<TransactionCoinRow> rows) => rows.All(r => r.Amount is not null) ? rows.Sum(r => r.Amount) : null;
	private static string FormatTotal(IReadOnlyList<TransactionCoinRow> rows) => Total(rows)?.ToFormattedString(addTicker: true) ?? Resources.Unknown;
	internal static string Shorten(string value) => value.Length > 24 ? $"{value[..12]}…{value[^8..]}" : value;

	internal static IObservable<string> FormatFiat(Money? amount, IObservable<decimal> exchangeRate) => exchangeRate
		.Select(rate => amount is { } money && rate > 0
			? $"≈ {(money.ToDecimal(MoneyUnit.BTC) * rate).ToString("N2", Resources.Culture)} {Resources.Culture.GetFiatTicker()}" : "");

	public void Dispose()
	{
		_subscriptions.Dispose();
		foreach (var node in Inputs.Concat(Outputs))
		{
			node.SelectCommand.Dispose();
		}
	}
}

public record TransactionViewerRow(TransactionViewerNode? Input, TransactionViewerNode? Output);
