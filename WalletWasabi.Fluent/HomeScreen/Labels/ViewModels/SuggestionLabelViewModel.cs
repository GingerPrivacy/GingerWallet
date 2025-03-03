using WalletWasabi.Fluent.Common.ViewModels;

namespace WalletWasabi.Fluent.HomeScreen.Labels.ViewModels;

public class SuggestionLabelViewModel : ViewModelBase
{
	public SuggestionLabelViewModel(string label, int score)
	{
		Label = label;
		Score = score;
	}

	public string Label { get; }

	public int Score { get; }
}
