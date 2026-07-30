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
        public WorldQuestStatus Status { get; }
        public int ObjectiveIndex { get; }
        public string ObjectiveText { get; }
        public int CurrentAmount { get; }
        public int RequiredAmount { get; }
        public bool ShowsNpcMarker =>
            Status is WorldQuestStatus.Available or
                WorldQuestStatus.ReadyToTurnIn;

        public WorldQuestViewModel(
            string questId,
            string title,
            string description,
            WorldQuestStatus status,
            int objectiveIndex,
            string objectiveText,
            int currentAmount,
            int requiredAmount)
        {
            QuestId = questId;
            Title = title;
            Description = description;
            Status = status;
            ObjectiveIndex = objectiveIndex;
            ObjectiveText = objectiveText;
            CurrentAmount = currentAmount;
            RequiredAmount = requiredAmount;
        }
    }

    [DisallowMultipleComponent]
    public sealed class WorldQuestRuntime : MonoBehaviour
    {
        public static WorldQuestRuntime Instance { get; private set; }

        public event Action<WorldQuestViewModel> OnQuestChanged;
        public event Action<string> OnNotificationRequested;

        private readonly Dictionary<string, WorldQuestDefinition>
            definitions = new();
        private readonly object notificationRequester = new();
        private bool savePending;
        private bool saveAfterNextSceneLoad;
        private string notificationAfterNextSceneLoad;
        private Coroutine clearNotificationRoutine;

        public static WorldQuestRuntime Ensure(GameObject host)
        {
            if (Instance != null)
                return Instance;
            if (host == null)
                return null;

            WorldQuestRuntime runtime =
                host.GetComponent<WorldQuestRuntime>();
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
            LoadDefinitions();
        }

        private void OnDestroy()
        {
            InfoPanel.Instance?.ClearInfoRequest(notificationRequester);
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            if (!savePending ||
                GameDirector.Instance?.GameData == null)
            {
                return;
            }

            savePending = false;
            GameDirector.Instance.SaveGame();
        }

        public void Initialize(GameData gameData)
        {
            if (gameData == null)
                return;

            foreach (WorldQuestDefinition definition in definitions.Values)
            {
                WorldQuestProgressionService.EnsureQuestState(
                    gameData,
                    definition.Id);
            }

            RaiseChanged(RiverbendForestQuestRules.QuestId);
        }

        public void HandleSceneLoaded()
        {
            if (!string.IsNullOrWhiteSpace(
                    notificationAfterNextSceneLoad))
            {
                string message = notificationAfterNextSceneLoad;
                notificationAfterNextSceneLoad = null;
                OnNotificationRequested?.Invoke(message);
                ShowNotification(message);
            }

            if (!saveAfterNextSceneLoad)
                return;

            saveAfterNextSceneLoad = false;
            savePending = true;
        }

        public bool TryAccept(string questId)
        {
            GameData gameData = GameDirector.Instance?.GameData;
            if (!HasDefinition(questId) ||
                !WorldQuestRewardService.TryAcceptAndGrant(
                    gameData,
                    questId))
            {
                return false;
            }

            BackpackService.NotifyContentsChanged();
            Publish(
                questId,
                "【接受任务：林间异响】\n" +
                "前往河湾市场购买1份粮食。\n" +
                "已收到8枚预付金币和1份治疗药品。",
                saveImmediately: true);
            return true;
        }

        public bool ReportMarketPurchase(
            string marketId,
            string commodityId,
            int quantity)
        {
            string questId = RiverbendForestQuestRules.QuestId;
            GameData gameData = GameDirector.Instance?.GameData;
            if (!WorldQuestProgressionService.ReportMarketPurchase(
                    gameData,
                    questId,
                    marketId,
                    commodityId,
                    playerBuys: true,
                    quantity))
            {
                return false;
            }

            Publish(
                questId,
                "【任务更新：林间异响】\n" +
                "补给已经准备好，前往低语森林。",
                saveImmediately: true);
            return true;
        }

        public bool ReportLocationEntered(string locationId)
        {
            string questId = RiverbendForestQuestRules.QuestId;
            GameData gameData = GameDirector.Instance?.GameData;
            if (!WorldQuestProgressionService.ReportLocationEntered(
                    gameData,
                    questId,
                    locationId))
            {
                return false;
            }

            RaiseChanged(questId);
            notificationAfterNextSceneLoad =
                "【任务更新：林间异响】\n" +
                "调查森林，并击败1只史莱姆。";
            saveAfterNextSceneLoad = true;
            return true;
        }

        public bool ReportEnemyDefeated(
            string cardDefinitionId,
            bool creditedToPlayerParty)
        {
            string questId = RiverbendForestQuestRules.QuestId;
            GameData gameData = GameDirector.Instance?.GameData;
            if (!WorldQuestProgressionService.ReportEnemyDefeated(
                    gameData,
                    questId,
                    cardDefinitionId,
                    creditedToPlayerParty))
            {
                return false;
            }

            Publish(
                questId,
                "【任务更新：林间异响】\n" +
                "已经查明森林中的威胁。返回河湾村向村长汇报。",
                saveImmediately: false);
            savePending = true;
            return true;
        }

        public bool CanTurnIn(string questId, string npcId)
        {
            return HasDefinition(questId) &&
                   WorldQuestProgressionService.CanTurnIn(
                       GameDirector.Instance?.GameData,
                       questId,
                       npcId);
        }

        public bool TryTurnIn(string questId, string npcId)
        {
            GameData gameData = GameDirector.Instance?.GameData;
            if (!HasDefinition(questId) ||
                !WorldQuestRewardService.TryCompleteAndGrant(
                    gameData,
                    questId,
                    npcId))
            {
                return false;
            }

            CardInstance activeProtagonist =
                GameDirector.Instance?.FindActiveProtagonistCard();
            CardData protagonistData = gameData.GetProtagonistData();
            if (activeProtagonist != null && protagonistData != null)
                activeProtagonist.RestoreSavedStats(protagonistData);
            BackpackService.NotifyContentsChanged();
            Publish(
                questId,
                "【任务完成：林间异响】\n" +
                "获得10枚金币和10点经验。",
                saveImmediately: true);
            return true;
        }

        public WorldQuestViewModel GetViewModel(string questId)
        {
            if (!definitions.TryGetValue(
                    questId,
                    out WorldQuestDefinition definition) ||
                GameDirector.Instance?.GameData == null)
            {
                return default;
            }

            WorldQuestStateData state =
                WorldQuestProgressionService.EnsureQuestState(
                    GameDirector.Instance.GameData,
                    questId);
            string objectiveText;
            int requiredAmount = 1;
            if (state.Status == WorldQuestStatus.Available)
            {
                objectiveText = "与河湾村村长交谈";
            }
            else if (state.Status == WorldQuestStatus.Completed)
            {
                objectiveText = "任务已完成";
            }
            else
            {
                int objectiveIndex = Mathf.Clamp(
                    state.ObjectiveIndex,
                    0,
                    definition.Objectives.Count - 1);
                WorldQuestObjectiveDefinition objective =
                    definition.Objectives[objectiveIndex];
                objectiveText = objective.Text;
                requiredAmount = objective.RequiredAmount;
            }

            return new WorldQuestViewModel(
                definition.Id,
                definition.Title,
                definition.Description,
                state.Status,
                state.ObjectiveIndex,
                objectiveText,
                state.CurrentAmount,
                requiredAmount);
        }

        public WorldQuestStatus GetStatus(
            string questId)
        {
            GameData gameData = GameDirector.Instance?.GameData;
            if (gameData == null || !HasDefinition(questId))
                return WorldQuestStatus.Available;

            return WorldQuestProgressionService.EnsureQuestState(
                gameData,
                questId).Status;
        }

        private bool HasDefinition(string questId)
        {
            return !string.IsNullOrWhiteSpace(questId) &&
                   definitions.ContainsKey(questId);
        }

        private void LoadDefinitions()
        {
            definitions.Clear();
            foreach (WorldQuestDefinition definition in
                     Resources.LoadAll<WorldQuestDefinition>("WorldQuests"))
            {
                if (definition != null &&
                    !string.IsNullOrWhiteSpace(definition.Id))
                {
                    definitions.TryAdd(definition.Id, definition);
                }
            }
        }

        private void Publish(
            string questId,
            string message,
            bool saveImmediately)
        {
            RaiseChanged(questId);
            OnNotificationRequested?.Invoke(message);
            ShowNotification(message);
            if (saveImmediately)
                GameDirector.Instance?.SaveGame();
        }

        private void RaiseChanged(string questId)
        {
            WorldQuestViewModel viewModel = GetViewModel(questId);
            if (!string.IsNullOrWhiteSpace(viewModel.QuestId))
                OnQuestChanged?.Invoke(viewModel);
        }

        private void ShowNotification(string message)
        {
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
    }
}
