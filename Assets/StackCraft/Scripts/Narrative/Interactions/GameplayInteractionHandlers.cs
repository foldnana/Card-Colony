using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public sealed class InvestigationGameplayInteractionHandler :
        IGameplayInteractionHandler,
        IGameplayInteractionRecoveryHandler
    {
        public const string InvestigationActionId = "exploration.investigate";

        public string ActionId => InvestigationActionId;
        public InteractionParameterSchema Schema { get; } = new(
            new[]
            {
                new InteractionParameterDefinition(
                    "outcomeId", InteractionValueType.String, false),
                new InteractionParameterDefinition(
                    "quantity", InteractionValueType.Int, false)
            });

        public InteractionValidationResult Validate(
            GameplayInteractionRequest request,
            GameplayInteractionContext context) =>
            string.IsNullOrWhiteSpace(request?.ContextId)
                ? new InteractionValidationResult(false, "ContextRequired")
                : InteractionValidationResult.Valid;

        public IGameplayInteractionOperation Begin(
            GameplayInteractionRequest request,
            GameplayInteractionContext context) =>
            CreateOperation(request);

        public IGameplayInteractionOperation Recover(
            GameplayInteractionRequest request,
            GameplayInteractionContext context) =>
            CreateOperation(request);

        private static IGameplayInteractionOperation CreateOperation(
            GameplayInteractionRequest request)
        {
            string outcome = request.Arguments.FirstOrDefault(value =>
                    value.Key == "outcomeId")?.Value as string;
            if (string.IsNullOrWhiteSpace(outcome))
                outcome = "clue_found";
            int quantity = request.Arguments.FirstOrDefault(value =>
                    value.Key == "quantity")?.Value is int value
                ? Math.Max(1, value)
                : 1;
            return new ImmediateGameplayInteractionOperation(
                new InteractionResult(
                    request.OperationId,
                    request.ActionId,
                    GameplayInteractionState.Completed,
                    outcome,
                    string.Empty,
                    request.ContextId,
                    quantity,
                    request.InitiatorActorIds,
                    request.TargetActorIds,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["investigatedContextId"] = request.ContextId
                    },
                    $"{request.OperationId}:Resolved:0"));
        }
    }

    public sealed class CombatGameplayInteractionHandler :
        IGameplayInteractionHandler,
        IGameplayInteractionRecoveryHandler
    {
        public const string CombatActionId = "core.combat";

        public string ActionId => CombatActionId;
        public InteractionParameterSchema Schema =>
            InteractionParameterSchema.Empty;

        public InteractionValidationResult Validate(
            GameplayInteractionRequest request,
            GameplayInteractionContext context)
        {
            if (request == null || request.InitiatorActorIds.Count == 0)
                return new InteractionValidationResult(
                    false, "CombatInitiatorRequired");
            if (request.TargetActorIds.Count == 0)
                return new InteractionValidationResult(
                    false, "CombatTargetRequired");
            if (CombatManager.Instance == null)
                return new InteractionValidationResult(
                    false, "CombatManagerUnavailable");
            if (!TryResolveCards(request.InitiatorActorIds, out _) ||
                !TryResolveCards(request.TargetActorIds, out _))
            {
                return new InteractionValidationResult(
                    false, "CombatActorMissing");
            }
            return InteractionValidationResult.Valid;
        }

        public IGameplayInteractionOperation Begin(
            GameplayInteractionRequest request,
            GameplayInteractionContext context)
        {
            TryResolveCards(request.InitiatorActorIds, out var initiators);
            TryResolveCards(request.TargetActorIds, out var targets);
            return new CombatGameplayInteractionOperation(
                request,
                CombatManager.Instance,
                initiators,
                targets,
                recover: false);
        }

        public IGameplayInteractionOperation Recover(
            GameplayInteractionRequest request,
            GameplayInteractionContext context) =>
            new CombatGameplayInteractionOperation(
                request,
                CombatManager.Instance,
                null,
                null,
                recover: true);

        private static bool TryResolveCards(
            IEnumerable<string> actorIds,
            out List<CardInstance> cards)
        {
            string[] required = (actorIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            cards = CardManager.Instance?.AllCards
                .Where(card => card != null &&
                    required.Contains(card.PersistentId,
                        StringComparer.Ordinal))
                .Distinct()
                .ToList() ?? new List<CardInstance>();
            return required.Length > 0 && cards.Count == required.Length;
        }
    }

    internal sealed class CombatGameplayInteractionOperation :
        IGameplayInteractionOperation,
        IRecoverableGameplayInteractionOperation
    {
        private readonly GameplayInteractionRequest request;
        private readonly CombatManager manager;
        private readonly List<CardInstance> initiators;
        private readonly List<CardInstance> targets;
        private readonly bool recover;
        private bool started;

        public CombatGameplayInteractionOperation(
            GameplayInteractionRequest request,
            CombatManager manager,
            List<CardInstance> initiators,
            List<CardInstance> targets,
            bool recover)
        {
            this.request = request;
            this.manager = manager;
            this.initiators = initiators;
            this.targets = targets;
            this.recover = recover;
        }

        public string OperationId => request.OperationId;
        public GameplayInteractionState State { get; private set; } =
            GameplayInteractionState.Running;
        public bool CanCancel => false;
        public InteractionResult Result { get; private set; }
        public string RecoveryToken => manager?.ResolveFinalSessionId(
            request.OperationId) ?? request.RecoveryToken ?? string.Empty;
        public event Action<GameplayInteractionProgress> Progressed
        {
            add { }
            remove { }
        }
        public event Action<InteractionResult> Completed;

        public void Start()
        {
            if (started)
                return;
            started = true;
            if (manager == null)
            {
                CompleteAborted("CombatManagerUnavailable");
                return;
            }

            manager.OutcomePublished += HandleOutcome;
            if (recover)
            {
                string finalSession = string.IsNullOrWhiteSpace(
                        request.RecoveryToken)
                    ? manager.ResolveFinalSessionId(request.OperationId)
                    : request.RecoveryToken;
                manager.RegisterSessionAlias(
                    request.OperationId, finalSession);
                bool isActive = manager.ActiveCombats.Any(task =>
                    task != null && task.IsOngoing &&
                    task.SessionId == finalSession);
                if (!isActive)
                    CompleteAborted("CombatSessionNotFound");
                return;
            }

            CombatTask task = manager.StartCombat(
                initiators,
                targets,
                playerIsAttacker: true,
                request.OperationId);
            if (task == null)
                CompleteAborted("CombatCouldNotStart");
        }

        public bool TryCancel(string reason) => false;

        private void HandleOutcome(CombatOutcome outcome)
        {
            if (outcome == null ||
                outcome.OriginalSessionId != request.OperationId)
                return;
            string outcomeId = outcome.Result switch
            {
                CombatOutcomeResult.Victory => "victory",
                CombatOutcomeResult.Defeat => "defeat",
                CombatOutcomeResult.Retreated => "retreated",
                _ => "aborted"
            };
            Complete(new InteractionResult(
                request.OperationId,
                request.ActionId,
                GameplayInteractionState.Completed,
                outcomeId,
                string.Empty,
                request.ContextId,
                1,
                request.InitiatorActorIds,
                request.TargetActorIds,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["originalSessionId"] = outcome.OriginalSessionId,
                    ["finalSessionId"] = outcome.FinalSessionId,
                    ["endReason"] = outcome.EndReason
                },
                $"{request.OperationId}:Resolved:0"));
        }

        private void CompleteAborted(string reason)
        {
            Complete(new InteractionResult(
                request.OperationId,
                request.ActionId,
                GameplayInteractionState.Completed,
                "aborted",
                reason,
                request.ContextId,
                0,
                request.InitiatorActorIds,
                request.TargetActorIds));
        }

        private void Complete(InteractionResult result)
        {
            if (State != GameplayInteractionState.Running)
                return;
            if (manager != null)
                manager.OutcomePublished -= HandleOutcome;
            Result = result;
            State = result.State;
            Completed?.Invoke(result);
        }
    }
}
