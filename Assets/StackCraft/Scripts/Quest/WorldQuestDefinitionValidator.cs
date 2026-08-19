using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public sealed class WorldQuestValidationReport
    {
        private readonly List<string> errors = new();
        private readonly List<string> warnings = new();

        public IList Errors => errors;
        public IList Warnings => warnings;
        public bool IsValid => errors.Count == 0;

        public void AddError(string message) => errors.Add(message);
        public void AddWarning(string message) => warnings.Add(message);
    }

    public static class WorldQuestDefinitionValidator
    {
        private static readonly Regex IdPattern = new(
            "^[a-z0-9]+(?:_[a-z0-9]+)*$",
            RegexOptions.CultureInvariant);

        public static WorldQuestValidationReport Validate(
            UnityEngine.Object[] objects)
        {
            return Validate(objects?.OfType<WorldQuestDefinition>());
        }

        public static WorldQuestValidationReport Validate(
            IEnumerable<WorldQuestDefinition> definitions)
        {
            var report = new WorldQuestValidationReport();
            List<WorldQuestDefinition> values = definitions?
                .Where(value => value != null)
                .ToList() ?? new List<WorldQuestDefinition>();
            if (values.Count > 500)
                report.AddError("V2 任务定义数量超过500条。");

            foreach (IGrouping<string, WorldQuestDefinition> duplicate in
                     values.GroupBy(value => value.Id, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                report.AddError($"QuestId 重复：{duplicate.Key}");
            }

            var knownIds = new HashSet<string>(
                values.Select(value => value.Id),
                StringComparer.Ordinal);
            var definitionsById = values
                .GroupBy(value => value.Id, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
            ISet<string> reasonIds = ValidateReasonCatalog(report);
            foreach (WorldQuestDefinition definition in values)
            {
                ValidateDefinition(
                    definition,
                    knownIds,
                    definitionsById,
                    reasonIds,
                    report);
            }
            ValidateDependencyGraph(values, report);
            ValidateWorldFactTypes(values, report);
            return report;
        }

        private static void ValidateDefinition(
            WorldQuestDefinition definition,
            ISet<string> knownQuestIds,
            IReadOnlyDictionary<string, WorldQuestDefinition> definitions,
            ISet<string> reasonIds,
            WorldQuestValidationReport report)
        {
            string prefix = $"[{definition.name}/{definition.Id}] ";
            if (definition.SchemaVersion != 2)
                report.AddError(prefix + "schemaVersion 必须为2。");
            if (!IsValidId(definition.Id))
                report.AddError(prefix + "QuestId 格式非法。");
            if (string.IsNullOrWhiteSpace(definition.Title))
                report.AddError(prefix + "标题不能为空。");
            if (definition.Stages == null || definition.Stages.Count == 0)
            {
                report.AddError(prefix + "至少需要一个阶段。");
                return;
            }
            if (definition.Stages.Count > 32)
                report.AddError(prefix + "阶段数量超过32。");
            if (definition.FindStage(definition.EntryStageId) == null)
                report.AddError(prefix + "入口阶段不存在。");
            if (definition.StartMode == WorldQuestStartMode.ManualAccept &&
                string.IsNullOrWhiteSpace(definition.GiverNpcId))
            {
                report.AddError(prefix + "手动任务缺少发布 NPC。");
            }
            if (definition.TurnInMode == WorldQuestTurnInMode.ManualNpc &&
                (definition.TurnInNpcIds == null ||
                 definition.TurnInNpcIds.Count == 0))
            {
                report.AddError(prefix + "手动任务缺少交付 NPC。");
            }

            ValidateUniqueIds(
                definition.Stages.Select(stage => stage?.StageId),
                "StageId",
                prefix,
                report);
            var objectiveIds = new List<string>();
            var effectIds = new List<string>();
            var transitionIds = new List<string>();
            var conditionIds = new List<string>();
            effectIds.AddRange(definition.OnAcceptEffects
                .Where(effect => effect != null)
                .Select(effect => effect.EffectId));
            foreach (WorldQuestStageDefinition stage in definition.Stages)
            {
                if (stage == null)
                {
                    report.AddError(prefix + "阶段列表包含空项。");
                    continue;
                }
                string stagePrefix = prefix + $"[{stage.StageId}] ";
                if (!IsValidId(stage.StageId))
                    report.AddError(stagePrefix + "StageId 格式非法。");
                if (stage.Objectives == null || stage.Objectives.Count == 0)
                    report.AddError(stagePrefix + "阶段必须有目标。");
                if (stage.Objectives.Count > 16)
                    report.AddError(stagePrefix + "目标数量超过16。");
                if (!stage.Objectives.Any(objective =>
                        objective != null && !objective.Optional))
                {
                    report.AddError(stagePrefix +
                        "至少需要一个非可选目标。");
                }
                foreach (WorldQuestObjectiveDefinition objective in
                         stage.Objectives.Where(value => value != null))
                {
                    objectiveIds.Add(objective.ObjectiveId);
                    if (!IsValidId(objective.ObjectiveId))
                        report.AddError(stagePrefix + "ObjectiveId 格式非法。");
                    if (objective.RequiredAmount <= 0)
                        report.AddError(stagePrefix + "目标数量必须大于0。");
                    ValidateObjective(objective, stagePrefix, report);
                }
                ValidateEffectListCapacity(
                    stage.OnEnterEffects,
                    stagePrefix + "进入效果",
                    report);
                ValidateEffectListCapacity(
                    stage.OnCompleteEffects,
                    stagePrefix + "完成效果",
                    report);
                effectIds.AddRange(stage.OnEnterEffects
                    .Where(effect => effect != null)
                    .Select(effect => effect.EffectId));
                effectIds.AddRange(stage.OnCompleteEffects
                    .Where(effect => effect != null)
                    .Select(effect => effect.EffectId));
                if (stage.Transitions.Count > 16)
                    report.AddError(stagePrefix + "转移数量超过16。");
                foreach (WorldQuestStageTransitionDefinition transition in
                         stage.Transitions.Where(value => value != null))
                {
                    transitionIds.Add(transition.TransitionId);
                    ValidateConditionSet(
                        transition.Conditions,
                        conditionIds,
                        stagePrefix + "阶段转移",
                        report);
                    if (!IsValidId(transition.TransitionId))
                    {
                        report.AddError(stagePrefix +
                            "TransitionId 格式非法。");
                    }
                    if (definition.FindStage(transition.TargetStageId) == null)
                    {
                        report.AddError(stagePrefix +
                            $"转移目标不存在：{transition.TargetStageId}");
                    }
                }
            }
            ValidateUniqueIds(
                objectiveIds,
                "ObjectiveId",
                prefix,
                report);
            ValidateUniqueIds(
                transitionIds,
                "TransitionId",
                prefix,
                report);
            ValidateConditionSet(
                definition.HardStartConditions,
                conditionIds,
                prefix + "硬开始条件",
                report);
            ValidateConditionSet(
                definition.AvailabilityConditions,
                conditionIds,
                prefix + "可用条件",
                report);
            ValidateConditionSet(
                definition.AcceptConditions,
                conditionIds,
                prefix + "接取条件",
                report);
            ValidateEffectListCapacity(
                definition.OnAcceptEffects,
                prefix + "接取效果",
                report);

            if (definition.Outcomes == null || definition.Outcomes.Count == 0)
                report.AddError(prefix + "至少需要一个结局。");
            if (definition.Outcomes.Count > 8)
                report.AddError(prefix + "结局数量超过8。");
            ValidateUniqueIds(
                definition.Outcomes
                    .Where(outcome => outcome != null)
                    .Select(outcome => outcome.OutcomeId),
                "OutcomeId",
                prefix,
                report);
            if (definition.Outcomes.Count(outcome =>
                    outcome != null && outcome.IsDefault) != 1)
            {
                report.AddError(prefix + "必须恰好有一个默认结局。");
            }
            foreach (WorldQuestOutcomeDefinition outcome in
                     definition.Outcomes.Where(value => value != null))
            {
                if (!IsValidId(outcome.OutcomeId))
                    report.AddError(prefix + "OutcomeId 格式非法。");
                ValidateConditionSet(
                    outcome.SelectionConditions,
                    conditionIds,
                    prefix + $"结局 {outcome.OutcomeId} 选择条件",
                    report);
                ValidateConditionSet(
                    outcome.TurnInConditions,
                    conditionIds,
                    prefix + $"结局 {outcome.OutcomeId} 交付条件",
                    report);
                ValidateEffectListCapacity(
                    outcome.Effects,
                    prefix + $"结局 {outcome.OutcomeId} 效果",
                    report);
                effectIds.AddRange(outcome.Effects
                    .Where(effect => effect != null)
                    .Select(effect => effect.EffectId));
            }
            ValidateUniqueIds(
                conditionIds,
                "ConditionId",
                prefix,
                report);
            if (definition.TurnInMode ==
                    WorldQuestTurnInMode.AutoOnFinalStage &&
                definition.Outcomes
                    .Where(value => value != null)
                    .GroupBy(value => value.Priority)
                    .OrderByDescending(group => group.Key)
                    .FirstOrDefault()?.Count() > 1)
            {
                report.AddError(prefix +
                    "自动结算任务存在并列最高优先级结局。");
            }
            ValidateUniqueIds(effectIds, "EffectId", prefix, report);
            ValidateEffects(
                definition,
                effectIds,
                knownQuestIds,
                definitions,
                reasonIds,
                prefix,
                report);
            ValidateGraph(definition, prefix, report);
        }

        private static void ValidateEffects(
            WorldQuestDefinition definition,
            IReadOnlyCollection<string> effectIds,
            ISet<string> knownQuestIds,
            IReadOnlyDictionary<string, WorldQuestDefinition> definitions,
            ISet<string> reasonIds,
            string prefix,
            WorldQuestValidationReport report)
        {
            IEnumerable<WorldQuestEffectDefinition> effects =
                definition.OnAcceptEffects
                    .Concat(definition.Stages.SelectMany(stage =>
                        stage.OnEnterEffects.Concat(stage.OnCompleteEffects)))
                    .Concat(definition.Outcomes.SelectMany(outcome =>
                        outcome.Effects))
                    .Where(effect => effect != null);
            foreach (WorldQuestEffectDefinition effect in effects)
            {
                if (!IsValidId(effect.EffectId))
                    report.AddError(prefix + "EffectId 格式非法。");
                if ((effect.Type is WorldQuestEffectType.MakeQuestAvailable or
                    WorldQuestEffectType.ActivateQuest or
                    WorldQuestEffectType.SuspendQuest or
                    WorldQuestEffectType.ResumeQuest or
                    WorldQuestEffectType.FailQuest or
                    WorldQuestEffectType.CancelQuest or
                    WorldQuestEffectType.TrackQuest) &&
                    !knownQuestIds.Contains(effect.TargetId))
                {
                    report.AddError(prefix +
                        $"效果引用未知任务：{effect.TargetId}");
                }
                if ((effect.Type is WorldQuestEffectType.SuspendQuest or
                    WorldQuestEffectType.FailQuest or
                    WorldQuestEffectType.CancelQuest) &&
                    string.IsNullOrWhiteSpace(effect.ReasonId))
                {
                    report.AddError(prefix +
                        $"效果 {effect.EffectId} 缺少 reasonId。");
                }
                else if ((effect.Type is
                        WorldQuestEffectType.SuspendQuest or
                        WorldQuestEffectType.FailQuest or
                        WorldQuestEffectType.CancelQuest) &&
                    !reasonIds.Contains(effect.ReasonId))
                {
                    report.AddError(prefix +
                        $"效果 {effect.EffectId} 引用未知 reasonId：" +
                        effect.ReasonId);
                }
                if (effect.Type == WorldQuestEffectType.ActivateQuest &&
                    !string.IsNullOrWhiteSpace(effect.SecondaryTargetId) &&
                    definitions.TryGetValue(
                        effect.TargetId,
                        out WorldQuestDefinition target))
                {
                    if (target.FindStage(
                            effect.SecondaryTargetId) == null)
                    {
                        report.AddError(prefix +
                            $"激活效果引用未知阶段：{effect.SecondaryTargetId}");
                    }
                }
                ValidateEffectFields(effect, prefix, report);
            }
        }

        private static void ValidateObjective(
            WorldQuestObjectiveDefinition objective,
            string prefix,
            WorldQuestValidationReport report)
        {
            if (objective.SerializedRequiredAmount <= 0)
            {
                report.AddError(prefix +
                    $"目标 {objective.ObjectiveId} 的 requiredAmount 必须大于0。");
            }
            bool needsTarget = objective.Type is not
                (WorldQuestObjectiveType.ProtagonistLevel or
                 WorldQuestObjectiveType.WorldDayReached);
            if (needsTarget ==
                string.IsNullOrWhiteSpace(objective.TargetId))
            {
                report.AddError(prefix +
                    $"目标 {objective.ObjectiveId} 的 targetId 配置错误。");
            }
            if (objective.Type == WorldQuestObjectiveType.DeliverCard &&
                string.IsNullOrWhiteSpace(objective.SecondaryTargetId))
            {
                report.AddError(prefix +
                    $"交付目标 {objective.ObjectiveId} 缺少 NPC ID。");
            }
            if ((objective.Type is
                    WorldQuestObjectiveType.MarketPurchase or
                    WorldQuestObjectiveType.MarketSale) &&
                string.IsNullOrWhiteSpace(objective.ContextId))
            {
                report.AddError(prefix +
                    $"市场目标 {objective.ObjectiveId} 缺少 Market ID。");
            }
            if ((objective.Type is WorldQuestObjectiveType.OwnCardCount or
                    WorldQuestObjectiveType.CurrencyBalance or
                    WorldQuestObjectiveType.ProtagonistLevel or
                    WorldQuestObjectiveType.WorldDayReached) &&
                objective.ProgressMode !=
                WorldQuestProgressMode.SetToCurrentSnapshot)
            {
                report.AddError(prefix +
                    $"快照目标 {objective.ObjectiveId} 的进度模式错误。");
            }
            if (objective.Type is not
                    (WorldQuestObjectiveType.OwnCardCount or
                     WorldQuestObjectiveType.CurrencyBalance or
                     WorldQuestObjectiveType.ProtagonistLevel or
                     WorldQuestObjectiveType.WorldDayReached) &&
                objective.ProgressMode ==
                    WorldQuestProgressMode.SetToCurrentSnapshot)
            {
                report.AddError(prefix +
                    $"事件目标 {objective.ObjectiveId} 不能使用快照进度模式。");
            }
            if (objective.Type == WorldQuestObjectiveType.DefeatCard &&
                objective.ActorRequirement !=
                WorldQuestActorRequirement.Protagonist)
            {
                report.AddWarning(prefix +
                    $"战斗目标 {objective.ObjectiveId} 未要求主角参与。");
            }
            if (objective.Type == WorldQuestObjectiveType.Interaction &&
                string.IsNullOrWhiteSpace(objective.TargetId))
            {
                report.AddError(prefix +
                    $"互动目标 {objective.ObjectiveId} 缺少 ActionId。");
            }
            if (string.IsNullOrWhiteSpace(objective.DisplayText))
                report.AddError(prefix + "目标显示文本不能为空。");
        }

        private static void ValidateConditionSet(
            WorldQuestConditionSet set,
            ICollection<string> conditionIds,
            string prefix,
            WorldQuestValidationReport report)
        {
            if (set?.Conditions == null)
                return;
            if (set.Conditions.Count > 32)
                report.AddError(prefix + "条件数量超过32。" );
            foreach (WorldQuestConditionDefinition condition in
                     set.Conditions.Where(value => value != null))
            {
                conditionIds.Add(condition.ConditionId);
                if (!IsValidId(condition.ConditionId))
                    report.AddError(prefix + " ConditionId 格式非法。");
                if ((condition.Type is WorldQuestConditionType.QuestStatus or
                        WorldQuestConditionType.QuestOutcome or
                        WorldQuestConditionType.WorldFactBool or
                        WorldQuestConditionType.WorldFactInt or
                        WorldQuestConditionType.WorldFactString or
                        WorldQuestConditionType.OwnedCardCount or
                        WorldQuestConditionType.CurrentLocation) &&
                    string.IsNullOrWhiteSpace(condition.TargetId))
                {
                    report.AddError(prefix +
                        $"条件 {condition.ConditionId} 缺少 targetId。");
                }
                bool equalityOnly = condition.Type is
                    WorldQuestConditionType.QuestStatus or
                    WorldQuestConditionType.QuestOutcome or
                    WorldQuestConditionType.WorldFactBool or
                    WorldQuestConditionType.WorldFactString or
                    WorldQuestConditionType.CurrentLocation or
                    WorldQuestConditionType.SelectedChoiceEquals;
                if (equalityOnly && condition.Comparison is not
                    (WorldQuestComparison.Equal or
                     WorldQuestComparison.NotEqual))
                {
                    report.AddError(prefix +
                        $"条件 {condition.ConditionId} 使用了非法比较符。");
                }
            }
        }

        private static void ValidateEffectListCapacity(
            IReadOnlyList<WorldQuestEffectDefinition> effects,
            string prefix,
            WorldQuestValidationReport report)
        {
            if (effects != null && effects.Count > 32)
                report.AddError(prefix + "数量超过32。" );
        }

        private static void ValidateEffectFields(
            WorldQuestEffectDefinition effect,
            string prefix,
            WorldQuestValidationReport report)
        {
            if (effect.Type == WorldQuestEffectType.GrantCard &&
                (string.IsNullOrWhiteSpace(effect.TargetId) ||
                 effect.IntValue <= 0))
            {
                report.AddError(prefix +
                    $"奖励效果 {effect.EffectId} 的卡牌或数量非法。");
            }
            if (effect.Type ==
                    WorldQuestEffectType.GrantProtagonistExperience &&
                effect.IntValue <= 0)
            {
                report.AddError(prefix +
                    $"经验效果 {effect.EffectId} 的数值必须大于0。");
            }
            if (effect.Type == WorldQuestEffectType.ShowNotification &&
                string.IsNullOrWhiteSpace(effect.Notification))
            {
                report.AddError(prefix +
                    $"通知效果 {effect.EffectId} 缺少文本。");
            }
            if ((effect.Type is WorldQuestEffectType.SetWorldFactBool or
                    WorldQuestEffectType.SetWorldFactInt or
                    WorldQuestEffectType.SetWorldFactString or
                    WorldQuestEffectType.IncrementWorldFactInt) &&
                string.IsNullOrWhiteSpace(effect.TargetId))
            {
                report.AddError(prefix +
                    $"世界事实效果 {effect.EffectId} 缺少 fact key。");
            }
            if (effect.Type == WorldQuestEffectType.IncrementWorldFactInt &&
                effect.IntValue == 0)
            {
                report.AddError(prefix +
                    $"世界事实增量效果 {effect.EffectId} 不能增加0。");
            }
        }

        private static ISet<string> ValidateReasonCatalog(
            WorldQuestValidationReport report)
        {
            WorldQuestReasonCatalog catalog =
                Resources.Load<WorldQuestReasonCatalog>(
                    "WorldQuests/WorldQuestReasons");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (catalog == null)
            {
                report.AddError("缺少 WorldQuestReasons 原因目录。");
                return ids;
            }
            foreach (WorldQuestReasonDefinition reason in catalog.Reasons)
            {
                if (reason == null || !IsValidId(reason.ReasonId))
                {
                    report.AddError("原因目录包含非法 ReasonId。");
                    continue;
                }
                if (!ids.Add(reason.ReasonId))
                    report.AddError($"ReasonId 重复：{reason.ReasonId}");
                if (string.IsNullOrWhiteSpace(reason.DisplayText))
                {
                    report.AddError(
                        $"ReasonId {reason.ReasonId} 缺少中文显示文本。");
                }
            }
            return ids;
        }

        private static void ValidateDependencyGraph(
            IReadOnlyList<WorldQuestDefinition> definitions,
            WorldQuestValidationReport report)
        {
            List<WorldQuestDefinition> uniqueDefinitions = definitions
                .GroupBy(value => value.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            var graph = uniqueDefinitions.ToDictionary(
                value => value.Id,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);
            foreach (WorldQuestDefinition definition in uniqueDefinitions)
            {
                foreach (WorldQuestConditionDefinition condition in
                         EnumerateConditions(definition))
                {
                    if ((condition.Type is
                            WorldQuestConditionType.QuestStatus or
                            WorldQuestConditionType.QuestOutcome) &&
                        graph.ContainsKey(condition.TargetId))
                    {
                        graph[definition.Id].Add(condition.TargetId);
                    }
                }
                foreach (WorldQuestEffectDefinition effect in
                         EnumerateEffects(definition))
                {
                    if ((effect.Type is
                            WorldQuestEffectType.MakeQuestAvailable or
                            WorldQuestEffectType.ActivateQuest or
                            WorldQuestEffectType.SuspendQuest or
                            WorldQuestEffectType.ResumeQuest or
                            WorldQuestEffectType.FailQuest or
                            WorldQuestEffectType.CancelQuest) &&
                        graph.ContainsKey(effect.TargetId))
                    {
                        graph[definition.Id].Add(effect.TargetId);
                    }
                }
            }

            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            bool Visit(string id)
            {
                if (!visiting.Add(id))
                    return false;
                if (!visited.Add(id))
                {
                    visiting.Remove(id);
                    return true;
                }
                foreach (string target in graph[id])
                {
                    if (!Visit(target))
                        return false;
                }
                visiting.Remove(id);
                return true;
            }

            foreach (string id in graph.Keys.OrderBy(value => value))
            {
                if (!Visit(id))
                {
                    report.AddError($"任务静态依赖图存在循环，涉及：{id}");
                    break;
                }
            }
        }

        private static void ValidateWorldFactTypes(
            IReadOnlyList<WorldQuestDefinition> definitions,
            WorldQuestValidationReport report)
        {
            var types = new Dictionary<string, WorldFactValueType>(
                StringComparer.Ordinal);
            void Register(string key, WorldFactValueType type)
            {
                if (string.IsNullOrWhiteSpace(key))
                    return;
                if (types.TryGetValue(key, out var existing) &&
                    existing != type)
                {
                    report.AddError(
                        $"世界事实 {key} 被声明为不同类型。");
                }
                else
                {
                    types[key] = type;
                }
            }

            foreach (WorldQuestEffectDefinition effect in definitions
                         .SelectMany(EnumerateEffects))
            {
                WorldFactValueType? type = effect.Type switch
                {
                    WorldQuestEffectType.SetWorldFactBool =>
                        WorldFactValueType.Bool,
                    WorldQuestEffectType.SetWorldFactInt =>
                        WorldFactValueType.Int,
                    WorldQuestEffectType.IncrementWorldFactInt =>
                        WorldFactValueType.Int,
                    WorldQuestEffectType.SetWorldFactString =>
                        WorldFactValueType.String,
                    _ => null
                };
                if (!type.HasValue)
                    continue;
                Register(effect.TargetId, type.Value);
            }

            foreach (WorldQuestConditionDefinition condition in definitions
                         .SelectMany(EnumerateConditions))
            {
                WorldFactValueType? type = condition.Type switch
                {
                    WorldQuestConditionType.WorldFactBool =>
                        WorldFactValueType.Bool,
                    WorldQuestConditionType.WorldFactInt =>
                        WorldFactValueType.Int,
                    WorldQuestConditionType.WorldFactString =>
                        WorldFactValueType.String,
                    _ => null
                };
                if (type.HasValue)
                    Register(condition.TargetId, type.Value);
            }
        }

        private static IEnumerable<WorldQuestConditionDefinition>
            EnumerateConditions(WorldQuestDefinition definition)
        {
            IEnumerable<WorldQuestConditionSet> sets = new[]
                {
                    definition.HardStartConditions,
                    definition.AvailabilityConditions,
                    definition.AcceptConditions
                }
                .Concat(definition.Stages.SelectMany(stage =>
                    stage.Transitions.Select(transition =>
                        transition.Conditions)))
                .Concat(definition.Outcomes.SelectMany(outcome => new[]
                {
                    outcome.SelectionConditions,
                    outcome.TurnInConditions
                }));
            return sets
                .Where(set => set?.Conditions != null)
                .SelectMany(set => set.Conditions)
                .Where(value => value != null);
        }

        private static IEnumerable<WorldQuestEffectDefinition>
            EnumerateEffects(WorldQuestDefinition definition)
        {
            return definition.OnAcceptEffects
                .Concat(definition.Stages.SelectMany(stage =>
                    stage.OnEnterEffects.Concat(stage.OnCompleteEffects)))
                .Concat(definition.Outcomes.SelectMany(outcome =>
                    outcome.Effects))
                .Where(effect => effect != null);
        }

        private static void ValidateGraph(
            WorldQuestDefinition definition,
            string prefix,
            WorldQuestValidationReport report)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            bool Visit(string stageId)
            {
                if (!visiting.Add(stageId))
                    return false;
                if (!visited.Add(stageId))
                {
                    visiting.Remove(stageId);
                    return true;
                }
                WorldQuestStageDefinition stage = definition.FindStage(stageId);
                if (stage != null)
                {
                    foreach (WorldQuestStageTransitionDefinition transition in
                             stage.Transitions.Where(value => value != null))
                    {
                        if (!Visit(transition.TargetStageId))
                            return false;
                    }
                }
                visiting.Remove(stageId);
                return true;
            }

            if (!Visit(definition.EntryStageId))
                report.AddError(prefix + "阶段图存在循环。");
            foreach (WorldQuestStageDefinition stage in definition.Stages)
            {
                if (stage != null && !visited.Contains(stage.StageId))
                    report.AddError(prefix + $"阶段不可达：{stage.StageId}");
            }
        }

        private static void ValidateUniqueIds(
            IEnumerable<string> ids,
            string label,
            string prefix,
            WorldQuestValidationReport report)
        {
            foreach (IGrouping<string, string> duplicate in ids
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .GroupBy(value => value, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                report.AddError(prefix + $"{label} 重复：{duplicate.Key}");
            }
        }

        private static bool IsValidId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   IdPattern.IsMatch(value);
        }
    }
}
