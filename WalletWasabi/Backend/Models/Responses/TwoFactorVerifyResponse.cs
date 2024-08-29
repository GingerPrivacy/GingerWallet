using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Backend.Models.Responses;

public class TwoFactorVerifyResponse
{
	[JsonProperty("secret_wallet")]
	public string SecretWallet { get; set; }

	public string ClientId { get; set; }
	public string ServerSecret { get; set; }
}
