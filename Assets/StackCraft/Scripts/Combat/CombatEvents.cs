using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public enum CombatOutcomeResult
    {
        Victory = 0,
        Defeat = 1,
        Retreated = 2,
        Aborted = 3
    }

    public static class CombatOutcomeRules
    {
        public static CombatOutcomeResult Resolve(
            bool playerAlive,
            bool mobAlive,
            bool anyPlayerRetreated,
            bool forceAbort)
        {
            if (forceAbort)
                return CombatOutcomeResult.Aborted;
            if (!playerAlive && anyPlayerRetreated)
                return CombatOutcomeResult.Retreated;
            if (!playerAlive)
                return CombatOutcomeResult.Defeat;
            if (!mobAlive)
                return CombatOutcomeResult.Victory;
            return CombatOutcomeResult.Aborted;
        }
    }

    [Serializable]
    public sealed class CombatOutcome
    {
        public string OriginalSessionId { get; }
        public string FinalSessionId { get; }
        public CombatOutcomeResult Result { get; }
        public string EndReason { get; }
        public IReadOnlyList<string> SurvivingActorIds { get; }
        public IReadOnlyList<string> DefeatedActorIds { get; }

        public CombatOutcome(
            string originalSessionId,
            string finalSessionId,
            CombatOutcomeResult result,
            string endReason,
            IEnumerable<string> survivingActorIds,
            IEnumerable<string> defeatedActorIds)
        {
            OriginalSessionId = originalSessionId ?? string.Empty;
            FinalSessionId = finalSessionId ?? string.Empty;
            Result = result;
            EndReason = endReason ?? string.Empty;
            SurvivingActorIds = (survivingActorIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            DefeatedActorIds = (defeatedActorIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }

    /// <summary>
    /// Keeps narrative-owned session aliases stable when combat rectangles merge.
    /// </summary>
    public sealed class CombatSessionOutcomeTracker
    {
        private readonly Dictionary<string, string> finalByOriginal =
            new(StringComparer.Ordinal);

        public void Register(string originalSessionId, string finalSessionId)
        {
            if (string.IsNullOrWhiteSpace(originalSessionId) ||
                string.IsNullOrWhiteSpace(finalSessionId))
                return;
            finalByOriginal[originalSessionId] = finalSessionId;
        }

        public void RemapFinalSession(string previousFinalSessionId,
            string newFinalSessionId)
        {
            if (string.IsNullOrWhiteSpace(previousFinalSessionId) ||
                string.IsNullOrWhiteSpace(newFinalSessionId))
                return;
            foreach (string original in finalByOriginal
                         .Where(pair => pair.Value == previousFinalSessionId)
                         .Select(pair => pair.Key).ToArray())
            {
                finalByOriginal[original] = newFinalSessionId;
            }
            finalByOriginal[previousFinalSessionId] = newFinalSessionId;
        }

        public string ResolveFinalSession(string originalSessionId) =>
            !string.IsNullOrWhiteSpace(originalSessionId) &&
            finalByOriginal.TryGetValue(originalSessionId, out string value)
                ? value
                : originalSessionId ?? string.Empty;

        public IEnumerable<string> OriginalsForFinal(string finalSessionId) =>
            finalByOriginal
                .Where(pair => pair.Value == finalSessionId)
                .Select(pair => pair.Key)
                .Append(finalSessionId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal);

        public CombatOutcome CreateOutcome(
            string originalSessionId,
            CombatOutcomeResult result,
            string endReason,
            IEnumerable<string> survivingActorIds,
            IEnumerable<string> defeatedActorIds) =>
            new(
                originalSessionId,
                ResolveFinalSession(originalSessionId),
                result,
                endReason,
                survivingActorIds,
                defeatedActorIds);
    }

    public enum CombatEventType
    {
        CombatStarted,
        CombatRestored,
        CombatMerged,
        CombatantJoined,
        ActionQueued,
        ActionCancelled,
        ActionStarted,
        AttackMissed,
        DamageApplied,
        CriticalHit,
        HealingApplied,
        SkillUsed,
        ItemUsed,
        ActionCompleted,
        RetreatAttempted,
        RetreatSucceeded,
        RetreatFailed,
        CombatantDowned,
        CombatantDefeated,
        ExperienceGranted,
        CombatEnded
    }

    [Serializable]
    public sealed class CombatEvent
    {
        public long Sequence { get; set; }
        public string SessionId { get; set; }
        public CombatEventType Type { get; set; }
        public string SourceId { get; set; }
        public string SourceName { get; set; }
        public string TargetId { get; set; }
        public string TargetName { get; set; }
        public string DefinitionId { get; set; }
        public int Value { get; set; }
        public int SecondaryValue { get; set; }
        public HitType HitType { get; set; }
        public CombatTypeAdvantage Advantage { get; set; }

        // Non-persistent presentation metadata and version-1 compatibility.
        public float Timestamp { get; set; }
        public string Detail { get; set; }
        public string ActorId { get => SourceId; set => SourceId = value; }
        public string ActorName { get => SourceName; set => SourceName = value; }
        public string SkillId { get => DefinitionId; set => DefinitionId = value; }
        public string ItemId { get => DefinitionId; set => DefinitionId = value; }
        public int Amount { get => Value; set => Value = value; }
        public float Chance
        {
            get => SecondaryValue / 10000f;
            set => SecondaryValue = (int)Math.Round(value * 10000f);
        }
    }

    public sealed class CombatEventBuffer
    {
        public const int Capacity = 100;
        private readonly List<CombatEvent> events = new(Capacity);

        public IReadOnlyList<CombatEvent> Events => events;

        public void Add(CombatEvent combatEvent)
        {
            if (combatEvent == null)
                return;
            events.Add(combatEvent);
            if (events.Count > Capacity)
                events.RemoveRange(0, events.Count - Capacity);
        }

        public void Clear() => events.Clear();
    }

    public static class CombatEventFormatter
    {
        public static string Format(CombatEvent value)
        {
            if (value == null)
                return string.Empty;

            string source = string.IsNullOrWhiteSpace(value.SourceName)
                ? "未知单位"
                : value.SourceName;
            string target = string.IsNullOrWhiteSpace(value.TargetName)
                ? "目标"
                : value.TargetName;
            return value.Type switch
            {
                CombatEventType.CombatStarted => "战斗开始。",
                CombatEventType.CombatRestored => "战斗已恢复。",
                CombatEventType.ActionQueued => $"{source} 已准备行动。",
                CombatEventType.AttackMissed => $"{source} 的攻击被 {target} 躲开了。",
                CombatEventType.CriticalHit => string.Empty,
                CombatEventType.DamageApplied when value.HitType == HitType.Critical =>
                    $"{source} 对 {target} 造成 {value.Value} 点暴击伤害。",
                CombatEventType.DamageApplied =>
                    $"{source} 对 {target} 造成 {value.Value} 点伤害。",
                CombatEventType.HealingApplied =>
                    $"{source} 为 {target} 恢复 {value.Value} 点生命。",
                CombatEventType.SkillUsed => $"{source} 使用了 {value.Detail}。",
                CombatEventType.ItemUsed => $"{source} 使用了 {value.Detail}。",
                CombatEventType.RetreatAttempted => $"{source} 尝试撤退。",
                CombatEventType.RetreatSucceeded => $"{source} 成功脱离战斗。",
                CombatEventType.RetreatFailed => $"{source} 撤退失败，敌人立即反击！",
                CombatEventType.CombatantDefeated => $"{target} 被击败。",
                CombatEventType.ExperienceGranted => $"获得 {value.Value} 点经验。",
                CombatEventType.CombatEnded => "战斗结束。",
                _ => string.Empty
            };
        }
    }
}
