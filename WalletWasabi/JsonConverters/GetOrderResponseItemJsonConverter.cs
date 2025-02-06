using Newtonsoft.Json;
using System;
using WalletWasabi.BuySell;
using static WalletWasabi.BuySell.BuySellClientModels;

namespace WalletWasabi.JsonConverters
{
	public class GetOrderResponseItemJsonConverter : JsonConverter<GetOrderResponseItem>
	{
		public override GetOrderResponseItem? ReadJson(JsonReader reader, Type objectType, GetOrderResponseItem? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.String)
			{
				// Read the string as raw JSON text
				string jsonText = (string)reader.Value!;
				return JsonConvert.DeserializeObject<GetOrderResponseItem>(jsonText);
			}

			throw new JsonSerializationException("Expected JSON text for GetOrderResponseItem.");
		}

		public override void WriteJson(JsonWriter writer, GetOrderResponseItem? value, JsonSerializer serializer)
		{
			if (value is null)
			{
				writer.WriteNull();
				return;
			}

			// Convert object to JSON text and store as a string
			string jsonText = JsonConvert.SerializeObject(value);
			writer.WriteValue(jsonText);
		}
	}
}
