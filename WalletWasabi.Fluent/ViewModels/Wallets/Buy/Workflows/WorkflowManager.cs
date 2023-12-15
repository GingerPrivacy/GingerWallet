using System.Threading;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public abstract partial class WorkflowManager : ReactiveObject
{
	[AutoNotify(SetterModifier = AccessModifier.Protected)]
	private Workflow? _currentWorkflow;

	public WorkflowState WorkflowState { get; } = new();

	public void ResetWorkflow()
	{
		if (_currentWorkflow?.CanCancel() ?? true)
		{
			CurrentWorkflow = null;
		}
	}

	public void InvokeOutputWorkflows(Action<string, ChatMessageMetaData> onAssistantMessage, CancellationToken cancellationToken)
	{
		if (CurrentWorkflow is null)
		{
			return;
		}

		while (true)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}

			var peekStep = CurrentWorkflow.PeekNextStep();
			if (peekStep is null)
			{
				break;
			}

			var nextStep = CurrentWorkflow.TryToGetNextStep(cancellationToken);
			if (nextStep is null)
			{
				continue;
			}

			if (nextStep.UserInputValidator.CanDisplayMessage())
			{
				var message = nextStep.UserInputValidator.GetFinalMessage();
				if (message is not null)
				{
					onAssistantMessage(message, new ChatMessageMetaData(nextStep.UserInputValidator.Tag));
				}
			}

			if (nextStep.RequiresUserInput)
			{
				break;
			}

			if (!nextStep.UserInputValidator.OnCompletion())
			{
				break;
			}
		}
	}

	public bool InvokeInputWorkflows(Action<string, ChatMessageMetaData> onUserMessage, Action<string, ChatMessageMetaData> onAssistantMessage, object? args, CancellationToken cancellationToken)
	{
		WorkflowState.SignalValid(false);

		if (CurrentWorkflow is null)
		{
			return false;
		}

		if (CurrentWorkflow.CurrentStep is not null)
		{
			if (!CurrentWorkflow.CurrentStep.UserInputValidator.OnCompletion())
			{
				return false;
			}

			if (CurrentWorkflow.CurrentStep.UserInputValidator.CanDisplayMessage())
			{
				var message = CurrentWorkflow.CurrentStep.UserInputValidator.GetFinalMessage();

				if (message is not null)
				{
					onUserMessage(message, new ChatMessageMetaData(CurrentWorkflow.CurrentStep.UserInputValidator.Tag));
				}
			}
		}

		if (CurrentWorkflow.IsCompleted)
		{
			OnInvokeNextWorkflow(null, args, onAssistantMessage, cancellationToken);
			return false;
		}

		var nextStep = CurrentWorkflow.TryToGetNextStep(cancellationToken);
		if (nextStep is null)
		{
			return false;
		}

		if (!nextStep.UserInputValidator.OnCompletion())
		{
			return false;
		}

		if (!nextStep.RequiresUserInput)
		{
			if (nextStep.UserInputValidator.CanDisplayMessage())
			{
				var nextMessage = nextStep.UserInputValidator.GetFinalMessage();
				if (nextMessage is not null)
				{
					onAssistantMessage(nextMessage, new ChatMessageMetaData(nextStep.UserInputValidator.Tag));
				}
			}
		}

		if (nextStep.IsCompleted)
		{
			InvokeOutputWorkflows(onAssistantMessage, cancellationToken);
		}

		return true;
	}

	public abstract bool OnInvokeNextWorkflow(
		string? context,
		object? args,
		Action<string, ChatMessageMetaData> onAssistantMessage,
		CancellationToken cancellationToken);
}