using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public readonly struct WorldQuestViewModel
    {
        public string QuestId { get; }
        public string Title { get; }
        public string Description { get; }
        public WorldQuestCategory Category { get; }
        public WorldQuestVisibility Visibility { get; }
        public WorldQuestStatus Status { get; }
        public string StageId { get; }
        public int ObjectiveIndex { get; }
        public string ObjectiveText { get; }
        public int CurrentAmount { get; }
        public int RequiredAmount { get; }
        public string OutcomeId { get; }
        public string StatusReasonId { get; }
        public bool ShowsNpcMarker =>
            Status is WorldQuestStatus.Available or
                WorldQuestStatus.ReadyToTurnIn;

        public WorldQuestViewModel(
            string questId,
            string title,
            string description,
            WorldQuestCategory category,
            WorldQuestVisibility visibility,
            WorldQuestStatus status,
            string stageId,
            int objectiveIndex,
            string objectiveText,
            int currentAmount,
            int requiredAmount,
            string outcomeId,
            string statusReasonId)
        {
            QuestId = questId;
            Title = title;
            Description = description;
            Category = category;
            Visibility = visibility;
            Status = status;
            StageId = stageId;
            ObjectiveIndex = objectiveIndex;
            ObjectiveText = objectiveText;
            CurrentAmount = currentAmount;
            RequiredAmount = requiredAmount;
            OutcomeId = outcomeId;
            StatusReasonId = statusReasonId;
        }
    }

    [DisallowMultipleComponent]
    public sealed class WorldQuestRuntime : MonoBehaviour, IWorldQuestRuntime
    {
        private const float AutosaveQuietPeriodSeconds = 3f;
        private const float AutosaveMinimumIntervalSeconds = 10f;
        private const float AutosaveMaximumDelaySeconds = 30f;

        public static WorldQuestRuntime Instance { get; private set; }

        public event Action<WorldQuestViewModel> OnQuestChanged;
        public event Action<string> OnNotificationRequested;

        private readonly object notificationRequester = new();
        private WorldQuestEngine engine;
        private GameData gameData;
        private bool savePending;
        private bool urgentSavePending;
        private GameDirector saveLifecyclePublisher;
        private float firstAutosaveRequestedAt = -1f;
        private float latestQuestChangeAt = -1f;
        private float lastAutosaveCompletedAt = -1f;
        private Coroutine clearNotificationRoutine;
        private string resolvedNpcId;

        public static WorldQuestRuntime Ensure(GameObject host)
        {
            if (Instance != null)
                return Instance;
            if (host == null)
                return null;
            WorldQuestRuntime runtime = host.GetComponent<WorldQuestRuntime>();
            return runtime != null
                ? runtime
                : host.AddComponent<WorldQuestRuntime>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            SubscribeToSaveLifecycle();
        }

        private void OnDestroy()
        {
            UnsubscribeFromSaveLifecycle();
            UnsubscribeEngine();
            InfoPanel.Instance?.ClearInfoRequest(notificationRequester);
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            bool hasActiveNpcInteraction =
                NpcInteractionManager.Instance?.IsActive == true;
            float now = Time.unscaledTime;
            if (!CanFlushPendingAutosave(
                    savePending,
                    hasActiveNpcInteraction,
                    urgentSavePending,
                    now,
                    firstAutosaveRequestedAt,
                    latestQuestChangeAt,
                    lastAutosaveCompletedAt) ||
                GameDirector.Instance?.GameData == null)
            {
                return;
            }
            GameDirector.Instance.SaveGame();
        }

        internal static bool CanFlushPendingAutosave(
            bool pending,
            bool hasActiveNpcInteraction,
            bool urgent,
            float now,
            float firstRequestedAt,
            float latestChangeAt,
            float lastCompletedAt)
        {
            if (!pending || hasActiveNpcInteraction)
                return false;
            if (urgent)
                return true;
            if (firstRequestedAt >= 0f &&
                now - firstRequestedAt >= AutosaveMaximumDelaySeconds)
            {
                return true;
            }

            bool quietPeriodElapsed = latestChangeAt < 0f ||
                now - latestChangeAt >= AutosaveQuietPeriodSeconds;
            bool minimumIntervalElapsed = lastCompletedAt < 0f ||
                now - lastCompletedAt >= AutosaveMinimumIntervalSeconds;
            return quietPeriodElapsed && minimumIntervalElapsed;
        }

        public void Initialize(GameData data)
        {
            if (data == null)
                return;
            if (ReferenceEquals(gameData, data) && engine != null)
                return;

            SubscribeToSaveLifecycle();
            UnsubscribeEngine();
            ClearPendingAutosave(Time.unscaledTime);
            gameData = data;
            WorldQuestDefinition[] definitions =
                Resources.LoadAll<WorldQuestDefinition>("WorldQuests");
            WorldQuestValidationReport validation =
                WorldQuestDefinitionValidator.Validate(
                    (IEnumerable<WorldQuestDefinition>)definitions);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    "World Quest definitions are invalid: " +
                    string.Join(" | ", validation.Errors));
            }
            engine = new WorldQuestEngine(
                data,
                (IEnumerable<WorldQuestDefinition>)definitions);
            engine.QuestChanged += HandleQuestChanged;
            engine.NotificationRequested += HandleNotification;
            engine.Initialize();
        }

        public void HandleSceneLoaded()
        {
            if (gameData == null || engine == null)
                return;
            WorldQuestOperationResult result =
                engine.ReevaluateAvailability();
            if (!result.Success)
                Debug.LogError(
                    $"[WorldQuest] 场景加载后的任务重评失败：" +
                    result.Message);
        }

        public WorldQuestOperationResult ReportEvent(
            WorldQuestEvent questEvent)
        {
            WorldQuestOperationResult result = engine == null
                ? WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.NotInitialized,
                    string.Empty,
                    "任务运行时尚未初始化。")
                : engine.ReportEvent(questEvent);
            return result;
        }

        public WorldQuestOperationResult TryAccept(string questId)
        {
            if (engine == null)
            {
                return WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.NotInitialized,
                    questId,
                    "任务运行时尚未初始化。");
            }

            WorldQuestOperationResult result = engine.TryAccept(questId);
            if (result.Success)
            {
                BackpackService.NotifyContentsChanged();
                WorldQuestViewModel view = GetViewModel(questId);
                ShowNotification(
                    $"【接受任务：{view.Title}】\n{view.ObjectiveText}");
                ScheduleAutosave(urgent: true);
            }
            return result;
        }

        public WorldQuestOperationResult TryTurnIn(
            string questId,
            string outcomeId,
            string npcId)
        {
            if (engine == null)
            {
                return WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.NotInitialized,
                    questId,
                    "任务运行时尚未初始化。");
            }

            WorldQuestOperationResult result = engine.TryTurnIn(
                questId,
                outcomeId,
                npcId);
            if (!result.Success)
                return result;

            CardInstance activeProtagonist =
                GameDirector.Instance?.FindActiveProtagonistCard();
            CardData protagonistData = gameData?.GetProtagonistData();
            if (activeProtagonist != null && protagonistData != null)
                activeProtagonist.RestoreSavedStats(protagonistData);
            BackpackService.NotifyContentsChanged();
            ScheduleAutosave(urgent: true);
            return result;
        }

        public WorldQuestOperationResult TryTurnIn(
            string questId,
            string outcomeId)
        {
            return TryTurnIn(questId, outcomeId, resolvedNpcId);
        }

        public WorldQuestOperationResult ResolveNpcInteraction(
            string npcId,
            string locationId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.InvalidArgument,
                    string.Empty,
                    "NPC ID 不能为空。");
            }
            resolvedNpcId = npcId;
            return ReportNpcTalked(npcId, locationId);
        }

        public WorldQuestOperationResult SetTrackedQuest(string questId)
        {
            WorldQuestOperationResult result = engine == null
                ? WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.NotInitialized,
                    questId,
                    "任务运行时尚未初始化。")
                : engine.SetTrackedQuest(questId);
            if (result.Success)
            {
                if (result.Code == WorldQuestResultCode.Success)
                    ScheduleAutosave(urgent: false);
                WorldQuestViewModel view = GetViewModel(questId);
                if (!string.IsNullOrWhiteSpace(view.QuestId))
                    OnQuestChanged?.Invoke(view);
            }
            return result;
        }

        public WorldQuestOperationResult ClearTrackedQuest()
        {
            WorldQuestOperationResult result = engine == null
                ? WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.NotInitialized,
                    string.Empty,
                    "任务运行时尚未初始化。")
                : engine.ClearTrackedQuest();
            if (result.Code == WorldQuestResultCode.Success)
                ScheduleAutosave(urgent: false);
            return result;
        }

        public WorldQuestStateData GetState(string questId)
        {
            return engine?.GetState(questId)?.Clone();
        }

        public bool CanTurnIn(string questId, string npcId)
        {
            if (engine == null ||
                !engine.Registry.TryGet(
                    questId,
                    out WorldQuestDefinition definition))
            {
                return false;
            }
            return engine.GetState(questId)?.Status ==
                       WorldQuestStatus.ReadyToTurnIn &&
                   definition.TurnInNpcIds.Contains(npcId);
        }

        public bool ReportMarketPurchase(
            string marketId,
            string commodityId,
            int quantity)
        {
            return ReportEvent(CreateEvent(
                WorldQuestEventType.MarketTradeCommitted,
                commodityId,
                string.Empty,
                marketId,
                quantity,
                WorldQuestTradeDirection.PlayerBuys,
                creditedToParty: true,
                protagonistParticipated: true)).Code ==
                WorldQuestResultCode.Success;
        }

        public bool ReportMarketSale(
            string marketId,
            string commodityId,
            int quantity)
        {
            return ReportEvent(CreateEvent(
                WorldQuestEventType.MarketTradeCommitted,
                commodityId,
                string.Empty,
                marketId,
                quantity,
                WorldQuestTradeDirection.PlayerSells,
                creditedToParty: true,
                protagonistParticipated: true)).Code ==
                WorldQuestResultCode.Success;
        }

        public bool ReportLocationEntered(string locationId)
        {
            return ReportEvent(CreateEvent(
                WorldQuestEventType.LocationEntered,
                locationId,
                string.Empty,
                string.Empty,
                1,
                WorldQuestTradeDirection.None,
                creditedToParty: true,
                protagonistParticipated: true,
                locationId)).Code == WorldQuestResultCode.Success;
        }

        public bool ReportEnemyDefeated(
            string cardDefinitionId,
            bool creditedToPlayerParty)
        {
            return ReportEnemyDefeated(
                cardDefinitionId,
                creditedToPlayerParty,
                creditedToPlayerParty);
        }

        public bool ReportEnemyDefeated(
            string cardDefinitionId,
            bool creditedToPlayerParty,
            bool protagonistParticipated)
        {
            return ReportEvent(CreateEvent(
                WorldQuestEventType.CardDefeated,
                cardDefinitionId,
                string.Empty,
                string.Empty,
                1,
                WorldQuestTradeDirection.None,
                creditedToPlayerParty,
                protagonistParticipated)).Code ==
                WorldQuestResultCode.Success;
        }

        public WorldQuestOperationResult ReportCardObtained(
            string cardDefinitionId,
            int amount,
            string sourceId = "")
        {
            return ReportEvent(CreateEvent(
                WorldQuestEventType.CardObtained,
                cardDefinitionId,
                string.Empty,
                sourceId,
                amount,
                WorldQuestTradeDirection.None,
                creditedToParty: true,
                protagonistParticipated: true));
        }

        public WorldQuestOperationResult ReportCardDelivered(
            string cardDefinitionId,
            string npcId,
            int amount)
        {
            return ReportEvent(CreateEvent(
                WorldQuestEventType.CardDelivered,
                cardDefinitionId,
                npcId,
                string.Empty,
                amount,
                WorldQuestTradeDirection.None,
                creditedToParty: true,
                protagonistParticipated: true));
        }

        public WorldQuestOperationResult ReportCardCrafted(
            string cardDefinitionId,
            string recipeId,
            int amount = 1)
        {
            return ReportEvent(CreateEvent(
                WorldQuestEventType.CardCrafted,
                cardDefinitionId,
                recipeId,
                string.Empty,
                amount,
                WorldQuestTradeDirection.None,
                creditedToParty: true,
                protagonistParticipated: true));
        }

        public WorldQuestOperationResult ReportInventorySnapshotChanged()
        {
            return ReportEvent(CreateEvent(
                WorldQuestEventType.InventorySnapshotChanged,
                string.Empty,
                string.Empty,
                string.Empty,
                1,
                WorldQuestTradeDirection.None,
                creditedToParty: true,
                protagonistParticipated: true));
        }

        public WorldQuestOperationResult ReportProtagonistLevelChanged(
            int level)
        {
            return ReportEvent(CreateEvent(
                WorldQuestEventType.ProtagonistLevelChanged,
                string.Empty,
                string.Empty,
                string.Empty,
                level,
                WorldQuestTradeDirection.None,
                creditedToParty: true,
                protagonistParticipated: true));
        }

        public WorldQuestOperationResult ReportWorldDayStarted(int day)
        {
            return ReportEvent(CreateEvent(
                WorldQuestEventType.WorldDayStarted,
                string.Empty,
                string.Empty,
                string.Empty,
                day,
                WorldQuestTradeDirection.None,
                creditedToParty: true,
                protagonistParticipated: true));
        }

        public WorldQuestOperationResult ReportDialogueChoice(
            string choiceGroupId,
            string choiceId,
            string npcId)
        {
            return ReportEvent(CreateEvent(
                WorldQuestEventType.DialogueChoiceSelected,
                choiceGroupId,
                choiceId,
                npcId,
                1,
                WorldQuestTradeDirection.None,
                creditedToParty: true,
                protagonistParticipated: true));
        }

        public WorldQuestOperationResult ReportNpcTalked(
            string npcId,
            string locationId)
        {
            return ReportEvent(CreateEvent(
                WorldQuestEventType.NpcTalked,
                npcId,
                string.Empty,
                string.Empty,
                1,
                WorldQuestTradeDirection.None,
                creditedToParty: true,
                protagonistParticipated: true,
                locationId));
        }

        public IReadOnlyList<WorldQuestViewModel> GetQuestList()
        {
            if (engine == null)
                return Array.Empty<WorldQuestViewModel>();
            return engine.Registry.Definitions
                .Select(definition => GetViewModel(definition.Id))
                .Where(view => !string.IsNullOrWhiteSpace(view.QuestId))
                .OrderBy(view => view.Category)
                .ThenBy(view => view.Title, StringComparer.Ordinal)
                .ToList();
        }

        public IReadOnlyList<WorldQuestViewModel> GetNpcInteractions(
            string npcId)
        {
            if (engine == null || string.IsNullOrWhiteSpace(npcId))
                return Array.Empty<WorldQuestViewModel>();
            var candidates = engine.Registry.Definitions
                .Select(definition => new
                {
                    Definition = definition,
                    State = engine.GetState(definition.Id),
                    View = GetViewModel(definition.Id)
                })
                .Where(item => IsNpcInteraction(
                    item.Definition,
                    item.State,
                    npcId))
                .ToList();
            var current = candidates.Where(item => item.State.Status is
                WorldQuestStatus.ReadyToTurnIn or
                WorldQuestStatus.Available or
                WorldQuestStatus.Active);
            var latestTerminal = candidates
                .Where(item => item.State.Status is
                    WorldQuestStatus.Completed or
                    WorldQuestStatus.Failed or
                    WorldQuestStatus.Cancelled)
                .OrderByDescending(item => item.State.LastUpdatedWorldHour)
                .ThenByDescending(item => item.Definition.Priority)
                .ThenBy(item => item.Definition.Id, StringComparer.Ordinal)
                .Take(1);
            return current.Concat(latestTerminal)
                .OrderBy(item => InteractionOrder(item.State.Status))
                .ThenByDescending(item => item.Definition.Priority)
                .ThenBy(item => item.Definition.Id, StringComparer.Ordinal)
                .Select(item => item.View)
                .ToList();
        }

        private bool IsNpcInteraction(
            WorldQuestDefinition definition,
            WorldQuestStateData state,
            string npcId)
        {
            if (definition == null || state == null)
                return false;
            switch (state.Status)
            {
                case WorldQuestStatus.ReadyToTurnIn:
                    return definition.TurnInNpcIds.Contains(npcId);
                case WorldQuestStatus.Available:
                    return definition.GiverNpcId == npcId &&
                        (string.IsNullOrWhiteSpace(
                             definition.AcceptLocationId) ||
                         definition.AcceptLocationId ==
                         (gameData?.ActiveLocationId ?? string.Empty));
                case WorldQuestStatus.Active:
                    WorldQuestStageDefinition stage = definition.FindStage(
                        state.ActiveStageId);
                    bool requiresNpc = stage?.Objectives.Any(objective =>
                        objective != null &&
                        objective.Type ==
                        WorldQuestObjectiveType.TalkToNpc &&
                        objective.TargetId == npcId) == true;
                    bool hasReminder = definition.GiverNpcId == npcId &&
                        !string.IsNullOrWhiteSpace(
                            stage?.ActiveReminderText);
                    return requiresNpc || hasReminder;
                case WorldQuestStatus.Completed:
                    return definition.TurnInNpcIds.Contains(npcId) &&
                        !string.IsNullOrWhiteSpace(
                            definition.FindOutcome(state.OutcomeId)
                                ?.ResolutionText);
                case WorldQuestStatus.Failed:
                    return definition.GiverNpcId == npcId &&
                        !string.IsNullOrWhiteSpace(
                            definition.Dialogue?.FailedText);
                case WorldQuestStatus.Cancelled:
                    return definition.GiverNpcId == npcId &&
                        !string.IsNullOrWhiteSpace(
                            definition.Dialogue?.CancelledText);
                default:
                    return false;
            }
        }

        public bool TryGetNpcMarker(
            string npcId,
            out string marker,
            out Color color)
        {
            marker = string.Empty;
            color = Color.white;
            IReadOnlyList<WorldQuestViewModel> interactions =
                GetNpcInteractions(npcId);
            if (interactions.Any(view =>
                    view.Status == WorldQuestStatus.ReadyToTurnIn))
            {
                marker = "?";
                color = new Color32(55, 210, 230, 255);
                return true;
            }
            if (interactions.Any(view =>
                    view.Status == WorldQuestStatus.Available))
            {
                marker = "!";
                color = new Color32(255, 204, 64, 255);
                return true;
            }
            return false;
        }

        public WorldQuestViewModel GetViewModel(string questId)
        {
            if (engine == null ||
                !engine.Registry.TryGet(
                    questId,
                    out WorldQuestDefinition definition))
            {
                return default;
            }
            WorldQuestStateData state = engine.GetState(questId);
            if (state == null)
                return default;

            WorldQuestStageDefinition stage = definition.FindStage(
                state.ActiveStageId);
            int objectiveIndex = stage == null
                ? -1
                : IndexOf(definition.Stages, stage);
            WorldQuestObjectiveDefinition objective = stage?.Objectives
                .FirstOrDefault(candidate =>
                    !candidate.Optional &&
                    !IsObjectiveComplete(state, candidate.ObjectiveId)) ??
                stage?.Objectives.FirstOrDefault();
            WorldQuestObjectiveProgressData progress = objective == null
                ? null
                : state.ObjectiveProgress.FirstOrDefault(value =>
                    value.ObjectiveId == objective.ObjectiveId);

            string text = state.Status switch
            {
                WorldQuestStatus.Locked => "任务尚未开放",
                WorldQuestStatus.Available => definition.Dialogue?.OfferText ??
                    definition.Description,
                WorldQuestStatus.Suspended => stage?.ActiveReminderText ??
                    "任务暂时中断",
                WorldQuestStatus.ReadyToTurnIn =>
                    stage?.ObjectiveSummary ?? "任务可以交付",
                WorldQuestStatus.Completed =>
                    definition.FindOutcome(state.OutcomeId)?.ResolutionText ??
                    "任务已完成",
                WorldQuestStatus.Failed => "任务失败",
                WorldQuestStatus.Cancelled => "任务已取消",
                _ => objective?.DisplayText ?? stage?.ObjectiveSummary ??
                    definition.Description
            };
            return new WorldQuestViewModel(
                definition.Id,
                definition.Title,
                definition.Description,
                definition.Category,
                definition.Visibility,
                state.Status,
                state.ActiveStageId,
                objectiveIndex,
                text,
                progress?.CurrentAmount ?? 0,
                objective?.RequiredAmount ?? 1,
                state.OutcomeId,
                state.StatusReasonId);
        }

        public WorldQuestStatus GetStatus(string questId)
        {
            return engine?.GetState(questId)?.Status ??
                   WorldQuestStatus.Locked;
        }

        public WorldQuestDefinition GetDefinition(string questId)
        {
            return engine != null &&
                   engine.Registry.TryGet(questId, out var definition)
                ? definition
                : null;
        }

        public string ResolveStatusReason(string reasonId)
        {
            if (string.IsNullOrWhiteSpace(reasonId))
                return string.Empty;
            WorldQuestReasonCatalog catalog =
                Resources.Load<WorldQuestReasonCatalog>(
                    "WorldQuests/WorldQuestReasons");
            return catalog != null &&
                   catalog.TryGet(reasonId, out string displayText)
                ? displayText
                : reasonId;
        }

        public IReadOnlyList<WorldQuestOutcomeDefinition>
            GetEligibleOutcomes(string questId)
        {
            return engine?.GetEligibleOutcomes(questId) ??
                Array.Empty<WorldQuestOutcomeDefinition>();
        }

        IReadOnlyList<WorldQuestOutcomeViewModel>
            IWorldQuestRuntime.GetEligibleOutcomes(string questId)
        {
            return GetEligibleOutcomes(questId)
                .Select(outcome => new WorldQuestOutcomeViewModel(
                    outcome.OutcomeId,
                    outcome.ChoiceLabel,
                    outcome.ResolutionText,
                    outcome.Priority))
                .ToList();
        }

        IReadOnlyList<WorldQuestListItemViewModel>
            IWorldQuestRuntime.GetQuestList()
        {
            return GetQuestList()
                .Select(view =>
                {
                    WorldQuestDefinition definition = GetDefinition(
                        view.QuestId);
                    WorldQuestStageDefinition stage = definition?.FindStage(
                        view.StageId);
                    WorldQuestOutcomeDefinition outcome =
                        definition?.FindOutcome(view.OutcomeId);
                    return new WorldQuestListItemViewModel(
                        view,
                        definition?.GiverNpcId ?? string.Empty,
                        stage?.Title ?? string.Empty,
                        outcome?.ChoiceLabel ?? string.Empty,
                        outcome?.ResolutionText ?? string.Empty);
                })
                .ToList();
        }

        public WorldQuestTrackerViewModel GetTrackedQuest()
        {
            string questId = gameData?.TrackedWorldQuestId;
            WorldQuestStateData state = engine?.GetState(questId);
            WorldQuestDefinition definition = GetDefinition(questId);
            if (state == null || definition == null)
                return default;

            WorldQuestStageDefinition stage = definition.FindStage(
                state.ActiveStageId);
            var objectives = new List<
                WorldQuestTrackerObjectiveViewModel>();
            if (stage != null)
            {
                foreach (WorldQuestObjectiveDefinition objective in
                         stage.Objectives.Where(value => value != null))
                {
                    WorldQuestObjectiveProgressData progress =
                        state.ObjectiveProgress?.FirstOrDefault(value =>
                            value != null &&
                            value.ObjectiveId == objective.ObjectiveId);
                    objectives.Add(
                        new WorldQuestTrackerObjectiveViewModel(
                            objective.DisplayText,
                            progress?.CurrentAmount ?? 0,
                            objective.RequiredAmount,
                            progress?.IsComplete == true));
                }
            }

            return new WorldQuestTrackerViewModel(
                definition.Id,
                definition.Title,
                definition.Category,
                state.Status,
                stage?.Title ?? string.Empty,
                state.StatusReasonId,
                definition.TurnInNpcIds.ToArray(),
                objectives);
        }

        private WorldQuestEvent CreateEvent(
            WorldQuestEventType type,
            string primaryTargetId,
            string secondaryTargetId,
            string contextId,
            int amount,
            WorldQuestTradeDirection direction,
            bool creditedToParty,
            bool protagonistParticipated,
            string locationId = null)
        {
            return new WorldQuestEvent(
                Guid.NewGuid().ToString("N"),
                type,
                gameData?.WorldElapsedHours ?? 0,
                locationId ?? gameData?.ActiveLocationId ?? string.Empty,
                gameData?.ProtagonistPersistentId ?? string.Empty,
                creditedToParty,
                protagonistParticipated,
                primaryTargetId ?? string.Empty,
                secondaryTargetId ?? string.Empty,
                contextId ?? string.Empty,
                Mathf.Max(1, amount),
                direction);
        }

        private void HandleQuestChanged(WorldQuestChangedEvent changed)
        {
            WorldQuestViewModel view = GetViewModel(changed.QuestId);
            if (!string.IsNullOrWhiteSpace(view.QuestId))
                OnQuestChanged?.Invoke(view);
            ScheduleAutosave(urgent: false);
        }

        private void ScheduleAutosave(bool urgent)
        {
            float now = Time.unscaledTime;
            if (!savePending)
                firstAutosaveRequestedAt = now;
            savePending = true;
            urgentSavePending |= urgent;
            latestQuestChangeAt = now;
        }

        private void HandleAfterSave(GameData savedGameData)
        {
            if (gameData != null &&
                !ReferenceEquals(gameData, savedGameData))
            {
                return;
            }
            ClearPendingAutosave(Time.unscaledTime);
        }

        private void ClearPendingAutosave(float completedAt)
        {
            savePending = false;
            urgentSavePending = false;
            firstAutosaveRequestedAt = -1f;
            latestQuestChangeAt = -1f;
            lastAutosaveCompletedAt = completedAt;
        }

        private void SubscribeToSaveLifecycle()
        {
            GameDirector currentPublisher = GameDirector.Instance;
            if (ReferenceEquals(
                    saveLifecyclePublisher,
                    currentPublisher))
            {
                return;
            }

            UnsubscribeFromSaveLifecycle();
            if (currentPublisher == null)
                return;
            currentPublisher.OnAfterSave += HandleAfterSave;
            saveLifecyclePublisher = currentPublisher;
        }

        private void UnsubscribeFromSaveLifecycle()
        {
            if (ReferenceEquals(saveLifecyclePublisher, null))
                return;
            saveLifecyclePublisher.OnAfterSave -= HandleAfterSave;
            saveLifecyclePublisher = null;
        }

        private void HandleNotification(string message)
        {
            ShowNotification(message);
        }

        private void ShowNotification(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;
            OnNotificationRequested?.Invoke(message);
            InfoPanel.Instance?.RequestInfoDisplay(
                notificationRequester,
                InfoPriority.Sequence,
                ("任务更新", message));
            if (clearNotificationRoutine != null)
                StopCoroutine(clearNotificationRoutine);
            clearNotificationRoutine =
                StartCoroutine(ClearNotificationAfterDelay());
        }

        private IEnumerator ClearNotificationAfterDelay()
        {
            yield return new WaitForSecondsRealtime(6f);
            InfoPanel.Instance?.ClearInfoRequest(notificationRequester);
            clearNotificationRoutine = null;
        }

        private void UnsubscribeEngine()
        {
            if (engine == null)
                return;
            engine.QuestChanged -= HandleQuestChanged;
            engine.NotificationRequested -= HandleNotification;
            engine = null;
        }

        private static int InteractionOrder(WorldQuestStatus status)
        {
            return status switch
            {
                WorldQuestStatus.ReadyToTurnIn => 0,
                WorldQuestStatus.Available => 1,
                WorldQuestStatus.Active => 2,
                WorldQuestStatus.Suspended => 3,
                _ => 4
            };
        }

        private static bool IsObjectiveComplete(
            WorldQuestStateData state,
            string objectiveId)
        {
            return state.ObjectiveProgress?.Any(progress =>
                progress != null &&
                progress.ObjectiveId == objectiveId &&
                progress.IsComplete) == true;
        }

        private static int IndexOf<T>(IReadOnlyList<T> list, T value)
        {
            for (int index = 0; index < list.Count; index++)
            {
                if (ReferenceEquals(list[index], value))
                    return index;
            }
            return -1;
        }
    }
}
