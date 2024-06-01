using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class ApiResponse
{
	private static ApiResponseInfo InfoOK = new(HttpStatusCode.OK, true, "", "");

	public ApiResponse(ApiResponseInfo? info)
	{
		Info = info ?? InfoOK;
	}

	public bool ShouldBan { get; set; } = true;
	public bool ShouldRemove { get; set; } = true;

	public ApiResponseInfo Info { get; }

	public virtual void Evaluate(int blockchainHeightOfCoin, CoinVerifierRiskConfig riskConfig)
	{
		ShouldBan = ShouldRemove = false;
	}

	public virtual string GetDetails()
	{
		return "";
	}

	// Json handlig helpers
	protected static readonly JsonSerializerSettings JsonSettings = new();

	protected static T CreateAndCopy<T>(object? src = null)
	{
		var dstType = typeof(T);
		var obj = (T)RuntimeHelpers.GetUninitializedObject(dstType);

		if (src != null)
		{
			var srcType = src.GetType();
			foreach (var srcProp in srcType.GetProperties())
			{
				var value = srcProp.GetValue(src);
				var dstProp = dstType.GetProperty(srcProp.Name);
				if (dstProp != null && value != null)
				{
					dstProp.SetValue(obj, value);
				}
			}
		}
		return obj;
	}
}
