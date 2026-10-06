using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.TrezorSuite;

internal sealed class TrezorSuiteCdpClient : IAsyncDisposable
{
	private readonly ClientWebSocket _webSocket = new();
	private int _messageId;

	private TrezorSuiteCdpClient()
	{
	}

	public static async Task<TrezorSuiteCdpClient> ConnectAsync(int port, TimeSpan timeout, CancellationToken cancellationToken)
	{
		using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutSource.CancelAfter(timeout);

		using var httpClient = WasabiHttpClientFactory.CreateLongLivedHttpClient();
		httpClient.BaseAddress = new Uri($"http://127.0.0.1:{port}/");
		httpClient.Timeout = TimeSpan.FromSeconds(2);

		Exception? lastException = null;
		while (!timeoutSource.IsCancellationRequested)
		{
			try
			{
				using var response = await httpClient.GetAsync("json/list", timeoutSource.Token).ConfigureAwait(false);
				response.EnsureSuccessStatusCode();
				await using var responseStream = await response.Content.ReadAsStreamAsync(timeoutSource.Token).ConfigureAwait(false);
				using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: timeoutSource.Token).ConfigureAwait(false);

				var target = document.RootElement
					.EnumerateArray()
					.Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "page")
					.Where(x => !x.TryGetProperty("url", out var url) || !IsDevToolsUrl(url.GetString()))
					.FirstOrDefault(x => x.TryGetProperty("webSocketDebuggerUrl", out _));

				if (target.ValueKind != JsonValueKind.Undefined &&
					target.TryGetProperty("webSocketDebuggerUrl", out var webSocketUrl) &&
					Uri.TryCreate(webSocketUrl.GetString(), UriKind.Absolute, out var uri))
				{
					var client = new TrezorSuiteCdpClient();
					await client._webSocket.ConnectAsync(uri, timeoutSource.Token).ConfigureAwait(false);
					return client;
				}
			}
			catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
			{
				lastException = ex;
			}

			try
			{
				await Task.Delay(250, timeoutSource.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				break;
			}
		}

		cancellationToken.ThrowIfCancellationRequested();

