using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Userfacing.Bip21;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public class DependencyGraphTaskScheduler
{
	public DependencyGraphTaskScheduler(DependencyGraph graph)
	{
		Graph = graph;
		var allInEdges = Enum.GetValues<CredentialType>()
			.SelectMany(type => Graph.Reissuances.Concat<RequestNode>(Graph.Outputs)
			.SelectMany(node => Graph.EdgeSets[type].InEdges(node)));
		DependencyTasks = allInEdges.ToDictionary(edge => edge, _ => new TaskCompletionSource<Credential>(TaskCreationOptions.RunContinuationsAsynchronously));
	}

	private DependencyGraph Graph { get; }
	private Dictionary<CredentialDependency, TaskCompletionSource<Credential>> DependencyTasks { get; }

	private async Task CompleteConnectionConfirmationAsync(IEnumerable<AliceClient> aliceClients, BobClient bobClient, CancellationToken cancellationToken)
	{
		var aliceNodePairs = PairAliceClientAndRequestNodes(aliceClients, Graph);

		List<Task> connectionConfirmationTasks = new();

		using CancellationTokenSource ctsOnError = new();
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsOnError.Token);

		foreach ((var aliceClient, var node) in aliceNodePairs)
		{
			var amountEdgeTaskCompSources = Graph.OutEdges(node, CredentialType.Amount).Select(edge => DependencyTasks[edge]);
			var vsizeEdgeTaskCompSources = Graph.OutEdges(node, CredentialType.Vsize).Select(edge => DependencyTasks[edge]);
			SmartRequestNode smartRequestNode = new(
				aliceClient.IssuedAmountCredentials.Take(ProtocolConstants.CredentialNumber).Select(Task.FromResult),
				aliceClient.IssuedVsizeCredentials.Take(ProtocolConstants.CredentialNumber).Select(Task.FromResult),
				amountEdgeTaskCompSources,
				vsizeEdgeTaskCompSources);

			var amountsToRequest = Graph.OutEdges(node, CredentialType.Amount).Select(e => e.Value);
			var vsizesToRequest = Graph.OutEdges(node, CredentialType.Vsize).Select(e => e.Value);

			// Although connection confirmation requests support k
			// credential requests, for now we only know which amounts to
			// request after connection confirmation has finished and the
			// final decomposition can be computed, so as a workaround we
			// unconditionally request the full amount in one credential and
			// then do an equivalent reissuance request for every connection
			// confirmation.
			var task = smartRequestNode
				.StartReissuanceAsync(bobClient, amountsToRequest, vsizesToRequest, linkedCts.Token)
				.ContinueWith(
				(t) =>
				{
					if (t.IsFaulted && t.Exception is { } exception)
					{
						// If one task is failing, cancel all the tasks and throw.
						ctsOnError.Cancel();
						throw exception;
					}
				},
				linkedCts.Token);

			connectionConfirmationTasks.Add(task);
		}

		await Task.WhenAll(connectionConfirmationTasks).ConfigureAwait(false);

		var amountEdges = Graph.Inputs.SelectMany(node => Graph.OutEdges(node, CredentialType.Amount));
		var vsizeEdges = Graph.Inputs.SelectMany(node => Graph.OutEdges(node, CredentialType.Vsize));

		// Check if all tasks were finished, otherwise Task.Result will block.
		if (!amountEdges.Concat(vsizeEdges).All(edge => DependencyTasks[edge].Task.IsCompletedSuccessfully))
		{
			throw new InvalidOperationException("Some Input nodes out-edges failed to complete.");
		}
	}

	public async Task StartReissuancesAsync(IEnumerable<AliceClient> aliceClients, BobClient bobClient, CancellationToken cancellationToken)
	{
		var aliceNodePairs = PairAliceClientAndRequestNodes(aliceClients, Graph);

		// Build tasks and link them together.
		List<Task> allTasks = new()
		{
			// Temporary workaround because we don't yet have a mechanism to
			// propagate the final amounts to request amounts to AliceClient's
			// connection confirmation loop even though they are already known
			// after the final successful input registration, which may be well
			// before the connection confirmation phase actually starts.
			CompleteConnectionConfirmationAsync(aliceClients, bobClient, cancellationToken)
		};

		using CancellationTokenSource ctsOnError = new();
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsOnError.Token);

		foreach (var node in Graph.Reissuances)
		{
			var inputAmountEdgeTasks = Graph.InEdges(node, CredentialType.Amount).Select(edge => DependencyTasks[edge].Task);
			var inputVsizeEdgeTasks = Graph.InEdges(node, CredentialType.Vsize).Select(edge => DependencyTasks[edge].Task);

			var outputAmountEdgeTaskCompSources = Graph.OutEdges(node, CredentialType.Amount).Select(edge => DependencyTasks[edge]);
			var outputVsizeEdgeTaskCompSources = Graph.OutEdges(node, CredentialType.Vsize).Select(edge => DependencyTasks[edge]);

			var requestedAmounts = Graph.OutEdges(node, CredentialType.Amount).Select(edge => edge.Value);
			var requestedVSizes = Graph.OutEdges(node, CredentialType.Vsize).Select(edge => edge.Value);

			SmartRequestNode smartRequestNode = new(
				inputAmountEdgeTasks,
				inputVsizeEdgeTasks,
				outputAmountEdgeTaskCompSources,
				outputVsizeEdgeTaskCompSources);

			var task = smartRequestNode
				.StartReissuanceAsync(bobClient, requestedAmounts, requestedVSizes, linkedCts.Token)
				.ContinueWith(
				(t) =>
				{
					if (t.IsFaulted && t.Exception is { } exception)
					{
						// If one task is failing, cancel all the tasks and throw.
						ctsOnError.Cancel();
						throw exception;
					}
				},
				linkedCts.Token);

			allTasks.Add(task);
		}

		await Task.WhenAll(allTasks).ConfigureAwait(false);

		var amountEdges = Graph.Outputs.SelectMany(node => Graph.InEdges(node, CredentialType.Amount));
		var vsizeEdges = Graph.Outputs.SelectMany(node => Graph.InEdges(node, CredentialType.Vsize));

		// Check if all tasks were finished, otherwise Task.Result will block.
		if (!amountEdges.Concat(vsizeEdges).All(edge => DependencyTasks[edge].Task.IsCompletedSuccessfully))
		{
			throw new InvalidOperationException("Some Output nodes in-edges failed to complete");
		}
	}

	public record OutputRegistrationError();
	public record UnknownError(Script ScriptPubKey) : OutputRegistrationError;
	public record AlreadyRegisteredScriptError(Script ScriptPubKey) : OutputRegistrationError;

	public async Task<Result<OutputRegistrationError[]>> StartOutputRegistrationsAsync(IEnumerable<TxOut> txOuts, BobClient bobClient,
		ImmutableList<DateTimeOffset> outputRegistrationScheduledDates, CancellationToken cancellationToken)
	{
		using CancellationTokenSource ctsOnError = new();
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsOnError.Token);

		var nodes = Graph.Outputs.Select(node =>
		{
			var amountCredsToPresentTasks = Graph.InEdges(node, CredentialType.Amount).Select(edge => DependencyTasks[edge].Task);
			var vsizeCredsToPresentTasks = Graph.InEdges(node, CredentialType.Vsize).Select(edge => DependencyTasks[edge].Task);

			SmartRequestNode smartRequestNode = new(
				amountCredsToPresentTasks,
				vsizeCredsToPresentTasks,
				Array.Empty<TaskCompletionSource<Credential>>(),
				Array.Empty<TaskCompletionSource<Credential>>());
			return smartRequestNode;
		});

		var tasks = txOuts.Zip(
			nodes,
			outputRegistrationScheduledDates,
			async (txOut, smartRequestNode, scheduledDate) =>
			{
				try
				{
					var delay = scheduledDate - DateTimeOffset.UtcNow;
					if (delay > TimeSpan.Zero)
					{
						await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
					}
					await smartRequestNode.StartOutputRegistrationAsync(bobClient, txOut.ScriptPubKey, cancellationToken).ConfigureAwait(false);
					return Result<OutputRegistrationError>.Ok();
				}
				catch (WabiSabiProtocolException ex) when (ex.ErrorCode == WabiSabiProtocolErrorCode.AlreadyRegisteredScript)
				{
					Logger.LogDebug($"Output registration error, code:'{ex.ErrorCode}' message:'{ex.Message}'.");
					return new AlreadyRegisteredScriptError(txOut.ScriptPubKey);
				}
				catch (Exception ex)
				{
					Logger.LogInfo($"Output registration error message:'{ex.Message}'.");
					return new UnknownError(txOut.ScriptPubKey);
				}
			})
			.ToImmutableArray();

		await Task.WhenAll(tasks).ConfigureAwait(false);
		return tasks.Select(x => x.Result).SequenceResults();
	}

	private IEnumerable<(AliceClient AliceClient, InputNode Node)> PairAliceClientAndRequestNodes(IEnumerable<AliceClient> aliceClients, DependencyGraph graph)
	{
		var inputNodes = graph.Inputs;

		if (aliceClients.Count() != inputNodes.Count)
		{
			throw new InvalidOperationException("Graph vs Alice inputs mismatch");
		}

		return aliceClients.Zip(inputNodes);
	}
}
