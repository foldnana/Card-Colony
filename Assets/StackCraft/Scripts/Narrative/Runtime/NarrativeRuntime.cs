using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public enum NarrativeRuntimeState
    {
        Idle = 0,
        Playing = 1,
        WaitingForInput = 2,
        WaitingForChoice = 3,
        WaitingForInteraction = 4,
        Completed = 5,
        Failed = 6,
        WaitingForTime = 7,
        WaitingAtBarrier = 8,
        WaitingForPresentation = 9
    }

    public sealed class NarrativeLine
    {
        public NarrativeLine(
            string actorRole,
            string textKey,
            string fallbackText)
        {
            ActorRole = actorRole ?? string.Empty;
            TextKey = textKey ?? string.Empty;
            FallbackText = fallbackText ?? string.Empty;
        }

        public string ActorRole { get; }
        public string TextKey { get; }
        public string FallbackText { get; }
    }

    /// <summary>
    /// Deterministic, UI-independent narrative command interpreter.
    /// Presentation code observes its state and supplies Continue/SelectChoice.
    /// </summary>
    public sealed class NarrativeRuntime
    {
        private const int MaximumSynchronousSteps = 1024;

        private readonly WorldEffectService worldEffects;
        private readonly GameplayInteractionGateway interactionGateway;
        private readonly IReadOnlyDictionary<string, NarrativeActorHandle>
            actorHandles;
        private readonly INarrativeCommandExecutor commandExecutor;
        private NarrativeDefinition definition;
        private NarrativeNodeDefinition currentNode;
        private string runId;
        private int commandIndex;
        private readonly List<NarrativeChoiceDefinition> currentChoices =
            new();
        private IGameplayInteractionOperation currentOperation;
        private Action<InteractionResult> currentOperationCompleted;
        private NarrativeCommandDefinition currentInteractionCommand;
        private float interactionTimeoutRemaining;
        private float waitTimeRemaining;
        private bool skippingToBarrier;
        private INarrativeCommandOperation currentPresentationOperation;
        private Action<NarrativeCommandResult> presentationCompleted;
        private int runGeneration;

        public NarrativeRuntime(WorldEffectService worldEffects)
            : this(worldEffects, null, null)
        {
        }

        public NarrativeRuntime(
            WorldEffectService worldEffects,
            GameplayInteractionGateway interactionGateway)
            : this(worldEffects, interactionGateway, null)
        {
        }

        public NarrativeRuntime(
            WorldEffectService worldEffects,
            GameplayInteractionGateway interactionGateway,
            IReadOnlyDictionary<string, NarrativeActorHandle> actorHandles)
            : this(worldEffects, interactionGateway, actorHandles, null)
        {
        }

        public NarrativeRuntime(
            WorldEffectService worldEffects,
            GameplayInteractionGateway interactionGateway,
            IReadOnlyDictionary<string, NarrativeActorHandle> actorHandles,
            INarrativeCommandExecutor commandExecutor)
        {
            this.worldEffects = worldEffects ?? throw new ArgumentNullException(
                nameof(worldEffects));
            this.interactionGateway = interactionGateway;
            this.actorHandles = actorHandles;
            this.commandExecutor = commandExecutor;
        }

        public NarrativeRuntimeState State { get; private set; } =
            NarrativeRuntimeState.Idle;
        public NarrativeLine CurrentLine { get; private set; }
        public IList CurrentChoices => currentChoices;
        public string CurrentChoiceTitle { get; private set; } = string.Empty;
        public string FailureReason { get; private set; } = string.Empty;

        public bool Start(NarrativeDefinition narrative, string narrativeRunId)
        {
            if (narrative == null || string.IsNullOrWhiteSpace(narrativeRunId))
                return false;

            DetachCurrentOperation(cancel: true, "NarrativeRestarted");
            runGeneration++;
            NarrativeValidationReport validation =
                NarrativeValidator.Validate(narrative);
            if (!validation.IsValid)
            {
                Fail(string.Join("\n", validation.Errors));
                return false;
            }

            definition = narrative;
            runId = narrativeRunId;
            currentNode = definition.FindNode(definition.EntryNodeId);
            commandIndex = 0;
            CurrentLine = null;
            CurrentChoiceTitle = string.Empty;
            currentChoices.Clear();
            FailureReason = string.Empty;
            State = NarrativeRuntimeState.Playing;
            Advance();
            return State != NarrativeRuntimeState.Failed;
        }

        public void Continue()
        {
            if (State != NarrativeRuntimeState.WaitingForInput &&
                State != NarrativeRuntimeState.WaitingAtBarrier)
                return;

            CurrentLine = null;
            State = NarrativeRuntimeState.Playing;
            Advance();
        }

        public bool SkipToNextBarrier()
        {
            if (definition == null || !definition.CanSkip ||
                State is NarrativeRuntimeState.Idle or
                    NarrativeRuntimeState.Completed or
                    NarrativeRuntimeState.Failed or
                    NarrativeRuntimeState.WaitingForChoice or
                    NarrativeRuntimeState.WaitingForInteraction or
                    NarrativeRuntimeState.WaitingAtBarrier)
                return false;

            skippingToBarrier = true;
            if (State == NarrativeRuntimeState.WaitingForPresentation)
            {
                currentPresentationOperation?.CompleteImmediately();
                return true;
            }
            if (State == NarrativeRuntimeState.WaitingForInput)
                CurrentLine = null;
            waitTimeRemaining = 0f;
            State = NarrativeRuntimeState.Playing;
            Advance();
            return true;
        }

        public bool SelectChoice(string choiceId)
        {
            if (State != NarrativeRuntimeState.WaitingForChoice)
                return false;

            NarrativeChoiceDefinition choice = currentChoices.FirstOrDefault(
                value => string.Equals(value.ChoiceId, choiceId,
                    StringComparison.Ordinal));
            if (choice == null)
                return false;

            currentChoices.Clear();
            CurrentChoiceTitle = string.Empty;
            return JumpTo(choice.TargetNodeId);
        }

        public void Cancel(string reason = "Cancelled")
        {
            if (State is NarrativeRuntimeState.Idle or
                NarrativeRuntimeState.Completed or
                NarrativeRuntimeState.Failed)
                return;
            runGeneration++;
            DetachCurrentOperation(cancel: true, reason);
            DetachPresentationOperation(cancel: true, reason);
            Fail(reason);
        }

        /// <summary>
        /// Advances only real-time narrative waits. A non-positive interaction
        /// timeout means that the interaction is allowed to wait indefinitely.
        /// </summary>
        public void AdvanceTime(float unscaledDeltaSeconds)
        {
            if (State == NarrativeRuntimeState.WaitingForTime)
            {
                if (unscaledDeltaSeconds <= 0f)
                    return;
                waitTimeRemaining -= unscaledDeltaSeconds;
                if (waitTimeRemaining > 0f)
                    return;
                waitTimeRemaining = 0f;
                State = NarrativeRuntimeState.Playing;
                Advance();
                return;
            }

            if (State != NarrativeRuntimeState.WaitingForInteraction ||
                currentOperation == null ||
                interactionTimeoutRemaining <= 0f ||
                unscaledDeltaSeconds <= 0f)
                return;

            interactionTimeoutRemaining -= unscaledDeltaSeconds;
            if (interactionTimeoutRemaining > 0f)
                return;

            NarrativeCommandDefinition command = currentInteractionCommand;
            DetachCurrentOperation(cancel: true, "InteractionTimeout");
            State = NarrativeRuntimeState.Playing;
            if (command == null ||
                !HandleFailure(command, "InteractionTimeout"))
                return;
            Advance();
        }

        private void Advance()
        {
            for (int steps = 0; steps < MaximumSynchronousSteps; steps++)
            {
                if (currentNode == null)
                {
                    Fail("Narrative node is missing.");
                    return;
                }

                if (commandIndex >= currentNode.Commands.Count)
                {
                    if (string.IsNullOrWhiteSpace(currentNode.NextNodeId))
                    {
                        Complete();
                        return;
                    }

                    if (!MoveToNode(currentNode.NextNodeId))
                        return;
                    continue;
                }

                NarrativeCommandDefinition command =
                    currentNode.Commands[commandIndex++];
                if (command == null)
                    continue;

                NarrativeBarrierType barrier = GetBarrier(command);
                if (skippingToBarrier && barrier != NarrativeBarrierType.None)
                {
                    skippingToBarrier = false;
                    if (barrier is NarrativeBarrierType.SceneTransition or
                        NarrativeBarrierType.IrreversibleConfirmation or
                        NarrativeBarrierType.Checkpoint)
                    {
                        State = NarrativeRuntimeState.WaitingAtBarrier;
                        return;
                    }
                }

                switch (command.Type)
                {
                    case NarrativeCommandType.ShowNarration:
                    case NarrativeCommandType.ShowDialogue:
                        if (skippingToBarrier)
                            break;
                        PresentLine(command.DialogueParameters);
                        return;
                    case NarrativeCommandType.ShowChoice:
                        PresentChoices(command.DialogueParameters);
                        return;
                    case NarrativeCommandType.Wait:
                        if (skippingToBarrier)
                            break;
                        waitTimeRemaining = command.TimingParameters?.Duration ??
                            0f;
                        if (waitTimeRemaining <= 0f)
                            break;
                        State = NarrativeRuntimeState.WaitingForTime;
                        return;
                    case NarrativeCommandType.Jump:
                        if (!MoveToNode(
                            command.FlowParameters?.TargetNodeId))
                            return;
                        break;
                    case NarrativeCommandType.SetWorldFact:
                        if (!ApplyWorldEffect(command))
                            return;
                        break;
                    case NarrativeCommandType.StartQuest:
                        if (!ApplyWorldEffect(command))
                            return;
                        break;
                    case NarrativeCommandType.ApplyDamage:
                        if (!ApplyDamage(command))
                            return;
                        break;
                    case NarrativeCommandType.ExecuteInteraction:
                        skippingToBarrier = false;
                        if (!ExecuteInteraction(command))
                            return;
                        if (State == NarrativeRuntimeState.WaitingForInteraction)
                            return;
                        break;
                    case NarrativeCommandType.EndNarrative:
                        skippingToBarrier = false;
                        Complete();
                        return;
                    case NarrativeCommandType.Checkpoint:
                    case NarrativeCommandType.SceneTransition:
                    case NarrativeCommandType.IrreversibleConfirmation:
                        State = NarrativeRuntimeState.WaitingAtBarrier;
                        return;
                    case NarrativeCommandType.AcquireActorControl:
                    case NarrativeCommandType.ReleaseActorControl:
                    case NarrativeCommandType.MoveToActor:
                    case NarrativeCommandType.MoveToMarker:
                    case NarrativeCommandType.FaceActor:
                    case NarrativeCommandType.ReturnToOrigin:
                    case NarrativeCommandType.ShowSpeechBubble:
                    case NarrativeCommandType.ShowEmote:
                    case NarrativeCommandType.SpawnActor:
                    case NarrativeCommandType.DespawnActor:
                    case NarrativeCommandType.PlayCinematicAttack:
                    case NarrativeCommandType.EnterVisualNovelMode:
                    case NarrativeCommandType.ExitVisualNovelMode:
                    case NarrativeCommandType.ShowFullscreenImage:
                    case NarrativeCommandType.HideFullscreenImage:
                    case NarrativeCommandType.FocusActor:
                    case NarrativeCommandType.ShakeCamera:
                        if (!ExecutePresentation(command))
                            return;
                        if (State == NarrativeRuntimeState.WaitingForPresentation)
                            return;
                        break;
                    default:
                        if (!HandleUnsupportedCommand(command))
                            return;
                        break;
                }
            }

            Fail("Narrative exceeded the synchronous command limit.");
        }

        private static NarrativeBarrierType GetBarrier(
            NarrativeCommandDefinition command)
        {
            if (command == null)
                return NarrativeBarrierType.None;
            if (command.BarrierType != NarrativeBarrierType.None)
                return command.BarrierType;
            return command.Type switch
            {
                NarrativeCommandType.ShowChoice =>
                    NarrativeBarrierType.ImportantChoice,
                NarrativeCommandType.ExecuteInteraction =>
                    NarrativeBarrierType.GameplayInteraction,
                NarrativeCommandType.SceneTransition =>
                    NarrativeBarrierType.SceneTransition,
                NarrativeCommandType.IrreversibleConfirmation =>
                    NarrativeBarrierType.IrreversibleConfirmation,
                NarrativeCommandType.Checkpoint =>
                    NarrativeBarrierType.Checkpoint,
                NarrativeCommandType.EndNarrative =>
                    NarrativeBarrierType.NarrativeEnd,
                _ => NarrativeBarrierType.None
            };
        }

        private bool ExecutePresentation(NarrativeCommandDefinition command)
        {
            if (commandExecutor == null)
                return HandleFailure(command,
                    "Narrative presentation executor is unavailable.");
            INarrativeCommandOperation operation;
            try
            {
                operation = commandExecutor.Execute(
                    command, skippingToBarrier);
            }
            catch (Exception exception)
            {
                return HandleFailure(command,
                    $"NarrativePresentationException:{exception.Message}");
            }
            if (operation == null)
                return HandleFailure(command,
                    "Narrative presentation operation was not created.");
            if (operation.IsCompleted)
                return operation.Result.Success ||
                    HandleFailure(command, operation.Result.Error);

            currentPresentationOperation = operation;
            int expectedGeneration = runGeneration;
            presentationCompleted = result =>
            {
                if (expectedGeneration != runGeneration ||
                    !ReferenceEquals(currentPresentationOperation, operation) ||
                    State != NarrativeRuntimeState.WaitingForPresentation)
                    return;
                DetachPresentationOperation(false, string.Empty);
                State = NarrativeRuntimeState.Playing;
                if (result.Success || HandleFailure(command, result.Error))
                    Advance();
            };
            State = NarrativeRuntimeState.WaitingForPresentation;
            operation.Completed += presentationCompleted;
            return true;
        }

        private void DetachPresentationOperation(bool cancel, string reason)
        {
            INarrativeCommandOperation operation = currentPresentationOperation;
            Action<NarrativeCommandResult> callback = presentationCompleted;
            currentPresentationOperation = null;
            presentationCompleted = null;
            if (operation == null)
                return;
            if (callback != null)
                operation.Completed -= callback;
            if (cancel && !operation.IsCompleted)
                operation.Cancel(reason ?? string.Empty);
        }

        private void PresentLine(NarrativeDialogueParameters parameters)
        {
            parameters ??= new NarrativeDialogueParameters();
            CurrentLine = new NarrativeLine(
                parameters.ActorRole,
                parameters.TextKey,
                parameters.FallbackText);
            State = NarrativeRuntimeState.WaitingForInput;
        }

        private void PresentChoices(NarrativeDialogueParameters parameters)
        {
            currentChoices.Clear();
            CurrentChoiceTitle = parameters?.ChoiceTitleFallback ??
                string.Empty;
            if (parameters?.Choices != null)
                currentChoices.AddRange(parameters.Choices.Where(c => c != null));
            if (currentChoices.Count == 0)
            {
                Fail("Choice command has no choices.");
                return;
            }

            State = NarrativeRuntimeState.WaitingForChoice;
        }

        private bool ApplyWorldEffect(NarrativeCommandDefinition command)
        {
            NarrativeEffectParameters effect = command.EffectParameters;
            if (effect == null)
                return HandleFailure(command, "World effect parameters are missing.");

            WorldEffectResult result = worldEffects.Apply(
                definition.Id,
                definition.Version,
                runId,
                currentNode.Id,
                new WorldEffectRequest(
                    effect.EffectType,
                    effect.ResultId,
                    effect.TargetId,
                    effect.SecondaryTargetId,
                    effect.StringValue,
                    effect.IntValue,
                    effect.BoolValue));
            return result.Success || HandleFailure(command, result.Error);
        }

        private bool ApplyDamage(NarrativeCommandDefinition command)
        {
            NarrativeActorActionParameters actorParameters =
                command.ActorActionParameters;
            NarrativeEffectParameters effect = command.EffectParameters;
            if (actorParameters == null || effect == null ||
                actorHandles == null ||
                !actorHandles.TryGetValue(
                    actorParameters.ActorRole,
                    out NarrativeActorHandle actor) ||
                actor?.Card == null)
            {
                return HandleFailure(command, "DamageActorMissing");
            }

            WorldEffectResult result = worldEffects.ApplyDamage(
                definition.Id,
                definition.Version,
                runId,
                currentNode.Id,
                effect.ResultId,
                actor.Card,
                effect.IntValue);
            return result.Success || HandleFailure(command, result.Error);
        }

        private bool ExecuteInteraction(NarrativeCommandDefinition command)
        {
            if (interactionGateway == null)
                return HandleFailure(command, "Interaction gateway is unavailable.");
            NarrativeInteractionParameters parameters =
                command.InteractionParameters;
            if (parameters == null ||
                string.IsNullOrWhiteSpace(parameters.ActionId))
            {
                return HandleFailure(command,
                    "Interaction action identifier is missing.");
            }

            string operationId = string.Join(":",
                definition.Id,
                definition.Version,
                runId,
                currentNode.Id,
                command.CommandId);
            var request = new GameplayInteractionRequest(
                operationId,
                parameters.ActionId)
            {
                ContextId = parameters.ContextId,
                TimeoutSeconds = parameters.TimeoutSeconds,
                ProtagonistParticipated = RolesIncludeProtagonist(
                    parameters),
                CreditedToParty = RolesIncludePartyMember(parameters)
            };
            request.InitiatorActorIds.AddRange(ResolveActorIds(
                parameters.InitiatorRoles));
            request.TargetActorIds.AddRange(ResolveActorIds(
                parameters.TargetRoles));
            foreach (NarrativeInteractionArgument argument in
                     parameters.Arguments)
            {
                request.Arguments.Add(new GameplayInteractionArgument(
                    argument.Key,
                    argument.ValueType,
                    GetInteractionArgumentValue(argument)));
            }

            IGameplayInteractionOperation operation;
            try
            {
                operation = interactionGateway.Execute(request);
            }
            catch (Exception exception)
            {
                return HandleFailure(command,
                    $"InteractionHandlerException:{exception.Message}");
            }
            if (operation == null)
                return HandleFailure(command,
                    "Interaction operation was not created.");
            if (operation.Result != null)
                return HandleInteractionResult(command, parameters,
                    operation.Result);

            State = NarrativeRuntimeState.WaitingForInteraction;
            currentOperation = operation;
            currentInteractionCommand = command;
            interactionTimeoutRemaining = Math.Max(0f,
                parameters.TimeoutSeconds);
            int expectedGeneration = runGeneration;
            currentOperationCompleted = result =>
            {
                if (expectedGeneration != runGeneration ||
                    !ReferenceEquals(currentOperation, operation) ||
                    State != NarrativeRuntimeState.WaitingForInteraction)
                    return;
                try
                {
                    DetachCurrentOperation(cancel: false, string.Empty);
                    State = NarrativeRuntimeState.Playing;
                    if (HandleInteractionResult(command, parameters, result))
                        Advance();
                }
                catch (Exception exception)
                {
                    Fail($"InteractionCompletionException:{exception.Message}");
                }
            };
            operation.Completed += currentOperationCompleted;
            return true;
        }

        private void DetachCurrentOperation(bool cancel, string reason)
        {
            IGameplayInteractionOperation operation = currentOperation;
            Action<InteractionResult> callback = currentOperationCompleted;
            currentOperation = null;
            currentOperationCompleted = null;
            currentInteractionCommand = null;
            interactionTimeoutRemaining = 0f;
            if (operation == null)
                return;
            if (callback != null)
                operation.Completed -= callback;
            if (cancel && operation.CanCancel)
                operation.TryCancel(reason ?? string.Empty);
        }

        private bool HandleInteractionResult(
            NarrativeCommandDefinition command,
            NarrativeInteractionParameters parameters,
            InteractionResult result)
        {
            if (result == null ||
                result.State == GameplayInteractionState.Failed ||
                result.State == GameplayInteractionState.Cancelled)
            {
                return HandleFailure(
                    command,
                    result?.FailureCode ?? "InteractionFailed");
            }

            NarrativeInteractionOutcomeBranch branch =
                parameters.OutcomeBranches.FirstOrDefault(value =>
                    value != null && string.Equals(value.OutcomeId,
                        result.OutcomeId, StringComparison.Ordinal));
            return branch == null || MoveToNode(branch.TargetNodeId);
        }

        private static object GetInteractionArgumentValue(
            NarrativeInteractionArgument argument)
        {
            return argument.ValueType switch
            {
                InteractionValueType.Int => argument.IntValue,
                InteractionValueType.Float => argument.FloatValue,
                InteractionValueType.Bool => argument.BoolValue,
                _ => argument.StringValue
            };
        }

        private bool RolesIncludeProtagonist(
            NarrativeInteractionParameters parameters)
        {
            GameData data = worldEffects.GameData;
            string protagonistId = data?.ProtagonistPersistentId;
            return ParticipatingBindings(parameters).Any(binding =>
                binding.ResolveMode == NarrativeActorResolveMode.PartyLeader ||
                binding.ResolveMode == NarrativeActorResolveMode.PersistentId &&
                !string.IsNullOrWhiteSpace(protagonistId) &&
                string.Equals(binding.PersistentId, protagonistId,
                    StringComparison.Ordinal));
        }

        private bool RolesIncludePartyMember(
            NarrativeInteractionParameters parameters)
        {
            GameData data = worldEffects.GameData;
            return ParticipatingBindings(parameters).Any(binding =>
                binding.ResolveMode == NarrativeActorResolveMode.PartyLeader ||
                binding.ResolveMode == NarrativeActorResolveMode.PersistentId &&
                data?.PartyMembers?.Any(member => member != null &&
                    string.Equals(member.PersistentId, binding.PersistentId,
                        StringComparison.Ordinal)) == true);
        }

        private IEnumerable<NarrativeActorBinding> ParticipatingBindings(
            NarrativeInteractionParameters parameters)
        {
            var roles = new HashSet<string>(
                parameters.InitiatorRoles.Concat(parameters.TargetRoles),
                StringComparer.Ordinal);
            return definition.ActorBindings.Where(binding =>
                binding != null && roles.Contains(binding.RoleId));
        }

        private IEnumerable<string> ResolveActorIds(
            IEnumerable<string> roles)
        {
            foreach (string role in roles ?? Array.Empty<string>())
            {
                if (actorHandles != null &&
                    actorHandles.TryGetValue(role, out var handle) &&
                    !string.IsNullOrWhiteSpace(handle?.Card?.PersistentId))
                {
                    yield return handle.Card.PersistentId;
                    continue;
                }
                NarrativeActorBinding binding = definition.ActorBindings
                    .FirstOrDefault(value => value != null &&
                        string.Equals(value.RoleId, role,
                            StringComparison.Ordinal));
                string persistentId = binding?.ResolveMode switch
                {
                    NarrativeActorResolveMode.PartyLeader =>
                        worldEffects.GameData?.ProtagonistPersistentId,
                    NarrativeActorResolveMode.PersistentId =>
                        binding.PersistentId,
                    _ => string.Empty
                };
                if (!string.IsNullOrWhiteSpace(persistentId))
                    yield return persistentId;
            }
        }

        private bool HandleUnsupportedCommand(NarrativeCommandDefinition command)
        {
            return HandleFailure(command,
                $"Command {command.Type} is not available in P0-A.");
        }

        private bool HandleFailure(
            NarrativeCommandDefinition command,
            string reason)
        {
            switch (command.FailurePolicy)
            {
                case NarrativeFailurePolicy.SkipCommand:
                    return true;
                case NarrativeFailurePolicy.JumpToFailureNode:
                    return MoveToNode(command.FailureNodeId);
                default:
                    Fail(reason);
                    return false;
            }
        }

        private bool JumpTo(string nodeId)
        {
            State = NarrativeRuntimeState.Playing;
            if (!MoveToNode(nodeId))
                return false;
            Advance();
            return State != NarrativeRuntimeState.Failed;
        }

        private bool MoveToNode(string nodeId)
        {
            NarrativeNodeDefinition node = definition?.FindNode(nodeId);
            if (node == null)
            {
                Fail($"Narrative node '{nodeId}' does not exist.");
                return false;
            }

            currentNode = node;
            commandIndex = 0;
            return true;
        }

        private void Complete()
        {
            DetachPresentationOperation(false, string.Empty);
            CurrentLine = null;
            currentChoices.Clear();
            CurrentChoiceTitle = string.Empty;
            waitTimeRemaining = 0f;
            skippingToBarrier = false;
            State = NarrativeRuntimeState.Completed;
        }

        private void Fail(string reason)
        {
            DetachPresentationOperation(true, reason);
            CurrentLine = null;
            currentChoices.Clear();
            CurrentChoiceTitle = string.Empty;
            waitTimeRemaining = 0f;
            skippingToBarrier = false;
            FailureReason = reason ?? string.Empty;
            State = NarrativeRuntimeState.Failed;
        }
    }
}