		throw new TrezorSuiteIntegrationException(
			"Trezor Suite did not expose its local configuration target. Close every running Trezor Suite window and try again.",
			lastException ?? new TimeoutException());
	}

	public async Task<TrezorSuiteStoredSettings> ReadDebugSettingsAsync(CancellationToken cancellationToken)
	{
		var value = await EvaluateAsync(BuildReadSettingsScript(), cancellationToken).ConfigureAwait(false);
		return new TrezorSuiteStoredSettings(
			value.GetProperty("exists").GetBoolean(),
			value.GetProperty("json").ValueKind == JsonValueKind.Null ? null : value.GetProperty("json").GetString());
	}

	public async Task ApplyGingerOverrideAsync(CancellationToken cancellationToken)
	{
		var value = await EvaluateAsync(BuildApplyOverrideScript(), cancellationToken).ConfigureAwait(false);
		if (!value.GetProperty("configured").GetBoolean())
		{
			throw new TrezorSuiteIntegrationException("Trezor Suite did not retain the Ginger CoinJoin configuration override.");
		}
	}

	public async Task RestoreDebugSettingsAsync(bool originalSettingsExisted, string? originalSettingsJson, CancellationToken cancellationToken)
	{
		var value = await EvaluateAsync(BuildRestoreSettingsScript(originalSettingsExisted, originalSettingsJson), cancellationToken).ConfigureAwait(false);
		if (!value.GetProperty("restored").GetBoolean())
		{
			throw new TrezorSuiteIntegrationException("Trezor Suite did not restore its original CoinJoin debug settings.");
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
		{
			using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
			try
			{
				await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Configuration complete", timeoutSource.Token).ConfigureAwait(false);
			}
			catch (Exception) when (timeoutSource.IsCancellationRequested)
			{
			}
		}

		_webSocket.Dispose();
	}

	internal static string BuildReadSettingsScript() => $$"""
		(async () => {
			{{DatabaseHelpers}}
			const db = await openSuiteDatabase();
			const value = await readDebugSettings(db);
			db.close();
			return { exists: value !== undefined, json: value === undefined ? null : JSON.stringify(value) };
		})()
		""";

	internal static string BuildApplyOverrideScript() => $$"""
		(async () => {
			{{DatabaseHelpers}}
			const db = await openSuiteDatabase();
			const current = (await readDebugSettings(db)) ?? {};
			const overrides = current.coinjoinConfigOverride ?? {};
			const btc = overrides.btc ?? {};
			const updated = {
				...current,
				coinjoinConfigOverride: {
					...overrides,
					btc: {
						...btc,
						coordinatorUrl: 'https://api.gingerwallet.io/WabiSabi/',
						wabisabiBackendUrl: 'https://api.gingerwallet.io/',
						affiliationId: null
					}
				}
			};
			await writeDebugSettings(db, updated);
			const saved = await readDebugSettings(db);
			db.close();
			const savedBtc = saved?.coinjoinConfigOverride?.btc;
			return {
				configured:
					savedBtc?.coordinatorUrl === 'https://api.gingerwallet.io/WabiSabi/' &&
					savedBtc?.wabisabiBackendUrl === 'https://api.gingerwallet.io/' &&
					savedBtc?.affiliationId === null
			};
		})()
		""";

	internal static string BuildRestoreSettingsScript(bool originalSettingsExisted, string? originalSettingsJson)
	{
		var existed = originalSettingsExisted ? "true" : "false";
		var serializedJson = JsonSerializer.Serialize(originalSettingsJson);

		return $$"""
			(async () => {
				{{DatabaseHelpers}}
				const db = await openSuiteDatabase();
				const originallyExisted = {{existed}};
				if (originallyExisted) {
					const original = JSON.parse({{serializedJson}});
					await writeDebugSettings(db, original);
				} else {
					await deleteDebugSettings(db);
				}
				const restoredValue = await readDebugSettings(db);
				db.close();
				return {
					restored: originallyExisted
						? JSON.stringify(restoredValue) === {{serializedJson}}
						: restoredValue === undefined
				};
			})()
			""";
	}

	private static bool IsDevToolsUrl(string? url) =>
		url?.StartsWith("devtools://", StringComparison.OrdinalIgnoreCase) is true ||
		url?.StartsWith("chrome-devtools://", StringComparison.OrdinalIgnoreCase) is true;

	internal async Task<JsonElement> EvaluateAsync(string expression, CancellationToken cancellationToken)
	{
		var messageId = Interlocked.Increment(ref _messageId);
		var payload = JsonSerializer.Serialize(new
		{
			id = messageId,
			method = "Runtime.evaluate",
			@params = new
			{
				expression,
				awaitPromise = true,
				returnByValue = true
			}
		});

		await _webSocket.SendAsync(
			Encoding.UTF8.GetBytes(payload),
			WebSocketMessageType.Text,
			endOfMessage: true,
			cancellationToken).ConfigureAwait(false);

		while (true)
		{
			using var document = await ReceiveDocumentAsync(cancellationToken).ConfigureAwait(false);
			if (!document.RootElement.TryGetProperty("id", out var id) || id.GetInt32() != messageId)
			{
				continue;
			}

			if (document.RootElement.TryGetProperty("error", out var protocolError))
			{
				throw new TrezorSuiteIntegrationException($"Trezor Suite configuration protocol failed: {protocolError}");
			}

			var result = document.RootElement.GetProperty("result");
			if (result.TryGetProperty("exceptionDetails", out var exceptionDetails))
			{
				throw new TrezorSuiteIntegrationException(
					$"Trezor Suite rejected the configuration update: {GetExceptionDescription(exceptionDetails)}");
			}

			return result.GetProperty("result").GetProperty("value").Clone();
		}
	}

	private static string GetExceptionDescription(JsonElement exceptionDetails)
	{
		if (exceptionDetails.TryGetProperty("exception", out var exception) &&
			exception.TryGetProperty("description", out var description) &&
			!string.IsNullOrWhiteSpace(description.GetString()))
		{
			return description.GetString()!;
		}

		if (exceptionDetails.TryGetProperty("text", out var text) &&
			!string.IsNullOrWhiteSpace(text.GetString()))
		{
			return text.GetString()!;
		}

		return "Unknown browser error.";
	}

	private async Task<JsonDocument> ReceiveDocumentAsync(CancellationToken cancellationToken)
	{
		var buffer = ArrayPool<byte>.Shared.Rent(8192);
		try
		{
			using var stream = new MemoryStream();
			while (true)
			{
				var result = await _webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
				if (result.MessageType == WebSocketMessageType.Close)
				{
					throw new TrezorSuiteIntegrationException("Trezor Suite closed its configuration channel unexpectedly.");
				}

				stream.Write(buffer, 0, result.Count);
				if (result.EndOfMessage)
				{
					stream.Position = 0;
					return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
				}
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	internal const string DatabaseHelpers = """
		const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));
		const openSuiteDatabaseOnce = () => new Promise((resolve, reject) => {
			const request = indexedDB.open('trezor-suite');
			request.onsuccess = () => {
				const db = request.result;
				if (!db.objectStoreNames.contains('coinjoinDebugSettings')) {
					db.close();
					reject(new Error('Trezor Suite storage is not initialized yet.'));
					return;
				}
				resolve(db);
			};
			request.onerror = () => reject(request.error);
			request.onblocked = () => reject(new Error('Trezor Suite storage is busy.'));
		});
		const openSuiteDatabase = async () => {
			for (let attempt = 0; attempt < 40; attempt++) {
				const databases = await indexedDB.databases();
				if (databases.some(database => database.name === 'trezor-suite')) {
					try {
						return await openSuiteDatabaseOnce();
					}
					catch (error) {
						if (attempt === 39) {
							throw error;
						}
					}
				}

				await delay(250);
			}

			throw new Error('Trezor Suite storage is not initialized yet.');
		};
		const readDebugSettings = db => new Promise((resolve, reject) => {
			const request = db.transaction('coinjoinDebugSettings', 'readonly')
				.objectStore('coinjoinDebugSettings').get('debug');
			request.onsuccess = () => resolve(request.result);
			request.onerror = () => reject(request.error);
		});
		const writeDebugSettings = (db, value) => new Promise((resolve, reject) => {
			const transaction = db.transaction('coinjoinDebugSettings', 'readwrite');
			transaction.objectStore('coinjoinDebugSettings').put(value, 'debug');
			transaction.oncomplete = () => resolve();
			transaction.onerror = () => reject(transaction.error);
			transaction.onabort = () => reject(transaction.error);
		});
		const deleteDebugSettings = db => new Promise((resolve, reject) => {
			const transaction = db.transaction('coinjoinDebugSettings', 'readwrite');
			transaction.objectStore('coinjoinDebugSettings').delete('debug');
			transaction.oncomplete = () => resolve();
			transaction.onerror = () => reject(transaction.error);
			transaction.onabort = () => reject(transaction.error);
		});
		""";
}
