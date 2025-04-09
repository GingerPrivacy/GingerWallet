namespace WalletWasabi.Models;

[AttributeUsage(AttributeTargets.Field)]
public class CharAttribute : Attribute
{
	public CharAttribute(string character)
	{
		Character = character;
	}

	public string Character { get; }
}
