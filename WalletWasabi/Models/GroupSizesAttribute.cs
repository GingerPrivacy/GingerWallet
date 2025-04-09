namespace WalletWasabi.Models;

[AttributeUsage(AttributeTargets.Field)]
public class GroupSizesAttribute : Attribute
{
	public GroupSizesAttribute(int[] sizes)
	{
		Sizes = sizes;
	}

	public int[] Sizes { get; }
}
