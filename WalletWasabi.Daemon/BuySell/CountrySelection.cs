namespace WalletWasabi.Daemon.BuySell;

public record CountrySelection(string CountryName, string CountryCode, string? StateName = null, string? StateCode = null);
