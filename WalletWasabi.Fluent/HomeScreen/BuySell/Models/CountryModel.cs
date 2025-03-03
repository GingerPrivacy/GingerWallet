namespace WalletWasabi.Fluent.HomeScreen.BuySell.Models;

public record CountryModel(string Name, string Code, StateModel[]? States = null);
