namespace WalletWasabi.WabiSabi.Backend.Banning;

public record CoinVerifierConfig(string Name, string ApiUrl, string ApiKey, string ApiSecret, string RiskSettings);
