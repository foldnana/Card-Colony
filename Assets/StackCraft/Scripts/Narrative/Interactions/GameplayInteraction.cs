using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public enum GameplayInteractionPhase
    {
        Resolved = 0,
        Started = 1,
        Progressed = 2,
        Cancelled = 3
    }

    public enum GameplayInteractionState
    {
        Running = 0,
        Completed = 1,
        Failed = 2,
        Cancelled = 3
    }

    public enum InteractionValueType
    {
        String = 0,
        Int = 1,
        Float = 2,
        Bool = 3,
        ActorRole = 4,
        CardDefinitionId = 5,
        AssetReference = 6
    }

    [Serializable]
    public sealed class GameplayInteractionArgument
    {
        public string Key { get; }
        public InteractionValueType ValueType { get; }
        public object Value { get; }

        public GameplayInteractionArgument(
            string key,
            InteractionValueType valueType,
            object value)
        {
            Key = key ?? string.Empty;
            ValueType = valueType;
            Value = value;
        }
    }

    public sealed class InteractionParameterDefinition
    {
        public string Key { get; }
        public InteractionValueType ValueType { get; }
        public bool Required { get; }

        public InteractionParameterDefinition(
            string key,
            InteractionValueType valueType,
            bool required)
        {
            Key = key ?? string.Empty;
            ValueType = valueType;
            Required = required;
        }
    }

    public sealed class InteractionValidationResult
    {
        public static readonly InteractionValidationResult Valid =
            new(true, string.Empty);

        public bool IsValid { get; }
        public string ErrorCode { get; }

        public InteractionValidationResult(bool isValid, string errorCode)
        {
            IsValid = isValid;
            ErrorCode = errorCode ?? string.Empty;
        }
    }

    public sealed class InteractionParameterSchema
    {
        private readonly Dictionary<string, InteractionParameterDefinition>
            definitions;

        public static InteractionParameterSchema Empty { get; } =
            new(Array.Empty<InteractionParameterDefinition>());

        public IReadOnlyCollection<InteractionParameterDefinition>
            Definitions => definitions.Values;

        public InteractionParameterSchema(
            IEnumerable<InteractionParameterDefinition> values)
        {
            definitions = (values ??
                    Array.Empty<InteractionParameterDefinition>())
                .Where(value => value != null &&
                    !string.IsNullOrWhiteSpace(value.Key))
                .GroupBy(value => value.Key, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
        }

        public InteractionValidationResult Validate(
            IReadOnlyList<GameplayInteractionArgument> arguments)
        {
            arguments ??= Array.Empty<GameplayInteractionArgument>();
            foreach (InteractionParameterDefinition definition in
                     definitions.Values)
            {
                GameplayInteractionArgument argument = arguments
                    .FirstOrDefault(value => value != null &&
                        value.Key == definition.Key);
                if (definition.Required && argument == null)
                {
                    return new InteractionValidationResult(
                        false,
                        $"MissingArgument:{definition.Key}");
                }
                if (argument != null &&
                    argument.ValueType != definition.ValueType)
                {
                    return new InteractionValidationResult(
                        false,
                        $"InvalidArgumentType:{definition.Key}");
                }
            }
            foreach (GameplayInteractionArgument argument in arguments)
            {
                if (argument == null ||
                    !definitions.ContainsKey(argument.Key))
                {
                    return new InteractionValidationResult(
                        false,
                        $"UnknownArgument:{argument?.Key}");
                }
            }
            return InteractionValidationResult.Valid;
        }
    }

    [Serializable]
    public sealed class GameplayInteractionRequest
    {
        public string OperationId { get; }
        public string ActionId { get; }
        public List<string> InitiatorActorIds { get; } = new();
        public List<string> TargetActorIds { get; } = new();
        public string ContextId { get; set; } = string.Empty;
        public List<GameplayInteractionArgument> Arguments { get; } = new();
        public float TimeoutSeconds { get; set; }
        public bool CreditedToParty { get; set; }
        public bool ProtagonistParticipated { get; set; }

        public GameplayInteractionRequest(
            string operationId,
            string actionId)
        {
            OperationId = operationId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
        }
    }

    public sealed class GameplayInteractionProgress
    {
        public int Sequence { get; }
        public string OutcomeId { get; }
        public int Quantity { get; }

        public GameplayInteractionProgress(
            int sequence,
            string outcomeId,
            int quantity)
        {
            Sequence = Math.Max(0, sequence);
            OutcomeId = outcomeId ?? string.Empty;
            Quantity = Math.Max(0, quantity);
        }
    }

    public sealed class InteractionResult
    {
        public string OperationId { get; }
        public string ActionId { get; }
        public GameplayInteractionState State { get; }
        public string OutcomeId { get; }
        public string FailureCode { get; }
        public string ContextId { get; }
        public int Quantity { get; }

        public InteractionResult(
            string operationId,
            string actionId,
            GameplayInteractionState state,
            string outcomeId,
            string failureCode,
            string contextId,
            int quantity = 1)
        {
            OperationId = operationId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            State = state;
            OutcomeId = outcomeId ?? string.Empty;
            FailureCode = failureCode ?? string.Empty;
            ContextId = contextId ?? string.Empty;
            Quantity = Math.Max(0, quantity);
        }
    }

    public sealed class GameplayInteractionContext
    {
        public GameData GameData { get; }

        public GameplayInteractionContext(GameData gameData)
        {
            GameData = gameData;
        }
    }

    public interface IGameplayInteractionOperation
    {
        string OperationId { get; }
        GameplayInteractionState State { get; }
        bool CanCancel { get; }
        InteractionResult Result { get; }
        event Action<GameplayInteractionProgress> Progressed;
        event Action<InteractionResult> Completed;
        void Start();
        bool TryCancel(string reason);
    }

    public interface IGameplayInteractionHandler
    {
        string ActionId { get; }
        InteractionParameterSchema Schema { get; }
        InteractionValidationResult Validate(
            GameplayInteractionRequest request,
            GameplayInteractionContext context);
        IGameplayInteractionOperation Begin(
            GameplayInteractionRequest request,
            GameplayInteractionContext context);
    }

    public sealed class GameplayInteractionRegistry
    {
        private readonly Dictionary<string, IGameplayInteractionHandler>
            handlers = new(StringComparer.Ordinal);

        public bool Register(IGameplayInteractionHandler handler)
        {
            if (handler == null || string.IsNullOrWhiteSpace(handler.ActionId))
                return false;
            return handlers.TryAdd(handler.ActionId, handler);
        }

        public bool TryGet(
            string actionId,
            out IGameplayInteractionHandler handler) =>
            handlers.TryGetValue(actionId ?? string.Empty, out handler);
    }

    public sealed class GameplayInteractionEventData
    {
        public string PersistedEventId { get; }
        public string OperationId { get; }
        public string ActionId { get; }
        public GameplayInteractionPhase Phase { get; }
        public string OutcomeId { get; }
        public string ContextId { get; }
        public int Quantity { get; }
        public bool CreditedToParty { get; }
        public bool ProtagonistParticipated { get; }

        public GameplayInteractionEventData(
            string persistedEventId,
            GameplayInteractionRequest request,
            GameplayInteractionPhase phase,
            string outcomeId,
            int quantity)
        {
            PersistedEventId = persistedEventId ?? string.Empty;
            OperationId = request?.OperationId ?? string.Empty;
            ActionId = request?.ActionId ?? string.Empty;
            Phase = phase;
            OutcomeId = outcomeId ?? string.Empty;
            ContextId = request?.ContextId ?? string.Empty;
            Quantity = Math.Max(0, quantity);
            CreditedToParty = request?.CreditedToParty == true;
            ProtagonistParticipated =
                request?.ProtagonistParticipated == true;
        }
    }

    public interface IGameplayInteractionEventSink
    {
        void Publish(GameplayInteractionEventData interactionEvent);
    }

    public sealed class BufferedGameplayInteractionEventSink :
        IGameplayInteractionEventSink
    {
        private readonly List<GameplayInteractionEventData> events = new();

        public IList Events => events;

        public void Publish(GameplayInteractionEventData interactionEvent)
        {
            if (interactionEvent != null)
                events.Add(interactionEvent);
        }
    }

    public sealed class GameplayInteractionGateway
    {
        private readonly GameplayInteractionRegistry registry;
        private readonly IGameplayInteractionEventSink eventSink;
        private readonly GameplayInteractionContext context;

        public GameplayInteractionGateway(
            GameplayInteractionRegistry registry,
            IGameplayInteractionEventSink eventSink)
            : this(registry, eventSink, null)
        {
        }

        public GameplayInteractionGateway(
            GameplayInteractionRegistry registry,
            IGameplayInteractionEventSink eventSink,
            GameplayInteractionContext context)
        {
            this.registry = registry ??
                throw new ArgumentNullException(nameof(registry));
            this.eventSink = eventSink;
            this.context = context ?? new GameplayInteractionContext(null);
        }

        public IGameplayInteractionOperation Execute(
            GameplayInteractionRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.OperationId) ||
                string.IsNullOrWhiteSpace(request.ActionId))
            {
                return StartFailure(request, "InvalidRequest");
            }
            if (!registry.TryGet(request.ActionId, out var handler))
                return StartFailure(request, "HandlerNotFound");

            InteractionValidationResult schema = handler.Schema?.Validate(
                request.Arguments) ?? InteractionValidationResult.Valid;
            if (!schema.IsValid)
                return StartFailure(request, schema.ErrorCode);
            InteractionValidationResult validation = handler.Validate(
                request,
                context);
            if (validation == null || !validation.IsValid)
            {
                return StartFailure(
                    request,
                    validation?.ErrorCode ?? "ValidationFailed");
            }

            IGameplayInteractionOperation operation = handler.Begin(
                request,
                context);
            if (operation == null)
                return StartFailure(request, "OperationNotCreated");
            operation.Progressed += progress => eventSink?.Publish(
                CreateEvent(
                    request,
                    GameplayInteractionPhase.Progressed,
                    progress?.OutcomeId,
                    Math.Max(1, progress?.Quantity ?? 0),
                    progress?.Sequence ?? 0));
            operation.Completed += result => eventSink?.Publish(
                CreateEvent(
                    request,
                    result?.State == GameplayInteractionState.Cancelled
                        ? GameplayInteractionPhase.Cancelled
                        : GameplayInteractionPhase.Resolved,
                    result?.OutcomeId,
                    Math.Max(1, result?.Quantity ?? 0),
                    0));
            eventSink?.Publish(CreateEvent(
                request,
                GameplayInteractionPhase.Started,
                string.Empty,
                1,
                0));
            operation.Start();
            return operation;
        }

        private IGameplayInteractionOperation StartFailure(
            GameplayInteractionRequest request,
            string failureCode)
        {
            request ??= new GameplayInteractionRequest(
                Guid.NewGuid().ToString("N"),
                string.Empty);
            var operation = new ImmediateGameplayInteractionOperation(
                new InteractionResult(
                    request.OperationId,
                    request.ActionId,
                    GameplayInteractionState.Failed,
                    string.Empty,
                    failureCode,
                    request.ContextId,
                    0));
            operation.Start();
            return operation;
        }

        private static GameplayInteractionEventData CreateEvent(
            GameplayInteractionRequest request,
            GameplayInteractionPhase phase,
            string outcomeId,
            int quantity,
            int sequence)
        {
            string eventId =
                $"{request.OperationId}:{phase}:{Math.Max(0, sequence)}";
            return new GameplayInteractionEventData(
                eventId,
                request,
                phase,
                outcomeId,
                quantity);
        }
    }

    public sealed class ImmediateGameplayInteractionOperation :
        IGameplayInteractionOperation
    {
        private readonly InteractionResult pendingResult;
        private bool started;

        public string OperationId => pendingResult.OperationId;
        public GameplayInteractionState State { get; private set; } =
            GameplayInteractionState.Running;
        public bool CanCancel => !started;
        public InteractionResult Result { get; private set; }
        public event Action<GameplayInteractionProgress> Progressed
        {
            add { }
            remove { }
        }
        public event Action<InteractionResult> Completed;

        public ImmediateGameplayInteractionOperation(
            InteractionResult pendingResult)
        {
            this.pendingResult = pendingResult ??
                throw new ArgumentNullException(nameof(pendingResult));
        }

        public void Start()
        {
            if (started)
                return;
            started = true;
            Result = pendingResult;
            State = pendingResult.State;
            Completed?.Invoke(Result);
        }

        public bool TryCancel(string reason)
        {
            if (started)
                return false;
            Result = new InteractionResult(
                pendingResult.OperationId,
                pendingResult.ActionId,
                GameplayInteractionState.Cancelled,
                "cancelled",
                reason,
                pendingResult.ContextId,
                0);
            State = Result.State;
            started = true;
            Completed?.Invoke(Result);
            return true;
        }
    }

    public sealed class ValidationGameplayInteractionHandler :
        IGameplayInteractionHandler
    {
        private readonly string outcomeId;
        private readonly bool completeImmediately;

        public string ActionId { get; }
        public InteractionParameterSchema Schema =>
            InteractionParameterSchema.Empty;
        public IGameplayInteractionOperation LastOperation { get; private set; }

        public ValidationGameplayInteractionHandler(
            string actionId,
            string outcomeId)
            : this(actionId, outcomeId, true)
        {
        }

        public ValidationGameplayInteractionHandler(
            string actionId,
            string outcomeId,
            bool completeImmediately)
        {
            ActionId = actionId ?? string.Empty;
            this.outcomeId = outcomeId ?? string.Empty;
            this.completeImmediately = completeImmediately;
        }

        public InteractionValidationResult Validate(
            GameplayInteractionRequest request,
            GameplayInteractionContext context) =>
            string.IsNullOrWhiteSpace(ActionId)
                ? new InteractionValidationResult(false, "InvalidActionId")
                : InteractionValidationResult.Valid;

        public IGameplayInteractionOperation Begin(
            GameplayInteractionRequest request,
            GameplayInteractionContext context)
        {
            var result = new InteractionResult(
                request.OperationId,
                request.ActionId,
                GameplayInteractionState.Completed,
                outcomeId,
                string.Empty,
                request.ContextId);
            LastOperation = completeImmediately
                ? new ImmediateGameplayInteractionOperation(result)
                : new DeferredGameplayInteractionOperation(result);
            return LastOperation;
        }
    }

    public sealed class DeferredGameplayInteractionOperation :
        IGameplayInteractionOperation
    {
        private readonly InteractionResult pendingResult;
        private bool started;

        public DeferredGameplayInteractionOperation(
            InteractionResult pendingResult)
        {
            this.pendingResult = pendingResult ??
                throw new ArgumentNullException(nameof(pendingResult));
        }

        public string OperationId => pendingResult.OperationId;
        public GameplayInteractionState State { get; private set; } =
            GameplayInteractionState.Running;
        public bool CanCancel => started &&
            State == GameplayInteractionState.Running;
        public InteractionResult Result { get; private set; }
        public event Action<GameplayInteractionProgress> Progressed
        {
            add { }
            remove { }
        }
        public event Action<InteractionResult> Completed;

        public void Start()
        {
            started = true;
        }

        public void Complete()
        {
            if (!started || State != GameplayInteractionState.Running)
                return;
            Result = pendingResult;
            State = Result.State;
            Completed?.Invoke(Result);
        }

        public bool TryCancel(string reason)
        {
            if (!CanCancel)
                return false;
            Result = new InteractionResult(
                pendingResult.OperationId,
                pendingResult.ActionId,
                GameplayInteractionState.Cancelled,
                "cancelled",
                reason,
                pendingResult.ContextId,
                0);
            State = Result.State;
            Completed?.Invoke(Result);
            return true;
        }
    }
}
