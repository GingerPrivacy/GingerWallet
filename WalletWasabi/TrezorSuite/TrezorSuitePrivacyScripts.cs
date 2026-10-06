using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WalletWasabi.TrezorSuite;

internal static class TrezorSuitePrivacyScripts
{
	public static void ValidateTarget(int target)
	{
		if (target is < 2 or > 100)
		{
			throw new ArgumentOutOfRangeException(nameof(target), "Suite privacy target must be a whole number from 2 to 100.");
		}
	}

	// Only account keys and previous targets leave Suite, never wallet history or keys.
	public static string Read() => Build("null", "null");

	public static string Apply(int target, Dictionary<string, int> originals)
	{
		ValidateTarget(target);
		return Build(JsonSerializer.Serialize(target), JsonSerializer.Serialize(originals));
	}

	public static string Restore(Dictionary<string, int> originals) => Build("null", JsonSerializer.Serialize(originals));

	private static string Build(string target, string originals) => $$"""
		(async () => {
			{{TrezorSuiteCdpClient.DatabaseHelpers}}
			const target = {{target}};
			const originals = {{originals}};
			const db = await openSuiteDatabase();
			try {
				if (!db.objectStoreNames.contains('coinjoinAccounts')) {
					throw new Error('This Suite version has no supported CoinJoin account storage.');
				}
				return await new Promise((resolve, reject) => {
					const tx = db.transaction('coinjoinAccounts', originals === null ? 'readonly' : 'readwrite');
					const store = tx.objectStore('coinjoinAccounts');
					const request = store.openCursor();
					const targets = Object.create(null);
					let failure;
					let count = 0;
					const fail = message => { failure = new Error(message); tx.abort(); };
					tx.oncomplete = () => resolve({ targets, count });
					tx.onerror = () => reject(failure ?? tx.error);
					tx.onabort = () => reject(failure ?? tx.error);
					request.onsuccess = () => {
						const cursor = request.result;
						if (!cursor) return;
						const account = cursor.value;
						const restoring = originals !== null && target === null;
						if (account.symbol !== 'btc' || (restoring && !Object.hasOwn(originals, cursor.key))) {
							cursor.continue();
							return;
						}
						const setup = account.setup;
						if (typeof cursor.key !== 'string' || !setup ||
							!Number.isInteger(setup.targetAnonymity) ||
							!Number.isFinite(setup.maxFeePerVbyte) || typeof setup.skipRounds !== 'boolean') {
							fail('Open each remembered Bitcoin CoinJoin account in Suite, select Custom settings, then close Suite and configure again. Ginger will not guess or replace your mining fee settings.');
							return;
						}
						targets[cursor.key] = setup.targetAnonymity;
						count++;
						if (originals !== null) {
							if (!Object.hasOwn(originals, cursor.key)) {
								fail('Suite accounts changed during configuration. Close Suite and try again.');
								return;
							}
							const expected = restoring ? originals[cursor.key] : target;
							cursor.update({ ...account, setup: { ...setup, targetAnonymity: expected } });
							const verify = store.get(cursor.key);
							verify.onsuccess = () => {
								if (verify.result?.setup?.targetAnonymity !== expected) {
									fail('Suite did not retain the privacy target.');
								} else {
									cursor.continue();
								}
							};
						} else {
							cursor.continue();
						}
					};
				});
			} finally {
				db.close();
			}
		})()
		""";
}
