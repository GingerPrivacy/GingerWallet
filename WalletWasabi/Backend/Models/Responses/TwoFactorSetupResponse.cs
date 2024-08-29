using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WalletWasabi.Backend.Models.Responses
{
	public class TwoFactorSetupResponse
	{
		[JsonProperty("qrCodeUri")]
		public string QrCodeUri { get; set; }

		[JsonProperty("client_id")]
		public string ClientId { get; set; }

		[JsonProperty("secret_server")]
		public string SecretServer { get; set; }
	}
}
