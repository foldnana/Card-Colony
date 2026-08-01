using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CryingSnow.StackCraft
{
    public readonly struct WorldQuestChangedEvent
    {
        public string QuestId { get; }
        public WorldQuestStateData Previous { get; }
        public WorldQuestStateData Current { get; }

        public WorldQuestChangedEvent(
            string questId,
            WorldQuestStateData previous,
            WorldQuestStateData current)
        {
            QuestId = questId;
            Previous = previous;
            Current = current;
        }
    }

    public sealed class WorldQuestEngine
    {
        private const int MaximumActiveQuests = 50;
        private const int MaximumProcessedEventIds = 2048;
        private const int MaximumCascadeOperations = 512;
        private readonly GameData gameData;
        private readonly WorldQuestRegistry registry;
        private readonly List<string> pendingNotifications = new();
        private bool initialized;
        private bool transactionActive;
        private int cascadeOperations;

        public event Action<WorldQuestChangedEvent> QuestChanged;
        public event Action<string> NotificationRequested;

        public WorldQuestEngine(
            GameData gameData,
            IEnumerable<WorldQuestDefinition> definitions)
        {
            this.gameData = gameData ??
                throw new ArgumentNullException(nameof(gameData));
            registry = new WorldQuestRegistry(definitions);
        }

        public WorldQuestEngine(
            GameData gameData,
            UnityEngine.Object[] definitions)
            : this(
                gameData,
                definitions?.OfType<WorldQuestDefinition>())
        {
        }

        public WorldQuestRegistry Registry => registry;

        public void Initialize()
        {
            if (initialized)
                return;

            if (gameData.WorldQuestStateVersion >
                GameData.CurrentWorldQuestStateVersion)
            {
                throw new NotSupportedException(
                    "存档由更新版本创建，当前客户端不能降级读取。");
            }

            NormalizeCollections();
            GameDataSnapshot snapshot = GameDataSnapshot.Capture(gameData);
            try
            {
                bool isLegacy = gameData.WorldQuestStateVersion <
                    GameData.CurrentWorldQuestStateVersion;
                if (isLegacy)
                    MergeDuplicateLegacyStates();
                MigrateV1States();
                if (!isLegacy)
                    RejectDuplicateStates();

                foreach (WorldQuestDefinition definition in
                         OrderedDefinitions())
                {
                    EnsureState(definition.Id);
                }

                ReevaluateAvailabilityInternal();
                ActivateAutomaticQuests();
                NormalizeTrackedQuest();
                ValidateStateInvariants();
                initialized = true;
            }
            catch
            {
                snapshot.Restore(gameData);
                throw;
            }
        }

        private void ValidateStateInvariants()
        {
            WorldQuestReasonCatalog reasonCatalog =
                UnityEngine.Resources.Load<WorldQuestReasonCatalog>(
                    "WorldQuests/WorldQuestReasons");
            foreach (WorldQuestStateData state in gameData.WorldQuests)
            {
                if (state == null ||
                    !registry.TryGet(
                        state.QuestId,
                        out WorldQuestDefinition definition))
                {
                    throw new InvalidDataException(
                        "存档包含没有定义的任务状态。");
                }
                if (state.Status is WorldQuestStatus.Active or
                    WorldQuestStatus.Suspended or
                    WorldQuestStatus.ReadyToTurnIn)
                {
                    WorldQuestStageDefinition stage = definition.FindStage(
                        state.ActiveStageId);
                    if (stage == null)
                    {
                        throw new InvalidDataException(
                            $"任务 '{state.QuestId}' 的活动阶段无效。");
                    }
                    var objectiveIds = new HashSet<string>(
                        stage.Objectives.Select(value => value.ObjectiveId),
                        StringComparer.Ordinal);
                    if (state.ObjectiveProgress.Count != objectiveIds.Count ||
                        state.ObjectiveProgress
                            .Select(value => value?.ObjectiveId)
                            .Distinct(StringComparer.Ordinal)
                            .Count() != objectiveIds.Count ||
                        state.ObjectiveProgress.Any(value =>
                            value == null ||
                            !objectiveIds.Contains(value.ObjectiveId) ||
                            value.CurrentAmount < 0 ||
                            value.CurrentAmount > stage.Objectives
                                .First(objective =>
                                    objective.ObjectiveId ==
                                    value.ObjectiveId)
                                .RequiredAmount ||
                            value.IsComplete !=
                            (value.CurrentAmount >= stage.Objectives
                                .First(objective =>
                                    objective.ObjectiveId ==
                                    value.ObjectiveId)
                                .RequiredAmount)))
                    {
                        throw new InvalidDataException(
                            $"任务 '{state.QuestId}' 的目标进度不属于当前阶段。");
                    }
                }
                if (state.Status == WorldQuestStatus.Completed &&
                    definition.FindOutcome(state.OutcomeId) == null)
                {
                    throw new InvalidDataException(
                        $"任务 '{state.QuestId}' 的完成结局无效。");
                }
                bool needsReason = state.Status is
                    WorldQuestStatus.Suspended or
                    WorldQuestStatus.Failed or
                    WorldQuestStatus.Cancelled;
                if (needsReason ==
                    string.IsNullOrWhiteSpace(state.StatusReasonId))
                {
                    throw new InvalidDataException(
                        $"任务 '{state.QuestId}' 的状态原因无效。");
                }
                if (needsReason &&
                    (reasonCatalog == null ||
                     !reasonCatalog.TryGet(state.StatusReasonId, out _)))
                {
                    throw new InvalidDataException(
                        $"任务 '{state.QuestId}' 引用了未知状态原因。");
                }
                if (state.Status == WorldQuestStatus.Suspended &&
                    state.SuspendedFromStatus is not
                        (WorldQuestStatus.Active or
                         WorldQuestStatus.ReadyToTurnIn))
                {
                    throw new InvalidDataException(
                        $"任务 '{state.QuestId}' 的中断来源状态无效。");
                }
                if (state.Status != WorldQuestStatus.Suspended &&
                    state.SuspendedFromStatus != WorldQuestStatus.Locked)
                {
                    throw new InvalidDataException(
                        $"任务 '{state.QuestId}' 保留了非法的中断来源状态。");
                }
                if (state.AppliedOperationIds.Count > 256)
                {
                    throw new InvalidDataException(
                        $"任务 '{state.QuestId}' 的幂等键超过256个。");
                }
                if (state.AppliedOperationIds.Any(
                        string.IsNullOrWhiteSpace) ||
                    state.AppliedOperationIds.Distinct(
                        StringComparer.Ordinal).Count() !=
                    state.AppliedOperationIds.Count)
                {
                    throw new InvalidDataException(
                        $"任务 '{state.QuestId}' 的幂等键非法或重复。");
                }
                if (state.RunNumber < 0 || state.CompletionCount < 0 ||
                    state.RunNumber < state.CompletionCount)
                {
                    throw new InvalidDataException(
                        $"任务 '{state.QuestId}' 的轮次计数无效。");
                }
            }

            if (gameData.ProcessedWorldQuestEventIds.Count >
                    MaximumProcessedEventIds ||
                gameData.ProcessedWorldQuestEventIds.Any(
                    string.IsNullOrWhiteSpace) ||
                gameData.ProcessedWorldQuestEventIds.Distinct(
                    StringComparer.Ordinal).Count() !=
                gameData.ProcessedWorldQuestEventIds.Count)
            {
                throw new InvalidDataException(
                    "存档中的任务事件幂等列表非法。");
            }

            if (!string.IsNullOrWhiteSpace(gameData.TrackedWorldQuestId))
            {
                WorldQuestStateData tracked = GetState(
                    gameData.TrackedWorldQuestId);
                if (tracked == null || tracked.Status ==
                    WorldQuestStatus.Locked)
                {
                    throw new InvalidDataException(
                        "存档中的追踪任务无效。");
                }
            }

            if (gameData.WorldFacts.Any(value => value == null ||
                    string.IsNullOrWhiteSpace(value.Key)))
            {
                throw new InvalidDataException(
                    "存档包含空世界事实或空事实 ID。");
            }
            foreach (IGrouping<string, WorldFactData> duplicate in
                     gameData.WorldFacts
                         .GroupBy(value => value.Key, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                throw new InvalidDataException(
                    $"世界事实 '{duplicate.Key}' 在存档中重复。");
            }
        }

        public WorldQuestStateData GetState(string questId)
        {
            return gameData.WorldQuests?.FirstOrDefault(state =>
                state != null && state.QuestId == questId);
        }

        public IReadOnlyList<WorldQuestOutcomeDefinition>
            GetEligibleOutcomes(string questId)
        {
            if (!registry.TryGet(questId, out WorldQuestDefinition definition))
                return Array.Empty<WorldQuestOutcomeDefinition>();

            WorldQuestStateData state = GetState(questId);
            if (state == null ||
                state.Status != WorldQuestStatus.ReadyToTurnIn)
            {
                return Array.Empty<WorldQuestOutcomeDefinition>();
            }

            return definition.Outcomes
                .Where(outcome => outcome != null &&
                    WorldQuestConditionEvaluator.Evaluate(
                        outcome.SelectionConditions,
                        gameData,
                        registry,
                        state))
                .OrderByDescending(outcome => outcome.Priority)
                .ThenBy(outcome => IndexOf(definition.Outcomes, outcome))
                .ToList();
        }

        public WorldQuestOperationResult TryAccept(string questId)
        {
            return ExecuteTransaction(questId, () =>
            {
                if (!registry.TryGet(
                        questId,
                        out WorldQuestDefinition definition))
                {
                    return WorldQuestOperationResult.Fail(
                        WorldQuestResultCode.DefinitionNotFound,
                        questId,
                        "任务定义不存在。");
                }

                WorldQuestStateData state = EnsureState(questId);
                if (state.Status != WorldQuestStatus.Available)
                {
                    return WorldQuestOperationResult.Fail(
                        WorldQuestResultCode.InvalidStateTransition,
                        questId,
                        "任务当前不可接取。");
                }

                if (CountRunningQuests() >= MaximumActiveQuests)
                {
                    return WorldQuestOperationResult.Fail(
                        WorldQuestResultCode.ActiveQuestLimitReached,
                        questId,
                        "同时进行的任务已达到上限。");
                }

                if (!string.IsNullOrWhiteSpace(definition.AcceptLocationId) &&
                    definition.AcceptLocationId !=
                    (gameData.ActiveLocationId ?? string.Empty))
                {
                    return WorldQuestOperationResult.Fail(
                        WorldQuestResultCode.ConditionsNotMet,
                        questId,
                        "当前地点不能接取该任务。");
                }

                if (!CanStart(definition, state, includeAvailability: false))
                {
                    return WorldQuestOperationResult.Fail(
                        WorldQuestResultCode.ConditionsNotMet,
                        questId,
                        "接取条件未满足。");
                }

                var changed = new HashSet<string>(StringComparer.Ordinal);
                WorldQuestOperationResult result = StartTask(
                    definition,
                    state,
                    definition.EntryStageId,
                    changed);
                return result.Success
                    ? WorldQuestOperationResult.Ok(
                        questId,
                        changed.OrderBy(value => value).ToArray())
                    : result;
            });
        }

        public WorldQuestOperationResult ReportEvent(WorldQuestEvent questEvent)
        {
            string questId = string.Empty;
            return ExecuteTransaction(questId, () =>
            {
                if (string.IsNullOrWhiteSpace(questEvent.EventId) ||
                    (questEvent.Amount <= 0 &&
                     questEvent.Type !=
                     WorldQuestEventType.InventorySnapshotChanged))
                {
                    return WorldQuestOperationResult.Fail(
                        WorldQuestResultCode.InvalidArgument,
                        questId,
                        "任务事件缺少 ID 或数量非法。");
                }

                if (gameData.ProcessedWorldQuestEventIds.Contains(
                        questEvent.EventId))
                {
                    return WorldQuestOperationResult.NoChange(
                        questId,
                        "任务事件已经处理。");
                }

                var changed = new HashSet<string>(StringComparer.Ordinal);
                foreach (WorldQuestDefinition definition in
                         OrderedDefinitions())
                {
                    WorldQuestStateData state = GetState(definition.Id);
                    if (state?.Status != WorldQuestStatus.Active)
                        continue;

                    if (ApplyEventToCurrentStage(
                            definition,
                            state,
                            questEvent,
                            changed))
                    {
                        questId = definition.Id;
                    }
                }

                RememberEvent(questEvent.EventId);
                ReevaluateAvailabilityInternal(changed);
                ActivateAutomaticQuests(changed);
                return changed.Count == 0
                    ? WorldQuestOperationResult.NoChange(
                        questId,
                        "事件与当前任务目标不匹配。")
                    : WorldQuestOperationResult.Ok(
                        questId,
                        changed.OrderBy(value => value).ToArray());
            });
        }

        public WorldQuestOperationResult TryTurnIn(
            string questId,
            string outcomeId,
            string npcId)
        {
            return ExecuteTransaction(questId, () =>
            {
                if (!registry.TryGet(
                        questId,
                        out WorldQuestDefinition definition))
                {
                    return WorldQuestOperationResult.Fail(
                        WorldQuestResultCode.DefinitionNotFound,
                        questId,
                        "任务定义不存在。");
                }

                WorldQuestStateData state = GetState(questId);
                if (state?.Status != WorldQuestStatus.ReadyToTurnIn)
                {
                    return WorldQuestOperationResult.Fail(
                        WorldQuestResultCode.InvalidStateTransition,
                        questId,
                        "任务尚不可交付。");
                }

                if (definition.TurnInMode == WorldQuestTurnInMode.ManualNpc &&
                    (definition.TurnInNpcIds == null ||
                     !definition.TurnInNpcIds.Contains(npcId)))
                {
                    return WorldQuestOperationResult.Fail(
                        WorldQuestResultCode.ConditionsNotMet,
                        questId,
                        "当前 NPC 不能接收该任务。");
                }

                WorldQuestOutcomeDefinition outcome = definition.FindOutcome(
                    outcomeId);
                if (outcome == null ||
                    !GetEligibleOutcomes(questId).Contains(outcome) ||
                    !WorldQuestConditionEvaluator.Evaluate(
                        outcome.TurnInConditions,
                        gameData,
                        registry,
                        state))
                {
                    return WorldQuestOperationResult.Fail(
                        WorldQuestResultCode.OutcomeNotEligible,
                        questId,
                        "选择的任务结局当前不可用。");
                }

                var changed = new HashSet<string>(StringComparer.Ordinal);
                WorldQuestOperationResult result = CompleteWithOutcome(
                    definition,
                    state,
                    outcome,
                    changed);
                if (!result.Success)
                    return result;

                ReevaluateAvailabilityInternal(changed);
                ActivateAutomaticQuests(changed);
                return WorldQuestOperationResult.Ok(
                    questId,
                    changed.OrderBy(value => value).ToArray());
            });
        }

        public WorldQuestOperationResult SetTrackedQuest(string questId)
        {
            return ExecuteTransaction(questId, () =>
            {
                WorldQuestStateData state = GetState(questId);
                if (state == null || state.Status == WorldQuestStatus.Locked)
                {
                    return WorldQuestOperationResult.Fail(
                        WorldQuestResultCode.StateNotFound,
                        questId,
                        "任务不能被追踪。");
                }

                if (gameData.TrackedWorldQuestId == questId)
                {
                    return WorldQuestOperationResult.NoChange(
                        questId,
                        "任务已经处于追踪状态。");
                }

                gameData.TrackedWorldQuestId = questId;
                return WorldQuestOperationResult.Ok(questId, questId);
            });
        }

        public WorldQuestOperationResult ClearTrackedQuest()
        {
            return ExecuteTransaction(string.Empty, () =>
            {
                if (string.IsNullOrWhiteSpace(
                        gameData.TrackedWorldQuestId))
                {
                    return WorldQuestOperationResult.NoChange(
                        string.Empty,
                        "当前没有正在追踪的任务。");
                }

                gameData.TrackedWorldQuestId = string.Empty;
                return WorldQuestOperationResult.Ok(string.Empty);
            });
        }

        public WorldQuestOperationResult ReevaluateAvailability()
        {
            return ExecuteTransaction(string.Empty, () =>
            {
                var changed = new HashSet<string>(StringComparer.Ordinal);
                ReevaluateAvailabilityInternal(changed);
                ActivateAutomaticQuests(changed);
                return changed.Count == 0
                    ? WorldQuestOperationResult.NoChange(string.Empty)
                    : WorldQuestOperationResult.Ok(
                        string.Empty,
                        changed.OrderBy(value => value).ToArray());
            });
        }

        private WorldQuestOperationResult ExecuteTransaction(
            string questId,
            Func<WorldQuestOperationResult> operation)
        {
            if (transactionActive)
            {
                return WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.ReentrantOperationRejected,
                    questId,
                    "任务事务不允许重入。");
            }

            if (!initialized)
                Initialize();

            transactionActive = true;
            cascadeOperations = 0;
            pendingNotifications.Clear();
            GameDataSnapshot snapshot = GameDataSnapshot.Capture(gameData);
            Dictionary<string, WorldQuestStateData> previous =
                gameData.WorldQuests
                    .Where(state => state != null)
                    .ToDictionary(
                        state => state.QuestId,
                        state => state.Clone(),
                        StringComparer.Ordinal);
            try
            {
                WorldQuestOperationResult result = operation();
                if (!result.Success)
                {
                    snapshot.Restore(gameData);
                    pendingNotifications.Clear();
                    return result;
                }

                var changed = new HashSet<string>(
                    result.ChangedQuestIds,
                    StringComparer.Ordinal);
                WorldQuestOperationResult stabilized =
                    StabilizeTransaction(previous, changed);
                if (!stabilized.Success)
                {
                    snapshot.Restore(gameData);
                    pendingNotifications.Clear();
                    return stabilized;
                }
                if (changed.Count > 0)
                {
                    result = WorldQuestOperationResult.Ok(
                        result.QuestId,
                        changed.OrderBy(value => value).ToArray());
                }

                NormalizeTrackedQuest();
                ValidateStateInvariants();

                PublishChanges(previous);
                foreach (string notification in pendingNotifications)
                    NotificationRequested?.Invoke(notification);
                return result;
            }
            catch (Exception exception)
            {
                snapshot.Restore(gameData);
                pendingNotifications.Clear();
                WorldQuestResultCode code = exception is
                    WorldQuestTransactionException transactionException
                    ? transactionException.Code
                    : WorldQuestResultCode.TransactionRolledBack;
                return WorldQuestOperationResult.Fail(
                    code,
                    questId,
                    exception.Message);
            }
            finally
            {
                transactionActive = false;
                cascadeOperations = 0;
            }
        }

        private WorldQuestOperationResult StabilizeTransaction(
            IReadOnlyDictionary<string, WorldQuestStateData> beforeOperation,
            ISet<string> changed)
        {
            var stageAdvanced = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorldQuestStateData state in gameData.WorldQuests)
            {
                if (state == null ||
                    !beforeOperation.TryGetValue(
                        state.QuestId,
                        out WorldQuestStateData previous))
                {
                    continue;
                }
                if (previous.Status != state.Status ||
                    previous.ActiveStageId != state.ActiveStageId)
                {
                    stageAdvanced.Add(state.QuestId);
                }
            }

            for (int pass = 0; pass < MaximumCascadeOperations; pass++)
            {
                Dictionary<string, int> revisions = gameData.WorldQuests
                    .Where(state => state != null)
                    .ToDictionary(
                        state => state.QuestId,
                        state => state.Revision,
                        StringComparer.Ordinal);

                foreach (WorldQuestDefinition definition in
                         OrderedDefinitions())
                {
                    if (stageAdvanced.Contains(definition.Id))
                        continue;
                    WorldQuestStateData state = GetState(definition.Id);
                    if (state?.Status != WorldQuestStatus.Active)
                        continue;
                    WorldQuestStatus previousStatus = state.Status;
                    string previousStage = state.ActiveStageId;
                    WorldQuestOperationResult result =
                        EvaluateCurrentSnapshotObjectives(
                            definition,
                            state,
                            changed);
                    if (!result.Success)
                        return result;
                    if (state.Status != previousStatus ||
                        state.ActiveStageId != previousStage)
                    {
                        stageAdvanced.Add(definition.Id);
                    }
                }

                ReevaluateAvailabilityInternal(changed);
                ActivateAutomaticQuests(changed);
                NormalizeTrackedQuest();

                bool stateChanged = gameData.WorldQuests.Any(state =>
                    state != null &&
                    (!revisions.TryGetValue(state.QuestId, out int revision) ||
                     revision != state.Revision));
                if (!stateChanged)
                    return WorldQuestOperationResult.Ok(string.Empty);
            }

            return WorldQuestOperationResult.Fail(
                WorldQuestResultCode.TransactionCycleDetected,
                string.Empty,
                "任务事务在快照与可用性收敛阶段超过512次迭代。");
        }

        private void PublishChanges(
            IReadOnlyDictionary<string, WorldQuestStateData> previous)
        {
            foreach (WorldQuestStateData state in gameData.WorldQuests
                         .Where(value => value != null)
                         .OrderBy(value => value.QuestId,
                             StringComparer.Ordinal))
            {
                previous.TryGetValue(
                    state.QuestId,
                    out WorldQuestStateData oldState);
                if (oldState != null && StatesEqual(oldState, state))
                    continue;
                QuestChanged?.Invoke(new WorldQuestChangedEvent(
                    state.QuestId,
                    oldState,
                    state.Clone()));
            }
        }

        private WorldQuestOperationResult StartTask(
            WorldQuestDefinition definition,
            WorldQuestStateData state,
            string stageId,
            ISet<string> changed)
        {
            WorldQuestStageDefinition stage = definition.FindStage(stageId);
            if (stage == null)
            {
                return WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.DefinitionInvalid,
                    definition.Id,
                    $"入口阶段 '{stageId}' 不存在。");
            }

            state.RunNumber++;
            state.Status = WorldQuestStatus.Active;
            state.ActiveStageId = stage.StageId;
            state.ObjectiveProgress = CreateProgress(stage);
            state.SelectedChoiceId = string.Empty;
            state.OutcomeId = string.Empty;
            state.StatusReasonId = string.Empty;
            state.SuspendedFromStatus = WorldQuestStatus.Locked;
            state.AvailabilityOverride = false;
            state.AcceptedWorldHour = gameData.WorldElapsedHours;
            state.LastUpdatedWorldHour = gameData.WorldElapsedHours;
            state.NextAvailableWorldHour = -1;
            state.AppliedOperationIds.Clear();
            state.Revision++;
            changed.Add(definition.Id);
            if (definition.DefaultTracked &&
                string.IsNullOrWhiteSpace(gameData.TrackedWorldQuestId))
            {
                gameData.TrackedWorldQuestId = definition.Id;
            }

            WorldQuestOperationResult effects = ApplyEffects(
                definition,
                state,
                "accept",
                definition.OnAcceptEffects,
                changed);
            if (!effects.Success)
                return effects;
            WorldQuestOperationResult entered = ApplyEffects(
                definition,
                state,
                $"stage-enter:{stage.StageId}",
                stage.OnEnterEffects,
                changed);
            if (entered.Success)
            {
                SynchronizeLegacyFields(definition, state, true, false);
                return EvaluateCurrentSnapshotObjectives(
                    definition,
                    state,
                    changed);
            }
            return entered;
        }

        private WorldQuestOperationResult EvaluateCurrentSnapshotObjectives(
            WorldQuestDefinition definition,
            WorldQuestStateData state,
            ISet<string> changed)
        {
            if (state.Status != WorldQuestStatus.Active)
                return WorldQuestOperationResult.Ok(definition.Id);
            WorldQuestStageDefinition stage = definition.FindStage(
                state.ActiveStageId);
            if (stage == null)
                return WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.DefinitionInvalid,
                    definition.Id,
                    "活动阶段不存在。");

            bool updated = false;
            foreach (WorldQuestObjectiveDefinition objective in
                     stage.Objectives.Where(value => value != null &&
                         value.ProgressMode ==
                         WorldQuestProgressMode.SetToCurrentSnapshot))
            {
                WorldQuestObjectiveProgressData progress =
                    state.ObjectiveProgress.First(value =>
                        value.ObjectiveId == objective.ObjectiveId);
                int amount = Math.Max(
                    0,
                    Math.Min(
                        objective.RequiredAmount,
                        GetSnapshotAmount(objective, gameData)));
                if (progress.CurrentAmount == amount &&
                    progress.IsComplete ==
                    (amount >= objective.RequiredAmount))
                {
                    continue;
                }
                progress.CurrentAmount = amount;
                progress.IsComplete = amount >= objective.RequiredAmount;
                updated = true;
            }
            if (!updated)
                return WorldQuestOperationResult.Ok(definition.Id);

            state.LastUpdatedWorldHour = gameData.WorldElapsedHours;
            state.Revision++;
            changed.Add(definition.Id);
            SynchronizeLegacyFields(definition, state, true, false);
            return IsStageComplete(stage, state)
                ? CompleteStage(definition, state, stage, changed)
                : WorldQuestOperationResult.Ok(definition.Id);
        }

        private bool ApplyEventToCurrentStage(
            WorldQuestDefinition definition,
            WorldQuestStateData state,
            WorldQuestEvent questEvent,
            ISet<string> changed)
        {
            WorldQuestStageDefinition stage = definition.FindStage(
                state.ActiveStageId);
            if (stage == null)
                throw new InvalidOperationException(
                    $"Quest '{definition.Id}' has invalid stage " +
                    $"'{state.ActiveStageId}'.");

            bool anyProgress = false;
            foreach (WorldQuestObjectiveDefinition objective in
                     stage.Objectives)
            {
                if (objective == null ||
                    !Matches(objective, questEvent, gameData))
                {
                    continue;
                }

                WorldQuestObjectiveProgressData progress =
                    state.ObjectiveProgress.First(value =>
                        value.ObjectiveId == objective.ObjectiveId);
                if (progress.IsComplete &&
                    objective.ProgressMode !=
                    WorldQuestProgressMode.SetToCurrentSnapshot)
                {
                    continue;
                }

                int previousAmount = progress.CurrentAmount;
                progress.CurrentAmount = CalculateProgress(
                    objective,
                    questEvent,
                    gameData,
                    progress.CurrentAmount);
                progress.IsComplete =
                    progress.CurrentAmount >= objective.RequiredAmount;
                anyProgress |= previousAmount != progress.CurrentAmount;
                if (objective.Type == WorldQuestObjectiveType.DialogueChoice &&
                    progress.IsComplete)
                {
                    if (!string.IsNullOrEmpty(state.SelectedChoiceId) &&
                        state.SelectedChoiceId != questEvent.SecondaryTargetId)
                    {
                        throw new InvalidOperationException(
                            "该任务分支已经确认，不能再次修改。");
                    }
                    state.SelectedChoiceId = questEvent.SecondaryTargetId;
                }
            }

            if (!anyProgress)
                return false;

            SynchronizeLegacyFields(definition, state, true, false);

            state.LastUpdatedWorldHour = questEvent.WorldHour;
            state.Revision++;
            changed.Add(definition.Id);
            if (IsStageComplete(stage, state))
            {
                WorldQuestOperationResult result = CompleteStage(
                    definition,
                    state,
                    stage,
                    changed);
                if (!result.Success)
                    throw new InvalidOperationException(result.Message);
            }

            return true;
        }

        private WorldQuestOperationResult CompleteStage(
            WorldQuestDefinition definition,
            WorldQuestStateData state,
            WorldQuestStageDefinition stage,
            ISet<string> changed)
        {
            WorldQuestOperationResult effects = ApplyEffects(
                definition,
                state,
                $"stage-complete:{stage.StageId}",
                stage.OnCompleteEffects,
                changed);
            if (!effects.Success)
                return effects;

            WorldQuestStageTransitionDefinition transition = stage.Transitions
                .Where(candidate => candidate != null &&
                    (string.IsNullOrWhiteSpace(candidate.RequiredChoiceId) ||
                     candidate.RequiredChoiceId == state.SelectedChoiceId) &&
                    WorldQuestConditionEvaluator.Evaluate(
                        candidate.Conditions,
                        gameData,
                        registry,
                        state))
                .OrderByDescending(candidate => candidate.Priority)
                .ThenBy(candidate => IndexOf(stage.Transitions, candidate))
                .FirstOrDefault();

            if (transition != null)
            {
                WorldQuestStageDefinition next = definition.FindStage(
                    transition.TargetStageId);
                if (next == null)
                {
                    return WorldQuestOperationResult.Fail(
                        WorldQuestResultCode.DefinitionInvalid,
                        definition.Id,
                        $"目标阶段 '{transition.TargetStageId}' 不存在。");
                }

                state.ActiveStageId = next.StageId;
                state.ObjectiveProgress = CreateProgress(next);
                state.SelectedChoiceId = string.Empty;
                state.Revision++;
                changed.Add(definition.Id);
                WorldQuestOperationResult entered = ApplyEffects(
                    definition,
                    state,
                    $"stage-enter:{next.StageId}",
                    next.OnEnterEffects,
                    changed);
                if (entered.Success)
                    SynchronizeLegacyFields(definition, state, true, false);
                return entered;
            }

            if (stage.Transitions.Count > 0)
            {
                return WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.DefinitionInvalid,
                    definition.Id,
                    "阶段完成后没有可用转移。");
            }

            if (definition.TurnInMode == WorldQuestTurnInMode.ManualNpc)
            {
                state.Status = WorldQuestStatus.ReadyToTurnIn;
                state.Revision++;
                changed.Add(definition.Id);
                SynchronizeLegacyFields(definition, state, true, false);
                return WorldQuestOperationResult.Ok(definition.Id);
            }

            IReadOnlyList<WorldQuestOutcomeDefinition> eligible =
                definition.Outcomes
                    .Where(outcome => outcome != null &&
                        WorldQuestConditionEvaluator.Evaluate(
                            outcome.SelectionConditions,
                            gameData,
                            registry,
                            state))
                    .OrderByDescending(outcome => outcome.Priority)
                    .ThenBy(outcome =>
                        IndexOf(definition.Outcomes, outcome))
                    .ToList();
            if (eligible.Count == 0 ||
                eligible.Count > 1 &&
                eligible[0].Priority == eligible[1].Priority)
            {
                return WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.NoEligibleOutcome,
                    definition.Id,
                    "自动任务结局不存在或不唯一。");
            }

            return CompleteWithOutcome(
                definition,
                state,
                eligible[0],
                changed);
        }

        private WorldQuestOperationResult CompleteWithOutcome(
            WorldQuestDefinition definition,
            WorldQuestStateData state,
            WorldQuestOutcomeDefinition outcome,
            ISet<string> changed)
        {
            WorldQuestOperationResult effects = ApplyEffects(
                definition,
                state,
                $"outcome:{outcome.OutcomeId}",
                outcome.Effects,
                changed);
            if (!effects.Success)
                return effects;

            state.Status = WorldQuestStatus.Completed;
            state.OutcomeId = outcome.OutcomeId;
            state.StatusReasonId = string.Empty;
            state.CompletionCount++;
            state.LastCompletedWorldHour = gameData.WorldElapsedHours;
            state.LastUpdatedWorldHour = gameData.WorldElapsedHours;
            state.NextAvailableWorldHour = CalculateNextAvailable(
                definition,
                state.LastCompletedWorldHour);
            state.Revision++;
            changed.Add(definition.Id);
            SynchronizeLegacyFields(definition, state, true, true);
            if (!string.IsNullOrWhiteSpace(outcome.ResolutionText))
                pendingNotifications.Add(outcome.ResolutionText);
            return WorldQuestOperationResult.Ok(definition.Id);
        }

        private static void SynchronizeLegacyFields(
            WorldQuestDefinition definition,
            WorldQuestStateData state,
            bool acceptanceRewardClaimed,
            bool completionRewardClaimed)
        {
#pragma warning disable CS0612
            WorldQuestStageDefinition stage = definition.FindStage(
                state.ActiveStageId);
            state.ObjectiveIndex = stage == null
                ? 0
                : Math.Max(0, IndexOf(definition.Stages, stage));
            state.CurrentAmount = state.ObjectiveProgress?
                .FirstOrDefault(value => value != null && !value.IsComplete)
                ?.CurrentAmount ??
                state.ObjectiveProgress?.LastOrDefault()?.CurrentAmount ?? 0;
            state.AcceptanceRewardClaimed = acceptanceRewardClaimed;
            state.CompletionRewardClaimed = completionRewardClaimed;
#pragma warning restore CS0612
        }

        private WorldQuestOperationResult ApplyEffects(
            WorldQuestDefinition sourceDefinition,
            WorldQuestStateData sourceState,
            string scopeId,
            IReadOnlyList<WorldQuestEffectDefinition> effects,
            ISet<string> changed)
        {
            if (effects == null)
                return WorldQuestOperationResult.Ok(sourceDefinition.Id);

            foreach (WorldQuestEffectDefinition effect in effects)
            {
                if (effect == null)
                    continue;
                CountCascadeOperation();
                string operationId =
                    $"{sourceDefinition.Id}:{sourceState.RunNumber}:" +
                    $"{scopeId}:{effect.EffectId}";
                if (sourceState.AppliedOperationIds.Contains(operationId))
                    continue;

                WorldQuestOperationResult result = ApplyEffect(
                    effect,
                    sourceDefinition,
                    sourceState,
                    changed);
                if (!result.Success)
                    return result;
                sourceState.AppliedOperationIds.Add(operationId);
            }

            return WorldQuestOperationResult.Ok(sourceDefinition.Id);
        }

        private WorldQuestOperationResult ApplyEffect(
            WorldQuestEffectDefinition effect,
            WorldQuestDefinition sourceDefinition,
            WorldQuestStateData sourceState,
            ISet<string> changed)
        {
            switch (effect.Type)
            {
                case WorldQuestEffectType.GrantCard:
                    if (string.IsNullOrWhiteSpace(effect.TargetId) ||
                        effect.IntValue <= 0)
                    {
                        return InvalidEffect(sourceDefinition.Id, effect);
                    }
                    for (int index = 0; index < effect.IntValue; index++)
                    {
                        if (!gameData.EnsureBackpack().TryAdd(
                                CreateRewardCard(effect.TargetId),
                                out _))
                        {
                            return WorldQuestOperationResult.Fail(
                                WorldQuestResultCode.InventoryCapacityInsufficient,
                                sourceDefinition.Id,
                                "背包无法接收任务奖励。");
                        }
                    }
                    return WorldQuestOperationResult.Ok(sourceDefinition.Id);

                case WorldQuestEffectType.GrantProtagonistExperience:
                    CardData protagonist = gameData.GetProtagonistData();
                    if (protagonist == null || effect.IntValue <= 0)
                    {
                        return WorldQuestOperationResult.Fail(
                            WorldQuestResultCode.ResourceNotFound,
                            sourceDefinition.Id,
                            "主角不存在，无法发放经验。");
                    }
                    CharacterProgressionService.GrantExperience(
                        protagonist,
                        effect.IntValue);
                    return WorldQuestOperationResult.Ok(sourceDefinition.Id);

                case WorldQuestEffectType.SetWorldFactBool:
                    return SetFact(
                        sourceDefinition.Id,
                        effect.TargetId,
                        WorldFactValueType.Bool,
                        fact => fact.BoolValue = effect.BoolValue);
                case WorldQuestEffectType.SetWorldFactInt:
                    return SetFact(
                        sourceDefinition.Id,
                        effect.TargetId,
                        WorldFactValueType.Int,
                        fact => fact.IntValue = effect.IntValue);
                case WorldQuestEffectType.SetWorldFactString:
                    return SetFact(
                        sourceDefinition.Id,
                        effect.TargetId,
                        WorldFactValueType.String,
                        fact => fact.StringValue = effect.StringValue ??
                            string.Empty);
                case WorldQuestEffectType.IncrementWorldFactInt:
                    return SetFact(
                        sourceDefinition.Id,
                        effect.TargetId,
                        WorldFactValueType.Int,
                        fact => fact.IntValue = checked(
                            fact.IntValue + effect.IntValue));

                case WorldQuestEffectType.MakeQuestAvailable:
                    if (!registry.TryGet(
                            effect.TargetId,
                            out WorldQuestDefinition availableDefinition))
                    {
                        return MissingTarget(sourceDefinition.Id, effect);
                    }
                    if (!WorldQuestConditionEvaluator.Evaluate(
                            availableDefinition.HardStartConditions,
                            gameData,
                            registry,
                            EnsureState(effect.TargetId)))
                    {
                        return WorldQuestOperationResult.Fail(
                            WorldQuestResultCode.ConditionsNotMet,
                            effect.TargetId,
                            "目标任务的硬性开始条件未满足。");
                    }
                    WorldQuestStateData availableState = EnsureState(
                        effect.TargetId);
                    if (availableState.Status != WorldQuestStatus.Locked)
                    {
                        return WorldQuestOperationResult.NoChange(
                            effect.TargetId);
                    }
                    return ChangeTargetQuest(
                        effect,
                        changed,
                        target =>
                        {
                            if (target.Status == WorldQuestStatus.Locked)
                                target.Status = WorldQuestStatus.Available;
                            target.AvailabilityOverride = true;
                        });
                case WorldQuestEffectType.ActivateQuest:
                    if (!registry.TryGet(
                            effect.TargetId,
                            out WorldQuestDefinition targetDefinition))
                    {
                        return MissingTarget(sourceDefinition.Id, effect);
                    }
                    WorldQuestStateData targetState = EnsureState(
                        effect.TargetId);
                    if (!WorldQuestConditionEvaluator.Evaluate(
                            targetDefinition.HardStartConditions,
                            gameData,
                            registry,
                            targetState))
                    {
                        return WorldQuestOperationResult.Fail(
                            WorldQuestResultCode.ConditionsNotMet,
                            effect.TargetId,
                            "目标任务的硬性开始条件未满足。");
                    }
                    if (targetState.Status is WorldQuestStatus.Active or
                        WorldQuestStatus.Suspended or
                        WorldQuestStatus.ReadyToTurnIn)
                    {
                        return WorldQuestOperationResult.NoChange(
                            effect.TargetId);
                    }
                    if (targetState.Status is WorldQuestStatus.Completed or
                        WorldQuestStatus.Failed or
                        WorldQuestStatus.Cancelled)
                    {
                        bool canRepeat = targetDefinition.RepeatPolicy !=
                                WorldQuestRepeatPolicy.Never &&
                            targetState.NextAvailableWorldHour >= 0 &&
                            gameData.WorldElapsedHours >=
                            targetState.NextAvailableWorldHour;
                        if (!canRepeat)
                        {
                            return WorldQuestOperationResult.Fail(
                                WorldQuestResultCode.InvalidStateTransition,
                                effect.TargetId,
                                "终态任务尚未达到可重复启动时间。");
                        }
                        ResetForRepeat(targetState);
                        changed.Add(effect.TargetId);
                    }
                    if (CountRunningQuests() >= MaximumActiveQuests)
                    {
                        return WorldQuestOperationResult.Fail(
                            WorldQuestResultCode.ActiveQuestLimitReached,
                            effect.TargetId,
                            "同时进行的任务已达到上限。");
                    }
                    return StartTask(
                        targetDefinition,
                        targetState,
                        string.IsNullOrWhiteSpace(effect.SecondaryTargetId)
                            ? targetDefinition.EntryStageId
                            : effect.SecondaryTargetId,
                        changed);
                case WorldQuestEffectType.SuspendQuest:
                    WorldQuestStateData suspendTarget = GetState(
                        effect.TargetId);
                    if (suspendTarget?.Status ==
                        WorldQuestStatus.Suspended)
                    {
                        return suspendTarget.StatusReasonId == effect.ReasonId
                            ? WorldQuestOperationResult.NoChange(
                                effect.TargetId)
                            : WorldQuestOperationResult.Fail(
                                WorldQuestResultCode.InvalidStateTransition,
                                effect.TargetId,
                                "任务已因其他原因中断。");
                    }
                    return ChangeTargetQuest(
                        effect,
                        changed,
                        target =>
                        {
                            if (target.Status is not
                                (WorldQuestStatus.Active or
                                 WorldQuestStatus.ReadyToTurnIn))
                            {
                                throw new InvalidOperationException(
                                    "只有进行中或可交付任务能被中断。");
                            }
                            target.SuspendedFromStatus = target.Status;
                            target.Status = WorldQuestStatus.Suspended;
                            target.StatusReasonId = effect.ReasonId;
                        });
                case WorldQuestEffectType.ResumeQuest:
                    return ChangeTargetQuest(
                        effect,
                        changed,
                        target =>
                        {
                            if (target.Status != WorldQuestStatus.Suspended)
                                throw new InvalidOperationException(
                                    "目标任务当前没有中断。");
                            target.Status = target.SuspendedFromStatus;
                            target.SuspendedFromStatus =
                                WorldQuestStatus.Locked;
                            target.StatusReasonId = string.Empty;
                        });
                case WorldQuestEffectType.FailQuest:
                    return EndTargetQuest(
                        effect,
                        WorldQuestStatus.Failed,
                        changed);
                case WorldQuestEffectType.CancelQuest:
                    return EndTargetQuest(
                        effect,
                        WorldQuestStatus.Cancelled,
                        changed);
                case WorldQuestEffectType.TrackQuest:
                    WorldQuestStateData tracked = GetState(effect.TargetId);
                    if (tracked == null)
                        return MissingTarget(sourceDefinition.Id, effect);
                    if (tracked.Status is not
                        (WorldQuestStatus.Available or
                         WorldQuestStatus.Active or
                         WorldQuestStatus.Suspended or
                         WorldQuestStatus.ReadyToTurnIn))
                    {
                        return WorldQuestOperationResult.Fail(
                            WorldQuestResultCode.InvalidStateTransition,
                            effect.TargetId,
                            "当前任务状态不能被追踪。");
                    }
                    gameData.TrackedWorldQuestId = effect.TargetId;
                    return WorldQuestOperationResult.Ok(sourceDefinition.Id);
                case WorldQuestEffectType.ShowNotification:
                    if (!string.IsNullOrWhiteSpace(effect.Notification))
                        pendingNotifications.Add(effect.Notification);
                    return WorldQuestOperationResult.Ok(sourceDefinition.Id);
                default:
                    return InvalidEffect(sourceDefinition.Id, effect);
            }
        }

        private WorldQuestOperationResult SetFact(
            string questId,
            string key,
            WorldFactValueType type,
            Action<WorldFactData> apply)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.DefinitionInvalid,
                    questId,
                    "世界事实 ID 为空。");
            }

            WorldFactData fact = gameData.WorldFacts.FirstOrDefault(value =>
                value != null && value.Key == key);
            if (fact == null)
            {
                fact = new WorldFactData { Key = key, Type = type };
                gameData.WorldFacts.Add(fact);
            }
            else if (fact.Type != type)
            {
                return WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.DefinitionInvalid,
                    questId,
                    $"世界事实 '{key}' 类型冲突。");
            }

            apply(fact);
            fact.Revision++;
            return WorldQuestOperationResult.Ok(questId);
        }

        private WorldQuestOperationResult ChangeTargetQuest(
            WorldQuestEffectDefinition effect,
            ISet<string> changed,
            Action<WorldQuestStateData> change)
        {
            if (!registry.TryGet(effect.TargetId, out _))
                return MissingTarget(string.Empty, effect);
            WorldQuestStateData target = EnsureState(effect.TargetId);
            change(target);
            target.LastUpdatedWorldHour = gameData.WorldElapsedHours;
            target.Revision++;
            changed.Add(effect.TargetId);
            return WorldQuestOperationResult.Ok(effect.TargetId);
        }

        private WorldQuestOperationResult EndTargetQuest(
            WorldQuestEffectDefinition effect,
            WorldQuestStatus status,
            ISet<string> changed)
        {
            WorldQuestStateData current = GetState(effect.TargetId);
            if (current == null)
                return MissingTarget(string.Empty, effect);
            if (current.Status == status &&
                current.StatusReasonId == effect.ReasonId)
            {
                return WorldQuestOperationResult.NoChange(effect.TargetId);
            }
            if (current.Status is WorldQuestStatus.Completed or
                WorldQuestStatus.Failed or
                WorldQuestStatus.Cancelled)
            {
                return WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.InvalidStateTransition,
                    effect.TargetId,
                    "终态任务不能再次结束。");
            }
            if (current.Status is not
                (WorldQuestStatus.Available or
                 WorldQuestStatus.Active or
                 WorldQuestStatus.Suspended or
                 WorldQuestStatus.ReadyToTurnIn))
            {
                return WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.InvalidStateTransition,
                    effect.TargetId,
                    "当前任务状态不能进入终态。");
            }
            return ChangeTargetQuest(
                effect,
                changed,
                target =>
                {
                    target.Status = status;
                    target.StatusReasonId = effect.ReasonId;
                    if (registry.TryGet(
                            effect.TargetId,
                            out WorldQuestDefinition definition))
                    {
                        target.NextAvailableWorldHour =
                            CalculateNextAvailable(
                                definition,
                                gameData.WorldElapsedHours);
                    }
                });
        }

        private void ReevaluateAvailabilityInternal(
            ISet<string> changed = null)
        {
            foreach (WorldQuestDefinition definition in OrderedDefinitions())
            {
                WorldQuestStateData state = EnsureState(definition.Id);
                if (state.Status is WorldQuestStatus.Completed or
                    WorldQuestStatus.Failed or
                    WorldQuestStatus.Cancelled)
                {
                    if (definition.RepeatPolicy !=
                            WorldQuestRepeatPolicy.Never &&
                        state.NextAvailableWorldHour >= 0 &&
                        gameData.WorldElapsedHours >=
                        state.NextAvailableWorldHour)
                    {
                        ResetForRepeat(state);
                        changed?.Add(definition.Id);
                    }
                    continue;
                }

                if (state.Status is not
                    (WorldQuestStatus.Locked or WorldQuestStatus.Available))
                {
                    continue;
                }

                bool available = CanStart(
                    definition,
                    state,
                    includeAvailability: true);
                WorldQuestStatus targetStatus = available
                    ? WorldQuestStatus.Available
                    : WorldQuestStatus.Locked;
                if (state.Status == targetStatus)
                    continue;
                state.Status = targetStatus;
                state.AvailableSinceWorldHour = available
                    ? gameData.WorldElapsedHours
                    : -1;
                state.Revision++;
                changed?.Add(definition.Id);
            }
        }

        private void NormalizeTrackedQuest()
        {
            if (string.IsNullOrWhiteSpace(gameData.TrackedWorldQuestId))
                return;

            WorldQuestStateData tracked = GetState(
                gameData.TrackedWorldQuestId);
            if (tracked == null || tracked.Status == WorldQuestStatus.Locked)
                gameData.TrackedWorldQuestId = string.Empty;
        }

        private void ActivateAutomaticQuests(ISet<string> changed = null)
        {
            foreach (WorldQuestDefinition definition in OrderedDefinitions())
            {
                if (definition.StartMode != WorldQuestStartMode.AutoActivate)
                    continue;
                WorldQuestStateData state = GetState(definition.Id);
                if (state?.Status != WorldQuestStatus.Available)
                    continue;
                if (CountRunningQuests() >= MaximumActiveQuests)
                    break;
                var localChanges = changed ??
                    new HashSet<string>(StringComparer.Ordinal);
                WorldQuestOperationResult result = StartTask(
                    definition,
                    state,
                    definition.EntryStageId,
                    localChanges);
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.Message);
                }
            }
        }

        private bool CanStart(
            WorldQuestDefinition definition,
            WorldQuestStateData state,
            bool includeAvailability)
        {
            if (!WorldQuestConditionEvaluator.Evaluate(
                    definition.HardStartConditions,
                    gameData,
                    registry,
                    state))
            {
                return false;
            }

            if (includeAvailability &&
                !state.AvailabilityOverride &&
                !WorldQuestConditionEvaluator.Evaluate(
                    definition.AvailabilityConditions,
                    gameData,
                    registry,
                    state))
            {
                return false;
            }

            return includeAvailability ||
                WorldQuestConditionEvaluator.Evaluate(
                    definition.AcceptConditions,
                    gameData,
                    registry,
                    state);
        }

        private WorldQuestStateData EnsureState(string questId)
        {
            WorldQuestStateData state = GetState(questId);
            if (state != null)
            {
                state.ObjectiveProgress ??=
                    new List<WorldQuestObjectiveProgressData>();
                state.AppliedOperationIds ??= new List<string>();
                return state;
            }

            state = new WorldQuestStateData
            {
                QuestId = questId,
                Status = WorldQuestStatus.Locked
            };
            gameData.WorldQuests.Add(state);
            return state;
        }

        private void NormalizeCollections()
        {
            gameData.WorldQuests ??= new List<WorldQuestStateData>();
            gameData.WorldFacts ??= new List<WorldFactData>();
            gameData.ProcessedWorldQuestEventIds ??= new List<string>();
            gameData.EnsureBackpack();
        }

        private void MigrateV1States()
        {
            if (gameData.WorldQuestStateVersion >=
                GameData.CurrentWorldQuestStateVersion)
            {
                return;
            }

            foreach (WorldQuestStateData state in gameData.WorldQuests
                         .Where(value => value != null))
            {
                if (!registry.TryGet(
                        state.QuestId,
                        out WorldQuestDefinition definition))
                {
                    throw new InvalidDataException(
                        $"旧存档引用未知任务：{state.QuestId}");
                }

#pragma warning disable CS0612
                int legacyStatus = (int)state.Status;
                ValidateLegacyState(state, definition, legacyStatus);
                int objectiveIndex = legacyStatus >= 2
                    ? definition.Stages.Count - 1
                    : state.ObjectiveIndex;
                WorldQuestStageDefinition stage = legacyStatus == 0
                    ? null
                    : definition.Stages[objectiveIndex];
                state.ActiveStageId = legacyStatus == 0
                    ? string.Empty
                    : stage?.StageId;
                state.ObjectiveProgress = stage == null
                    ? new List<WorldQuestObjectiveProgressData>()
                    : CreateProgress(stage);
                if (legacyStatus >= 2)
                {
                    foreach (WorldQuestObjectiveProgressData progress in
                             state.ObjectiveProgress)
                    {
                        WorldQuestObjectiveDefinition objective =
                            stage.Objectives.FirstOrDefault(value =>
                                value != null &&
                                value.ObjectiveId == progress.ObjectiveId);
                        progress.CurrentAmount = objective?.RequiredAmount ?? 0;
                        progress.IsComplete = objective != null;
                    }
                }
                else if (state.ObjectiveProgress.Count > 0)
                {
                    WorldQuestObjectiveDefinition objective =
                        stage.Objectives[0];
                    state.ObjectiveProgress[0].CurrentAmount = Math.Min(
                        objective.RequiredAmount,
                        Math.Max(0, state.CurrentAmount));
                    state.ObjectiveProgress[0].IsComplete =
                        state.ObjectiveProgress[0].CurrentAmount >=
                        objective.RequiredAmount;
                }

                state.Status = legacyStatus switch
                {
                    0 => WorldQuestStatus.Available,
                    1 => WorldQuestStatus.Active,
                    2 => WorldQuestStatus.ReadyToTurnIn,
                    3 => WorldQuestStatus.Completed,
                    _ => WorldQuestStatus.Locked
                };
                state.RunNumber = legacyStatus == 0 ? 0 : 1;
                state.CompletionCount = legacyStatus == 3 ? 1 : 0;
                if (legacyStatus == 3)
                {
                    state.OutcomeId = definition.Outcomes
                        .FirstOrDefault(outcome => outcome.IsDefault)
                        ?.OutcomeId ??
                        definition.Outcomes.FirstOrDefault()?.OutcomeId;
                }
                if (state.AcceptanceRewardClaimed)
                {
                    AddMigratedEffectKeys(
                        state,
                        definition,
                        "accept",
                        definition.OnAcceptEffects);
                }
                if (state.CompletionRewardClaimed &&
                    !string.IsNullOrWhiteSpace(state.OutcomeId))
                {
                    WorldQuestOutcomeDefinition outcome =
                        definition.FindOutcome(state.OutcomeId);
                    AddMigratedEffectKeys(
                        state,
                        definition,
                        $"outcome:{state.OutcomeId}",
                        outcome?.Effects);
                }
                else if (legacyStatus == 3 &&
                         !string.IsNullOrWhiteSpace(state.OutcomeId))
                {
                    WorldQuestOutcomeDefinition outcome =
                        definition.FindOutcome(state.OutcomeId);
                    WorldQuestOperationResult recovery = ApplyEffects(
                        definition,
                        state,
                        $"outcome:{state.OutcomeId}",
                        outcome?.Effects,
                        new HashSet<string>(StringComparer.Ordinal));
                    if (!recovery.Success)
                    {
                        throw new InvalidOperationException(
                            $"旧存档任务 '{definition.Id}' 的未领取奖励恢复失败：" +
                            recovery.Message);
                    }
                }
                state.ObjectiveIndex = 0;
                state.CurrentAmount = 0;
                state.AcceptanceRewardClaimed = false;
                state.CompletionRewardClaimed = false;
#pragma warning restore CS0612
            }

            gameData.WorldQuestStateVersion =
                GameData.CurrentWorldQuestStateVersion;
        }

        private static void ValidateLegacyState(
            WorldQuestStateData state,
            WorldQuestDefinition definition,
            int legacyStatus)
        {
#pragma warning disable CS0612
            if (legacyStatus < 0 || legacyStatus > 3)
                throw new InvalidDataException("V1 任务状态数值非法。");
            if (state.ObjectiveIndex < 0 ||
                state.ObjectiveIndex >= definition.Stages.Count)
            {
                throw new InvalidDataException(
                    $"V1 任务 '{state.QuestId}' 的目标索引越界。");
            }
            WorldQuestStageDefinition stage =
                definition.Stages[state.ObjectiveIndex];
            int maximum = stage.Objectives.FirstOrDefault()
                ?.RequiredAmount ?? 0;
            if (state.CurrentAmount < 0 || state.CurrentAmount > maximum)
            {
                throw new InvalidDataException(
                    $"V1 任务 '{state.QuestId}' 的目标进度非法。");
            }
            if (legacyStatus == 0 &&
                (state.ObjectiveIndex != 0 || state.CurrentAmount != 0 ||
                 state.AcceptanceRewardClaimed ||
                 state.CompletionRewardClaimed))
            {
                throw new InvalidDataException(
                    $"V1 可接任务 '{state.QuestId}' 含有已开始数据。");
            }
            if (legacyStatus != 3 && state.CompletionRewardClaimed)
            {
                throw new InvalidDataException(
                    $"V1 未完成任务 '{state.QuestId}' 已标记完成奖励。");
            }
#pragma warning restore CS0612
        }

        private static void AddMigratedEffectKeys(
            WorldQuestStateData state,
            WorldQuestDefinition definition,
            string scope,
            IReadOnlyList<WorldQuestEffectDefinition> effects)
        {
            if (effects == null)
                return;
            foreach (WorldQuestEffectDefinition effect in effects)
            {
                if (effect == null)
                    continue;
                state.AppliedOperationIds.Add(
                    $"{definition.Id}:{Math.Max(1, state.RunNumber)}:" +
                    $"{scope}:{effect.EffectId}");
            }
        }

        private void MergeDuplicateLegacyStates()
        {
            if (gameData.WorldQuests.Any(state => state == null ||
                    string.IsNullOrWhiteSpace(state.QuestId)))
            {
                throw new InvalidDataException(
                    "V1 存档包含空任务状态或空 QuestId。");
            }

            var canonicalStates = new List<WorldQuestStateData>();
            foreach (IGrouping<string, WorldQuestStateData> group in
                     gameData.WorldQuests.GroupBy(
                         state => state.QuestId,
                         StringComparer.Ordinal))
            {
                if (!registry.TryGet(
                        group.Key,
                        out WorldQuestDefinition definition))
                {
                    throw new InvalidDataException(
                        $"旧存档引用未知任务：{group.Key}");
                }

                List<WorldQuestStateData> candidates = group.ToList();
                foreach (WorldQuestStateData candidate in candidates)
                {
#pragma warning disable CS0612
                    ValidateLegacyState(
                        candidate,
                        definition,
                        (int)candidate.Status);
#pragma warning restore CS0612
                }

#pragma warning disable CS0612
                WorldQuestStateData canonical = candidates
                    .OrderByDescending(candidate => (int)candidate.Status)
                    .ThenByDescending(candidate => candidate.ObjectiveIndex)
                    .ThenByDescending(candidate => candidate.CurrentAmount)
                    .First();
                canonical.AcceptanceRewardClaimed = candidates.Any(
                    candidate => candidate.AcceptanceRewardClaimed);
                canonical.CompletionRewardClaimed = candidates.Any(
                    candidate => candidate.CompletionRewardClaimed);
#pragma warning restore CS0612
                canonicalStates.Add(canonical);
            }

            gameData.WorldQuests = canonicalStates;
        }

        private void RejectDuplicateStates()
        {
            if (gameData.WorldQuests.Any(state => state == null ||
                    string.IsNullOrWhiteSpace(state.QuestId)))
            {
                throw new InvalidDataException(
                    "V2 存档包含空任务状态或空 QuestId。");
            }
            string duplicate = gameData.WorldQuests
                .GroupBy(state => state.QuestId, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1)
                ?.Key;
            if (!string.IsNullOrWhiteSpace(duplicate))
            {
                throw new InvalidDataException(
                    $"V2 存档包含重复任务状态：{duplicate}");
            }
        }

        private static List<WorldQuestObjectiveProgressData> CreateProgress(
            WorldQuestStageDefinition stage)
        {
            return stage.Objectives
                .Where(objective => objective != null)
                .Select(objective => new WorldQuestObjectiveProgressData
                {
                    ObjectiveId = objective.ObjectiveId,
                    CurrentAmount = 0,
                    IsComplete = false
                })
                .ToList();
        }

        private static bool IsStageComplete(
            WorldQuestStageDefinition stage,
            WorldQuestStateData state)
        {
            List<WorldQuestObjectiveDefinition> required = stage.Objectives
                .Where(objective => objective != null && !objective.Optional)
                .ToList();
            if (required.Count == 0)
                return false;
            return stage.CompletionMode ==
                WorldQuestObjectiveCompletionMode.All
                ? required.All(objective => state.ObjectiveProgress
                    .Any(progress =>
                        progress.ObjectiveId == objective.ObjectiveId &&
                        progress.IsComplete))
                : required.Any(objective => state.ObjectiveProgress
                    .Any(progress =>
                        progress.ObjectiveId == objective.ObjectiveId &&
                        progress.IsComplete));
        }

        private static bool Matches(
            WorldQuestObjectiveDefinition objective,
            WorldQuestEvent questEvent,
            GameData gameData)
        {
            if (objective.ActorRequirement ==
                    WorldQuestActorRequirement.PlayerParty &&
                !questEvent.CreditedToPlayerParty)
            {
                return false;
            }
            if (objective.ActorRequirement ==
                    WorldQuestActorRequirement.Protagonist &&
                !questEvent.ProtagonistParticipated)
            {
                return false;
            }
            if (objective.ActorRequirement ==
                    WorldQuestActorRequirement.Protagonist &&
                questEvent.Type != WorldQuestEventType.CardDefeated &&
                !string.IsNullOrWhiteSpace(gameData.ProtagonistPersistentId) &&
                questEvent.ActorPersistentId !=
                gameData.ProtagonistPersistentId)
            {
                return false;
            }

            bool typeMatches = objective.Type switch
            {
                WorldQuestObjectiveType.TalkToNpc =>
                    questEvent.Type == WorldQuestEventType.NpcTalked,
                WorldQuestObjectiveType.MarketPurchase =>
                    questEvent.Type ==
                        WorldQuestEventType.MarketTradeCommitted &&
                    questEvent.TradeDirection ==
                        WorldQuestTradeDirection.PlayerBuys,
                WorldQuestObjectiveType.MarketSale =>
                    questEvent.Type ==
                        WorldQuestEventType.MarketTradeCommitted &&
                    questEvent.TradeDirection ==
                        WorldQuestTradeDirection.PlayerSells,
                WorldQuestObjectiveType.EnterLocation =>
                    questEvent.Type == WorldQuestEventType.LocationEntered,
                WorldQuestObjectiveType.DefeatCard =>
                    questEvent.Type == WorldQuestEventType.CardDefeated,
                WorldQuestObjectiveType.ObtainCard =>
                    questEvent.Type == WorldQuestEventType.CardObtained,
                WorldQuestObjectiveType.DeliverCard =>
                    questEvent.Type == WorldQuestEventType.CardDelivered,
                WorldQuestObjectiveType.CraftCard =>
                    questEvent.Type == WorldQuestEventType.CardCrafted,
                WorldQuestObjectiveType.OwnCardCount or
                WorldQuestObjectiveType.CurrencyBalance =>
                    questEvent.Type ==
                        WorldQuestEventType.InventorySnapshotChanged,
                WorldQuestObjectiveType.ProtagonistLevel =>
                    questEvent.Type ==
                        WorldQuestEventType.ProtagonistLevelChanged,
                WorldQuestObjectiveType.WorldDayReached =>
                    questEvent.Type == WorldQuestEventType.WorldDayStarted,
                WorldQuestObjectiveType.DialogueChoice =>
                    questEvent.Type ==
                        WorldQuestEventType.DialogueChoiceSelected,
                _ => false
            };
            if (!typeMatches)
                return false;

            if (!string.IsNullOrWhiteSpace(objective.TargetId) &&
                objective.Type != WorldQuestObjectiveType.EnterLocation &&
                objective.TargetId != questEvent.PrimaryTargetId)
            {
                return false;
            }
            if (objective.Type == WorldQuestObjectiveType.EnterLocation &&
                objective.TargetId != questEvent.LocationId &&
                objective.TargetId != questEvent.PrimaryTargetId)
            {
                return false;
            }
            if (!string.IsNullOrWhiteSpace(objective.SecondaryTargetId) &&
                objective.SecondaryTargetId != questEvent.SecondaryTargetId)
            {
                return false;
            }
            return string.IsNullOrWhiteSpace(objective.ContextId) ||
                   objective.ContextId == questEvent.ContextId ||
                   objective.ContextId == questEvent.LocationId;
        }

        private static int CalculateProgress(
            WorldQuestObjectiveDefinition objective,
            WorldQuestEvent questEvent,
            GameData gameData,
            int currentAmount)
        {
            int value = objective.ProgressMode switch
            {
                WorldQuestProgressMode.AddEventAmount =>
                    currentAmount + questEvent.Amount,
                WorldQuestProgressMode.SetToEventAmount => questEvent.Amount,
                WorldQuestProgressMode.SetToCurrentSnapshot =>
                    GetSnapshotAmount(objective, gameData),
                _ => currentAmount
            };
            return Math.Max(0, Math.Min(objective.RequiredAmount, value));
        }

        private static int GetSnapshotAmount(
            WorldQuestObjectiveDefinition objective,
            GameData gameData)
        {
            return objective.Type switch
            {
                WorldQuestObjectiveType.OwnCardCount or
                WorldQuestObjectiveType.CurrencyBalance =>
                    WorldQuestConditionEvaluator.CountOwnedCards(
                        gameData,
                        objective.TargetId),
                WorldQuestObjectiveType.ProtagonistLevel =>
                    gameData.GetProtagonistData()?.Level ?? 0,
                WorldQuestObjectiveType.WorldDayReached =>
                    gameData.WorldDay <= 0 ? 1 : gameData.WorldDay,
                _ => 0
            };
        }

        private static CardData CreateRewardCard(string definitionId)
        {
            bool medicine = definitionId == "medicine";
            return new CardData
            {
                Id = definitionId,
                UsesLeft = 1,
                CurrentHealth = 15,
                MaximumHealth = 15,
                CurrentNutrition = medicine ? 3 : 0,
                Level = 1,
                CurrentEnergy = 4,
                MaxEnergy = 4
            };
        }

        private static long CalculateNextAvailable(
            WorldQuestDefinition definition,
            long currentHour)
        {
            return definition.RepeatPolicy switch
            {
                WorldQuestRepeatPolicy.Never => -1,
                WorldQuestRepeatPolicy.Daily =>
                    (currentHour / 24L + 1L) * 24L,
                WorldQuestRepeatPolicy.CooldownHours =>
                    currentHour + definition.CooldownHours,
                _ => -1
            };
        }

        private static void ResetForRepeat(WorldQuestStateData state)
        {
            state.Status = WorldQuestStatus.Available;
            state.ActiveStageId = string.Empty;
            state.ObjectiveProgress.Clear();
            state.SelectedChoiceId = string.Empty;
            state.OutcomeId = string.Empty;
            state.StatusReasonId = string.Empty;
            state.SuspendedFromStatus = WorldQuestStatus.Locked;
            state.AvailabilityOverride = false;
            state.AvailableSinceWorldHour = state.NextAvailableWorldHour;
            state.AcceptedWorldHour = -1;
            state.NextAvailableWorldHour = -1;
            state.AppliedOperationIds.Clear();
            state.Revision++;
        }

        private IEnumerable<WorldQuestDefinition> OrderedDefinitions()
        {
            return registry.Definitions
                .OrderByDescending(definition => definition.Priority)
                .ThenBy(definition => definition.Id, StringComparer.Ordinal);
        }

        private int CountRunningQuests()
        {
            return gameData.WorldQuests.Count(state => state != null &&
                state.Status is WorldQuestStatus.Active or
                    WorldQuestStatus.Suspended or
                    WorldQuestStatus.ReadyToTurnIn);
        }

        private void RememberEvent(string eventId)
        {
            gameData.ProcessedWorldQuestEventIds.Add(eventId);
            while (gameData.ProcessedWorldQuestEventIds.Count >
                   MaximumProcessedEventIds)
            {
                gameData.ProcessedWorldQuestEventIds.RemoveAt(0);
            }
        }

        private void CountCascadeOperation()
        {
            cascadeOperations++;
            if (cascadeOperations > MaximumCascadeOperations)
            {
                throw new WorldQuestTransactionException(
                    WorldQuestResultCode.TransactionCycleDetected,
                    "任务联动超过512步，已判定为循环配置。");
            }
        }

        private sealed class WorldQuestTransactionException : Exception
        {
            public WorldQuestResultCode Code { get; }

            public WorldQuestTransactionException(
                WorldQuestResultCode code,
                string message)
                : base(message)
            {
                Code = code;
            }
        }

        private static WorldQuestOperationResult InvalidEffect(
            string questId,
            WorldQuestEffectDefinition effect)
        {
            return WorldQuestOperationResult.Fail(
                WorldQuestResultCode.DefinitionInvalid,
                questId,
                $"任务效果 '{effect?.EffectId}' 配置非法。");
        }

        private static WorldQuestOperationResult MissingTarget(
            string questId,
            WorldQuestEffectDefinition effect)
        {
            return WorldQuestOperationResult.Fail(
                WorldQuestResultCode.DefinitionNotFound,
                questId,
                $"效果目标任务 '{effect?.TargetId}' 不存在。");
        }

        private static int IndexOf<T>(IReadOnlyList<T> values, T value)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (ReferenceEquals(values[index], value))
                    return index;
            }
            return int.MaxValue;
        }

        private static bool StatesEqual(
            WorldQuestStateData left,
            WorldQuestStateData right)
        {
            return JsonConvert.SerializeObject(left) ==
                   JsonConvert.SerializeObject(right);
        }

        private sealed class GameDataSnapshot
        {
            private List<WorldQuestStateData> quests;
            private List<WorldFactData> facts;
            private List<string> eventIds;
            private string trackedQuestId;
            private int worldQuestStateVersion;
            private BackpackData backpack;
            private List<CardData> partyMembers;

            public static GameDataSnapshot Capture(GameData source)
            {
                return new GameDataSnapshot
                {
                    quests = Clone(source.WorldQuests),
                    facts = Clone(source.WorldFacts),
                    eventIds = new List<string>(
                        source.ProcessedWorldQuestEventIds),
                    trackedQuestId = source.TrackedWorldQuestId,
                    worldQuestStateVersion =
                        source.WorldQuestStateVersion,
                    backpack = Clone(source.Backpack),
                    partyMembers = Clone(source.PartyMembers)
                };
            }

            public void Restore(GameData destination)
            {
                destination.WorldQuests = quests;
                destination.WorldFacts = facts;
                destination.ProcessedWorldQuestEventIds = eventIds;
                destination.TrackedWorldQuestId = trackedQuestId;
                destination.WorldQuestStateVersion =
                    worldQuestStateVersion;
                destination.Backpack = backpack;
                destination.PartyMembers = partyMembers;
            }

            private static T Clone<T>(T value)
            {
                if (value == null)
                    return default;
                string json = JsonConvert.SerializeObject(value);
                return JsonConvert.DeserializeObject<T>(json);
            }
        }
    }
}
