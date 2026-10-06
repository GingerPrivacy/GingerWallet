// Run with: node --test WalletWasabi.Tests/UnitTests/TrezorSuite/privacy-scripts.test.cjs
// Executes the actual C#-embedded JavaScript against a transactional in-memory IndexedDB double.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = name => fs.readFileSync(path.join(__dirname, '../../..', 'WalletWasabi/TrezorSuite', name), 'utf8');
const template = source('TrezorSuitePrivacyScripts.cs').match(/=> \$\$"""\r?\n([\s\S]*?)\r?\n\s*""";/)[1];
const helpers = source('TrezorSuiteCdpClient.cs').match(/const string DatabaseHelpers = """\r?\n([\s\S]*?)\r?\n\s*""";/)[1];

function database(initial, corruptWrite = false) {
    let data = structuredClone(initial);
    const db = {
        objectStoreNames: { contains: name => ['coinjoinAccounts', 'coinjoinDebugSettings'].includes(name) },
        close() {},
        transaction() {
            const working = structuredClone(data);
            let pending = 0, aborted = false, finished = false;
            const tx = {
                abort() { aborted = true; setImmediate(() => tx.onabort()); },
                objectStore() { return store; },
            };
            function schedule(action) {
                pending++;
                setImmediate(() => {
                    if (aborted || finished) return;
                    action();
                    if (--pending === 0 && !aborted) {
                        finished = true;
                        data = working;
                        tx.oncomplete();
                    }
                });
            }
            const store = {
                get(key) {
                    const request = {};
                    schedule(() => { request.result = structuredClone(working[key]); request.onsuccess(); });
                    return request;
                },
                openCursor() {
                    const keys = Object.keys(working);
                    let index = 0;
                    const request = {};
                    function next() {
                        schedule(() => {
                            const key = keys[index++];
                            request.result = key === undefined ? null : {
                                key, value: structuredClone(working[key]),
                                continue: next,
                                update(value) { schedule(() => { if (!corruptWrite) working[key] = structuredClone(value); }); },
                            };
                            request.onsuccess();
                        });
                    }
                    next();
                    return request;
                },
            };
            return tx;
        },
    };
    return {
        data: () => data,
        indexedDB: {
            databases: async () => [{ name: 'trezor-suite' }],
            open() { const request = { result: db }; setImmediate(() => request.onsuccess()); return request; },
        },
    };
}

const account = (target = 8, symbol = 'btc') => ({ symbol, setup: { targetAnonymity: target, maxFeePerVbyte: 10, skipRounds: true }, unrelated: 'preserve' });
async function run(db, target = null, originals = null) {
    const script = template.replace('{{TrezorSuiteCdpClient.DatabaseHelpers}}', helpers)
        .replace('{{target}}', JSON.stringify(target)).replace('{{originals}}', JSON.stringify(originals));
    return JSON.parse(JSON.stringify(await vm.runInNewContext(script, { indexedDB: db.indexedDB, setTimeout })));
}

test('read, apply and restore preserve fees, skipping, unrelated data and other networks', async () => {
    const original = { first: account(), second: account(5), testnet: account(7, 'test') };
    const db = database(original);
    const backup = await run(db);
    assert.deepEqual(backup, { targets: { first: 8, second: 5 }, count: 2 });
    assert.equal((await run(db, 3, backup.targets)).count, 2);
    assert.deepEqual(db.data().first, { ...original.first, setup: { ...original.first.setup, targetAnonymity: 3 } });
    assert.deepEqual(db.data().testnet, original.testnet);
    await run(db, 2, backup.targets);
    await run(db, null, backup.targets);
    assert.deepEqual(db.data(), original);
});

test('recommended or malformed settings abort all account writes', async () => {
    for (const bad of [{ symbol: 'btc' }, { symbol: 'btc', setup: { targetAnonymity: 5 } }]) {
        const initial = { first: account(), second: bad };
        const db = database(initial);
        await assert.rejects(run(db, 3, { first: 8, second: 5 }), /Custom/);
        assert.deepEqual(db.data(), initial);
    }
});

test('new account without a backup aborts the transaction', async () => {
    const initial = { first: account(), newAccount: account() };
    const db = database(initial);
    await assert.rejects(run(db, 3, { first: 8 }), /accounts changed/);
    assert.deepEqual(db.data(), initial);
});

test('no saved accounts reports zero, restore does not recreate deleted accounts', async () => {
    const db = database({});
    assert.deepEqual(await run(db), { targets: {}, count: 0 });
    assert.equal((await run(db, 3, {})).count, 0);
    await run(db, null, { removed: 5 });
    assert.deepEqual(db.data(), {});
});

test('read-back detects a write that did not stick', async () => {
    const db = database({ first: account() }, true);
    await assert.rejects(run(db, 3, { first: 8 }), /did not retain/);
    assert.equal(db.data().first.setup.targetAnonymity, 8);
});
