using System.Collections;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class ProtagonistStatePresenter : MonoBehaviour
    {
        private readonly object downedRequester = new();
        private readonly object feedbackRequester = new();

        private bool lastKnownDowned;
        private bool hasKnownDownedState;
        private bool isConfirmingAction;
        private Coroutine clearFeedbackRoutine;

        public static ProtagonistStatePresenter Ensure(GameObject host)
        {
            if (host == null)
                return null;

            ProtagonistStatePresenter existing =
                FindAnyObjectByType<ProtagonistStatePresenter>();
            return existing != null
                ? existing
                : host.AddComponent<ProtagonistStatePresenter>();
        }

        private void OnEnable()
        {
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnProtagonistProgressed +=
                    HandleProgression;
            }
            BackpackService.Changed += HandleBackpackChanged;
            RefreshDownedState(force: true);
        }

        private void OnDisable()
        {
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnProtagonistProgressed -=
                    HandleProgression;
            }
            BackpackService.Changed -= HandleBackpackChanged;
            InfoPanel.Instance?.ClearInfoRequest(downedRequester);
            InfoPanel.Instance?.ClearInfoRequest(feedbackRequester);
        }

        private void Start()
        {
            // Awake order is not guaranteed. Refresh once all scene objects,
            // including InfoPanel, have completed their Awake callbacks.
            RefreshDownedState(force: true);
        }

        private void Update()
        {
            RefreshDownedState(force: false);
        }

        private void HandleBackpackChanged()
        {
            if (!isConfirmingAction)
                RefreshDownedState(force: true);
        }

        public void RefreshDownedState(bool force)
        {
            CardData protagonist =
                GameDirector.Instance?.GameData?.GetProtagonistData();
            bool isDowned = protagonist?.IsDowned == true;
            if (!force &&
                hasKnownDownedState &&
                isDowned == lastKnownDowned)
            {
                return;
            }

            hasKnownDownedState = true;
            lastKnownDowned = isDowned;
            if (!isDowned)
            {
                isConfirmingAction = false;
                InfoPanel.Instance?.ClearInfoRequest(downedRequester);
                return;
            }

            ShowDownedActions();
        }

        private void ShowDownedActions(string additionalMessage = null)
        {
            GameData gameData = GameDirector.Instance?.GameData;
            if (gameData?.GetProtagonistData()?.IsDowned != true)
                return;

            isConfirmingAction = false;
            bool hasCompanion =
                ProtagonistRecoveryService.CanBeRescued(gameData);
            bool hasMedicine =
                ProtagonistRecoveryService.FindRescueMedicineEntry(gameData) !=
                null;
            string rescueState = hasCompanion && hasMedicine
                ? "可由伙伴消耗一份药品救援。"
                : !hasCompanion
                    ? "队伍中没有仍可行动的伙伴。"
                    : "背包中没有可用药品。";
            string body =
                "主角已经倒地，普通返回已被阻止。\n" +
                rescueState +
                "\n撤退：消耗6小时并丢失最多3枚金币，恢复1点生命。" +
                "\n确认死亡：封存本局纪事并结束当前存档。";
            if (!string.IsNullOrWhiteSpace(additionalMessage))
                body = $"{additionalMessage}\n\n{body}";

            InfoPanel.Instance?.RequestInfoDisplayWithActions(
                downedRequester,
                InfoPriority.Modal,
                ("濒死", body),
                new InfoPanelAction("救援", TryRescue),
                new InfoPanelAction("撤退", ShowRetreatConfirmation),
                new InfoPanelAction("确认死亡", ShowDeathConfirmation));
        }

        private void TryRescue()
        {
            if (GameDirector.Instance?.TryRescueDownedProtagonist() == true)
            {
                RefreshDownedState(force: true);
                ShowTransientFeedback(
                    "救援成功",
                    "主角恢复了3点生命和1点精力。");
                return;
            }

            ShowDownedActions("救援失败：需要一名可行动伙伴和一份药品。");
        }

        private void ShowRetreatConfirmation()
        {
            isConfirmingAction = true;
            InfoPanel.Instance?.RequestInfoDisplayWithActions(
                downedRequester,
                InfoPriority.Modal,
                (
                    "确认撤退",
                    "将消耗6小时，丢失最多3枚金币，并返回世界地图。"
                ),
                new InfoPanelAction(
                    "确认撤退",
                    () => GameDirector.Instance
                        ?.RetreatDownedProtagonist()),
                new InfoPanelAction("返回", () => ShowDownedActions()));
        }

        private void ShowDeathConfirmation()
        {
            isConfirmingAction = true;
            InfoPanel.Instance?.RequestInfoDisplayWithActions(
                downedRequester,
                InfoPriority.Modal,
                (
                    "确认死亡",
                    "该操作会封存本局纪事、删除当前活动存档并返回标题界面。"
                ),
                new InfoPanelAction(
                    "结束本局",
                    () => GameDirector.Instance
                        ?.ConfirmProtagonistDeath("战斗重伤")),
                new InfoPanelAction("返回", () => ShowDownedActions()));
        }

        private void HandleProgression(
            CharacterProgressionNotification notification)
        {
            CardInstance active =
                GameDirector.Instance?.FindActiveProtagonistCard();
            if (notification.LeveledUp)
                active?.PlayLevelUpFeedback();

            string body = notification.Message +
                $"\n获得 {notification.ExperienceGained} 经验" +
                "\n" +
                CharacterProgressionFeedback.BuildExperienceBar(
                    notification.CurrentLevel,
                    notification.CurrentExperience,
                    10);
            ShowTransientFeedback(
                notification.LeveledUp ? "升级！" : "战斗经验",
                body);
        }

        private void ShowTransientFeedback(string header, string body)
        {
            InfoPanel.Instance?.RequestInfoDisplay(
                feedbackRequester,
                InfoPriority.Sequence,
                (header, body));
            if (clearFeedbackRoutine != null)
                StopCoroutine(clearFeedbackRoutine);
            clearFeedbackRoutine = StartCoroutine(ClearFeedbackAfterDelay());
        }

        private IEnumerator ClearFeedbackAfterDelay()
        {
            yield return new WaitForSecondsRealtime(4f);
            InfoPanel.Instance?.ClearInfoRequest(feedbackRequester);
            clearFeedbackRoutine = null;
        }
    }
}
