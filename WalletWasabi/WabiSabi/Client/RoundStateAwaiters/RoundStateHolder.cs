using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;

public class RoundStateHolder
{
	public RoundStateHolder(RoundState roundState)
	{
		RoundState = roundState;
		Confidence = 1;
	}

	public RoundState RoundState { get; internal set; }

	public int Confidence { get; internal set; }

	private Exception? _exception = null;

	public Exception? Exception
	{
		get => _exception;
		set
		{
			if (_exception is null && value is not null)
			{
				_exception = value;
				Confidence = -1;
			}
		}
	}
}
