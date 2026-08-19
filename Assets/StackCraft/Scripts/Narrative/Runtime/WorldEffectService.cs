using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public sealed class WorldEffectRequest
    {
        public WorldEffectRequest(
            NarrativeEffectType effectType,
            string resultId,
            string targetId,
            string stringValue,
            int intValue,
            bool boolValue)
            : this(
                effectType,
                resultId,
                targetId,
                string.Empty,
                stringValue,
                intValue,
                boolValue)
        {
        }

        public WorldEffectRequest(
            NarrativeEffectType effectType,
            string resultId,
            string targetId,
            string secondaryTargetId,
            string stringValue,
            int intValue,
            bool boolValue)
        {
            EffectType = effectType;
            ResultId = resultId;
            TargetId = targetId;
            SecondaryTargetId = secondaryTargetId;
            StringValue = stringValue;
            IntValue = intValue;
            BoolValue = boolValue;
        }

        public NarrativeEffectType EffectType { get; }
        public string ResultId { get; }
        public string TargetId { get; }
        public string SecondaryTargetId { get; }
        public string StringValue { get; }
        public int IntValue { get; }
        public bool BoolValue { get; }
    }

    public sealed class WorldEffectResult
    {
        private WorldEffectResult(
            bool success,
            bool applied,
            bool alreadyApplied,
            string error)
        {
            Success = success;
            Applied = applied;
            AlreadyApplied = alreadyApplied;
            Error = error;
        }

        public bool Success { get; }
        public bool Applied { get; }
        public bool AlreadyApplied { get; }
        public string Error { get; }

        public static WorldEffectResult AppliedNow() =>
            new(true, true, false, string.Empty);

        public static WorldEffectResult Duplicate() =>
            new(true, false, true, string.Empty);

        public static WorldEffectResult Failed(string error) =>
            new(false, false, false, error ?? string.Empty);
    }

    /// <summary>
    /// Applies narrative-authored changes to persistent world state. Each
    /// result is committed at most once for a narrative run.
    /// </summary>
    public sealed class WorldEffectService
    {
        private readonly GameData gameData;
        private readonly IWorldQuestRuntime questRuntime;

        internal GameData GameData => gameData;

        public WorldEffectService(GameData gameData)
            : this(gameData, null)
        {
        }

        public WorldEffectService(
            GameData gameData,
            IWorldQuestRuntime questRuntime)
        {
            this.gameData = gameData ?? throw new ArgumentNullException(
                nameof(gameData));
            this.questRuntime = questRuntime;
        }

        public WorldEffectResult Apply(
            string narrativeId,
            int narrativeVersion,
            string runId,
            string nodeId,
            WorldEffectRequest request)
        {
            if (request == null)
                return WorldEffectResult.Failed("World effect request is null.");
            if (string.IsNullOrWhiteSpace(narrativeId) ||
                string.IsNullOrWhiteSpace(runId) ||
                string.IsNullOrWhiteSpace(nodeId) ||
                string.IsNullOrWhiteSpace(request.ResultId))
            {
                return WorldEffectResult.Failed(
                    "Narrative, run, node, and result identifiers are required.");
            }

            return ApplyCommitted(
                narrativeId,
                narrativeVersion,
                runId,
                nodeId,
                request.ResultId,
                commitId => ApplyCore(request, commitId));
        }

        public WorldEffectResult ApplyDamage(
            string narrativeId,
            int narrativeVersion,
            string runId,
            string nodeId,
            string resultId,
            CardInstance target,
            int damage)
        {
            if (target == null)
                return WorldEffectResult.Failed("Damage target is missing.");
            if (damage <= 0)
                return WorldEffectResult.Failed(
                    "Damage amount must be greater than zero.");

            return ApplyCommitted(
                narrativeId,
                narrativeVersion,
                runId,
                nodeId,
                resultId,
                _ =>
                {
                    target.TakeDamage(damage);
                    if (target != null && target.CurrentHealth <= 0)
                        target.Kill();
                    return WorldEffectResult.AppliedNow();
                });
        }

        private WorldEffectResult ApplyCommitted(
            string narrativeId,
            int narrativeVersion,
            string runId,
            string nodeId,
            string resultId,
            Func<string, WorldEffectResult> apply)
        {
            if (string.IsNullOrWhiteSpace(narrativeId) ||
                string.IsNullOrWhiteSpace(runId) ||
                string.IsNullOrWhiteSpace(nodeId) ||
                string.IsNullOrWhiteSpace(resultId))
            {
                return WorldEffectResult.Failed(
                    "Narrative, run, node, and result identifiers are required.");
            }

            NarrativeRunStateData run = EnsureRun(
                narrativeId, narrativeVersion, runId);
            string commitId = string.Join(":", narrativeId,
                Math.Max(1, narrativeVersion), runId, nodeId, resultId);
            gameData.Narrative.CommittedResultIds ??= new List<string>();
            if (gameData.Narrative.CommittedResultIds.Contains(commitId) ||
                run.CommittedResultIds.Contains(commitId))
                return WorldEffectResult.Duplicate();

            WorldEffectResult result = apply(commitId);
            if (result.Applied)
            {
                run.CommittedResultIds.Add(commitId);
                gameData.Narrative.CommittedResultIds.Add(commitId);
            }
            return result;
        }

        private WorldEffectResult ApplyCore(
            WorldEffectRequest request,
            string commitId)
        {
            switch (request.EffectType)
            {
                case NarrativeEffectType.SetWorldFactBool:
                    SetFact(request.TargetId, WorldFactValueType.Bool,
                        request.BoolValue, 0, string.Empty);
                    break;
                case NarrativeEffectType.SetWorldFactInt:
                    SetFact(request.TargetId, WorldFactValueType.Int,
                        false, request.IntValue, string.Empty);
                    break;
                case NarrativeEffectType.SetWorldFactString:
                    SetFact(request.TargetId, WorldFactValueType.String,
                        false, 0, request.StringValue);
                    break;
                case NarrativeEffectType.IncrementWorldFactInt:
                {
                    WorldFactData fact = GetOrCreateFact(request.TargetId);
                    fact.Type = WorldFactValueType.Int;
                    fact.IntValue += request.IntValue;
                    fact.Revision++;
                    break;
                }
                case NarrativeEffectType.StartQuest:
                {
                    if (questRuntime == null)
                        return WorldEffectResult.Failed(
                            "World quest runtime is unavailable.");
                    WorldQuestOperationResult result =
                        questRuntime.TryAccept(request.TargetId);
                    if (!result.Success)
                        return WorldEffectResult.Failed(result.Message);
                    break;
                }
                case NarrativeEffectType.ReportQuestEvent:
                {
                    if (questRuntime == null)
                        return WorldEffectResult.Failed(
                            "World quest runtime is unavailable.");
                    WorldQuestOperationResult result = questRuntime.ReportEvent(
                        new WorldQuestEvent(
                            commitId,
                            WorldQuestEventType.GameplayInteraction,
                            gameData.WorldElapsedHours,
                            gameData.ActiveLocationId ?? string.Empty,
                            gameData.ProtagonistPersistentId ?? string.Empty,
                            true,
                            true,
                            request.TargetId,
                            request.SecondaryTargetId,
                            request.StringValue,
                            Math.Max(1, request.IntValue),
                            WorldQuestTradeDirection.None,
                            GameplayInteractionPhase.Resolved));
                    if (!result.Success)
                        return WorldEffectResult.Failed(result.Message);
                    break;
                }
                case NarrativeEffectType.ApplyDamage:
                    return WorldEffectResult.Failed(
                        "ApplyDamage requires a resolved narrative actor.");
                case NarrativeEffectType.GiveCoins:
                {
                    if (request.IntValue <= 0)
                        return WorldEffectResult.Failed(
                            "Coin reward must be greater than zero.");
                    CardDefinition currency = CardManager.Instance?
                        .GetDefinitionById(request.TargetId) ??
                        Resources.LoadAll<CardDefinition>("Cards")
                            .FirstOrDefault(value => value != null &&
                                value.Id == request.TargetId &&
                                value.Category == CardCategory.Currency);
                    if (currency == null)
                        return WorldEffectResult.Failed(
                            "Coin definition is unavailable.");
                    if (!BackpackService.TryStoreGeneratedCards(
                            currency,
                            request.IntValue,
                            gameData.EnsureBackpack()))
                    {
                        return WorldEffectResult.Failed(
                            "Coin reward could not enter the backpack.");
                    }
                    break;
                }
                default:
                    return WorldEffectResult.Failed(
                        $"Unsupported world effect: {request.EffectType}.");
            }

            return WorldEffectResult.AppliedNow();
        }

        private NarrativeRunStateData EnsureRun(
            string narrativeId,
            int narrativeVersion,
            string runId)
        {
            gameData.Narrative ??= new NarrativeHistoryData();
            NarrativeRunStateData run = gameData.Narrative.ActiveRun;
            if (run == null ||
                !string.Equals(run.NarrativeId, narrativeId,
                    StringComparison.Ordinal) ||
                run.NarrativeVersion != Math.Max(1, narrativeVersion) ||
                !string.Equals(run.RunId, runId, StringComparison.Ordinal))
            {
                run = new NarrativeRunStateData
                {
                    NarrativeId = narrativeId,
                    NarrativeVersion = Math.Max(1, narrativeVersion),
                    RunId = runId,
                    CommittedResultIds = new List<string>()
                };
                gameData.Narrative.ActiveRun = run;
            }
            else
            {
                run.CommittedResultIds ??= new List<string>();
            }

            return run;
        }

        private void SetFact(
            string key,
            WorldFactValueType type,
            bool boolValue,
            int intValue,
            string stringValue)
        {
            WorldFactData fact = GetOrCreateFact(key);
            fact.Type = type;
            fact.BoolValue = boolValue;
            fact.IntValue = intValue;
            fact.StringValue = stringValue ?? string.Empty;
            fact.Revision++;
        }

        private WorldFactData GetOrCreateFact(string key)
        {
            gameData.WorldFacts ??= new List<WorldFactData>();
            WorldFactData fact = gameData.WorldFacts.FirstOrDefault(value =>
                value != null && string.Equals(value.Key, key,
                    StringComparison.Ordinal));
            if (fact != null)
                return fact;

            fact = new WorldFactData { Key = key };
            gameData.WorldFacts.Add(fact);
            return fact;
        }
    }
}
