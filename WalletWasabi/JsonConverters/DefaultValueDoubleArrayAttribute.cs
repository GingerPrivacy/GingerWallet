using System.ComponentModel;

namespace WalletWasabi.JsonConverters;

public class DefaultValueDoubleArrayAttribute : DefaultValueAttribute
{
	public DefaultValueDoubleArrayAttribute(string json) : base(DoubleArrayJsonConverter.Parse(json))
	{
	}
}
