using System.Collections.Generic;
using System.Net.Http;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Lang;

namespace WalletWasabi.Fluent.Extensions;

public static class FriendlyExceptionMessageExtensions
{
	public static string ToUserFriendlyString(this Exception ex)
	{
		var exceptionMessage = Guard.Correct(ex.Message);

		if (exceptionMessage.Length == 0)
		{
			return Resources.UnexpextedError;
		}

		if (TryFindRpcErrorMessage(exceptionMessage, out var friendlyMessage))
		{
			return friendlyMessage;
		}

		return ex switch
		{
			HwiException hwiEx => GetFriendlyHwiExceptionMessage(hwiEx),
			HttpRequestException => Resources.SomethingWrong,
			UnauthorizedAccessException => Resources.PermissionError,
			_ => ex.Message
		};
	}

	private static string GetFriendlyHwiExceptionMessage(HwiException hwiEx)
	{
		return hwiEx.ErrorCode switch
		{
			HwiErrorCode.DeviceConnError => Resources.HardwareWalletNotFound,
			HwiErrorCode.ActionCanceled => Resources.TransactionCanceledOnDevice,
			HwiErrorCode.UnknownError => Resources.UnknownErrorDeviceCheck,
			_ => hwiEx.Message
		};
	}

	private static bool TryFindRpcErrorMessage(string exceptionMessage, out string friendlyMessage)
	{
		friendlyMessage = "";

		foreach (KeyValuePair<string, string> pair in RpcErrorTools.ErrorTranslations)
		{
			if (exceptionMessage.Contains(pair.Key, StringComparison.InvariantCultureIgnoreCase))
			{
				{
					friendlyMessage = pair.Value;
					return true;
				}
			}
		}

		return false;
	}
}
