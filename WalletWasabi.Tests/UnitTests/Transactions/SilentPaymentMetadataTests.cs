using System.IO;
using Microsoft.Data.Sqlite;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Tests.TestCommon;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

public class SilentPaymentMetadataTests
{
	[Fact]
	public void SameTransactionMergesPreserveProtectionInBothDirections()
	{
		var tx = CreateTransaction();
		var ordinary = new SmartTransaction(tx, Height.Mempool);
		var silent = new SmartTransaction(tx, Height.Mempool, isSilentPayment: true);
		Assert.True(ordinary.TryUpdate(silent));
		Assert.True(ordinary.IsSilentPayment);
		silent.TryUpdate(new SmartTransaction(tx, Height.Mempool));
		Assert.True(silent.IsSilentPayment);
	}

	[Fact]
	public void LegacyAndExtendedLinesLoadWithoutChangingOldMetadata()
	{
		var tx = CreateTransaction();
		var legacy = $"{tx.GetHash()}:{tx.ToHex()}:Mempool::0:recipient:1570464578:False:False:False";
		Assert.False(SmartTransaction.FromLine(legacy, Network.Main).IsSilentPayment);
		var loaded = SmartTransaction.FromLine(legacy + ":True", Network.Main);
		Assert.True(loaded.IsSilentPayment);
		Assert.Equal("recipient", loaded.Labels.ToString());
		Assert.Equal(Height.Mempool, loaded.Height);
	}

	[Fact]
	public void SqliteReloadUpsertUpdateAndRemovalPreserveProtection()
	{
		var path = Path.Combine(TestDirectory.Get(), "silent.sqlite");
		var tx = CreateTransaction();
		var ordinary = new SmartTransaction(tx, Height.Mempool);
		var silent = new SmartTransaction(tx, Height.Mempool, isSilentPayment: true);
		using (var storage = TransactionSqliteStorage.FromFile(path, Network.Main))
		{
			storage.BulkInsert(ordinary);
			storage.BulkUpdate(silent);
			storage.BulkInsert([ordinary], upsert: true);
			storage.BulkUpdate(ordinary);
		}
		using var reloaded = TransactionSqliteStorage.FromFile(path, Network.Main);
		Assert.True(reloaded.TryGet(tx.GetHash(), out var saved));
		Assert.True(saved.IsSilentPayment);
		Assert.True(reloaded.TryRemove(tx.GetHash(), out var removed));
		Assert.True(removed!.IsSilentPayment);
	}

	[Fact]
	public void ExistingDatabaseMigratesOldRowsWithFalseDefault()
	{
		var path = Path.Combine(TestDirectory.Get(), "legacy.sqlite");
		var tx = CreateTransaction();
		using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString))
		{
			connection.Open();
			using var command = connection.CreateCommand();
			command.CommandText = """
				CREATE TABLE "transaction" (txid BLOB NOT NULL PRIMARY KEY, block_height INTEGER NOT NULL,
				block_hash BLOB, block_index INTEGER NOT NULL, labels TEXT NOT NULL, first_seen INTEGER NOT NULL,
				is_replacement BOOLEAN NOT NULL, is_speedup BOOLEAN NOT NULL, is_cancellation BOOLEAN NOT NULL, tx BLOB NOT NULL);
				INSERT INTO "transaction" VALUES ($id, $height, NULL, 0, 'legacy', 1570464578, 0, 0, 0, $tx);
				""";
			command.Parameters.AddWithValue("$id", tx.GetHash().ToBytes(lendian: false));
			command.Parameters.AddWithValue("$height", Height.Mempool.Value);
			command.Parameters.AddWithValue("$tx", tx.ToBytes());
			command.ExecuteNonQuery();
		}
		using var migrated = TransactionSqliteStorage.FromFile(path, Network.Main);
		Assert.True(migrated.TryGet(tx.GetHash(), out var loaded));
		Assert.False(loaded.IsSilentPayment);
		Assert.Equal("legacy", loaded.Labels.ToString());
		migrated.BulkInsert([new SmartTransaction(tx, Height.Mempool, isSilentPayment: true)], upsert: true);
		Assert.True(migrated.TryGet(tx.GetHash(), out loaded));
		Assert.True(loaded.IsSilentPayment);
	}

	private static Transaction CreateTransaction()
	{
		var tx = Network.Main.CreateTransaction();
		tx.Inputs.Add(new TxIn(new OutPoint(uint256.One, 0)));
		using var key = new Key();
		tx.Outputs.Add(Money.Coins(0.1m), key.PubKey.WitHash.ScriptPubKey);
		return tx;
	}
}
