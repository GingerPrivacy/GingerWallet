namespace WalletWasabi.Fluent.Common.ViewModels.DialogBase;

public readonly struct DialogResult<TResult>
{
	public DialogResult(TResult? result, DialogResultKind kind)
	{
		Result = result;
		Kind = kind;
	}

	public TResult? Result { get; }

	public DialogResultKind Kind { get; }
}
