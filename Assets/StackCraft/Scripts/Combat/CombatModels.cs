using System;
using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public enum CombatPhase
    {
        Starting,
        Running,
        ResolvingAction,
        Ending,
        Finished
    }

    public enum CombatCommandType
    {
        BasicAttack,
        UseSkill,
        UseItem,
        Retreat,
        ForcedAttack
    }

    public enum CombatCommandResultCode
    {
        Accepted,
        SessionNotFound,
        SessionNotRunning,
        ActorNotFound,
        ActorNotControllable,
        ActorUnavailable,
        TargetNotFound,
        InvalidTarget,
        SkillNotKnown,
        InsufficientEnergy,
        SkillOnCooldown,
        BackpackEntryNotFound,
        ItemNotCombatUsable,
        ItemAlreadyReserved,
        RetreatNotAllowed
    }

    public enum CombatTargetRule
    {
        SingleLivingEnemy,
        SingleLivingAlly
    }

    public enum CombatItemEffectType
    {
        Damage,
        Heal
    }

    [Serializable]
    public sealed class CombatCommand
    {
        [field: SerializeField]
        public string CommandId { get; set; } = Guid.NewGuid().ToString("N");
        [field: SerializeField]
        public string SessionId { get; set; }
        [field: SerializeField]
        public CombatCommandType Type { get; set; }
        [field: SerializeField]
        public string ActorId { get; set; }
        [field: SerializeField]
        public string TargetId { get; set; }
        [field: SerializeField]
        public string DefinitionId { get; set; }
        [field: SerializeField]
        public string BackpackEntryId { get; set; }
        [field: SerializeField]
        public long RequestedSequence { get; set; }

        // Compatibility aliases for version-1 saves. New code only uses the
        // document-defined members above.
        public string SkillId
        {
            get => DefinitionId;
            set => DefinitionId = value;
        }

        public long QueuedSequence
        {
            get => RequestedSequence;
            set => RequestedSequence = value;
        }
    }

    public readonly struct CombatCommandResult
    {
        public bool Accepted { get; }
        public CombatCommandResultCode Code { get; }
        public string PlayerMessage { get; }
        public string Message => PlayerMessage;

        public CombatCommandResult(
            bool accepted,
            CombatCommandResultCode code,
            string message = null)
        {
            Accepted = accepted;
            Code = code;
            PlayerMessage = message ?? string.Empty;
        }

        public static CombatCommandResult Success() =>
            new(true, CombatCommandResultCode.Accepted);

        public static CombatCommandResult Failure(
            CombatCommandResultCode code,
            string message) => new(false, code, message);
    }

    public sealed class CombatCommandQueue
    {
        private readonly Dictionary<string, CombatCommand> queued = new();

        public int Count => queued.Count;

        public void Queue(CombatCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.ActorId))
                throw new ArgumentException("Combat command requires an actor id.");

            queued[command.ActorId] = command;
        }

        public CombatCommand PeekForActor(string actorId)
        {
            queued.TryGetValue(actorId ?? string.Empty, out CombatCommand command);
            return command;
        }

        public CombatCommand TakeForActor(string actorId)
        {
            CombatCommand command = PeekForActor(actorId);
            if (command != null)
                queued.Remove(actorId);
            return command;
        }

        public bool CancelForActor(string actorId) =>
            !string.IsNullOrEmpty(actorId) && queued.Remove(actorId);

        public void Clear() => queued.Clear();

        public IEnumerable<CombatCommand> Commands => queued.Values;
    }

    public sealed class CombatActionPlan
    {
        public CombatCommand Command { get; set; }
        public HitResult Hit { get; set; }
        public int Healing { get; set; }
        public bool RetreatSucceeded { get; set; }
        public string FailureReason { get; set; }
        public bool IsValid => string.IsNullOrEmpty(FailureReason);
    }

    public readonly struct CombatActionResult
    {
        public string ActorId { get; }
        public string TargetId { get; }
        public CombatCommandType CommandType { get; }
        public HitResult Hit { get; }
        public int Healing { get; }
        public bool RetreatSucceeded { get; }

        public CombatActionResult(
            string actorId,
            string targetId,
            CombatCommandType commandType,
            HitResult hit,
            int healing = 0,
            bool retreatSucceeded = false)
        {
            ActorId = actorId;
            TargetId = targetId;
            CommandType = commandType;
            Hit = hit;
            Healing = healing;
            RetreatSucceeded = retreatSucceeded;
        }
    }

}
