namespace WalletWasabi.Fluent.Models.BuySell;

public record CountryModel(string Name, string Code, StateModel[]? States = null);
