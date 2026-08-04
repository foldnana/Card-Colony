using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class CombatHudPresenter : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Toggle combatToggle;
        [SerializeField] private TMP_Text combatToggleLabel;
        [SerializeField] private WorldMapLocationView locationView;
        [SerializeField] private CombatLogPresenter combatLog;
        [SerializeField] private TMP_Text actorLabel;
        [SerializeField] private TMP_Text targetLabel;
        [SerializeField] private Button retreatButton;
        [SerializeField] private Button[] skillButtons = new Button[3];
        [SerializeField] private TMP_Text[] skillLabels = new TMP_Text[3];

        private CombatManager subscribedManager;
        private CombatTask activeCombat;
        private readonly CombatTargetingController targeting = new();
        private float nextRefreshAt;
        private bool wasInPlayerCombat;
        private bool locationViewSuppressed;
        private bool locationViewWasVisible;

        private void Awake()
        {
            panel ??= GetComponent<CanvasGroup>();
            combatLog ??= GetComponentInChildren<CombatLogPresenter>(true);
            retreatButton?.onClick.AddListener(QueueRetreat);
            for (int index = 0; index < skillButtons.Length; index++)
            {
                int captured = index;
                skillButtons[index]?.onClick.AddListener(() => QueueSkill(captured));
            }
            combatToggle?.onValueChanged.AddListener(HandleCombatToggleChanged);
            CombatFocusService.FocusChanged += HandleFocusChanged;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            retreatButton?.onClick.RemoveListener(QueueRetreat);
            combatToggle?.onValueChanged.RemoveListener(HandleCombatToggleChanged);
            CombatFocusService.FocusChanged -= HandleFocusChanged;
            Unsubscribe();
        }

        private void Update()
        {
            TrySubscribe();
            CombatTask focused = CombatFocusService.FocusedCombat;
            if (focused?.IsOngoing == true)
                activeCombat = focused;
            else if (activeCombat?.IsOngoing != true)
            {
                activeCombat = CombatFocusService.ChooseDefault(
                    subscribedManager?.ActiveCombats);
                if (activeCombat != null)
                    CombatFocusService.Focus(activeCombat);
            }
            if (Time.unscaledTime >= nextRefreshAt)
            {
                nextRefreshAt = Time.unscaledTime + 0.1f;
                Refresh();
            }
        }

        private void TrySubscribe()
        {
            CombatManager manager = CombatManager.Instance;
            if (manager == null || manager == subscribedManager)
                return;
            Unsubscribe();
            subscribedManager = manager;
            subscribedManager.EventPublished += HandleEvent;
        }

        private void Unsubscribe()
        {
            if (subscribedManager != null)
                subscribedManager.EventPublished -= HandleEvent;
            subscribedManager = null;
        }

        private void HandleEvent(CombatEvent combatEvent)
        {
            if (combatEvent?.Type == CombatEventType.CombatStarted)
            {
                CombatTask started = subscribedManager.ActiveCombats
                    .FirstOrDefault(task => task.SessionId == combatEvent.SessionId);
                if (started != null)
                {
                    activeCombat = CombatFocusService.ChooseDefault(
                        subscribedManager.ActiveCombats);
                    CombatFocusService.Focus(activeCombat);
                }
            }
            else if (combatEvent?.Type == CombatEventType.CombatEnded &&
                     activeCombat?.SessionId == combatEvent.SessionId)
            {
                CombatFocusService.ClearIfFinished(activeCombat);
                activeCombat = null;
            }
            Refresh();
        }

        private void HandleFocusChanged()
        {
            Refresh();
        }

        private void HandleCombatToggleChanged(bool isOn) => Refresh();

        private void Refresh()
        {
            bool inCombat = activeCombat?.IsOngoing == true &&
                activeCombat.PlayerCombatants.Any();
            bool shouldAutoOpen = ShouldAutoOpenCombatToggle(
                wasInPlayerCombat,
                inCombat);
            wasInPlayerCombat = inCombat;
            if (shouldAutoOpen && combatToggle != null && !combatToggle.isOn)
                combatToggle.isOn = true;
            bool logVisible = combatLog?.IsVisible == true;
            bool showCombatPage = ShouldShowCombatPage(
                inCombat,
                logVisible,
                combatToggle == null || combatToggle.isOn);
            if (combatToggleLabel != null)
                combatToggleLabel.text = inCombat || logVisible
                    ? "战斗"
                    : "地点";
            SetVisible(showCombatPage);
            if (!inCombat)
                return;

            CardInstance actor = CombatFocusService.SelectedActor ??
                activeCombat.PlayerCombatants.FirstOrDefault();
            CardInstance target = CombatFocusService.SelectedTarget ??
                activeCombat.LivingEnemiesOf(actor).FirstOrDefault();
            if (actorLabel != null)
                actorLabel.text = actor == null
                    ? "未选择角色"
                    : $"{actor.Definition.DisplayName}\n生命 {actor.CurrentHealth}/{actor.Stats.MaxHealth.Value}    体力 {actor.CurrentEnergy}/{actor.MaxEnergy}\n行动 {actor.Combatant.ActionProgress:0}/100";
            if (targetLabel != null)
                targetLabel.text = target == null
                    ? "未选择敌人"
                    : $"目标：{target.Definition.DisplayName}  生命 {target.CurrentHealth}";

            var skills = CombatSkillService.Resolve(actor)
                .Where(skill => skill != null).Take(3).ToArray() ??
                System.Array.Empty<CombatSkillDefinition>();
            for (int index = 0; index < skillButtons.Length; index++)
            {
                bool exists = index < skills.Length;
                if (skillButtons[index] != null)
                {
                    skillButtons[index].gameObject.SetActive(exists);
                    skillButtons[index].interactable = exists && actor.CurrentEnergy >=
                        skills[index].EnergyCost && actor.Combatant.GetSkillCooldown(
                            skills[index].Id) <= 0f;
                }
                if (exists && skillLabels[index] != null)
                {
                    float cooldown = actor.Combatant.GetSkillCooldown(skills[index].Id);
                    skillLabels[index].text = FormatSkillLabel(
                        skills[index],
                        cooldown,
                        actor.IsDowned,
                        actor.Combatant.IsAttacking,
                        actor.CurrentEnergy);
                }
            }

            if (retreatButton != null)
            {
                retreatButton.interactable = actor != null && !actor.IsDowned &&
                    actor.Combatant?.IsAttacking != true;
                if (retreatButton.transform is RectTransform retreatRect)
                {
                    retreatRect.anchoredPosition = new Vector2(
                        retreatRect.anchoredPosition.x,
                        -166f - skills.Length * 50f);
                }
            }
        }

        private static bool ShouldAutoOpenCombatToggle(
            bool wasInCombat,
            bool isInCombat) => isInCombat && !wasInCombat;

        internal static bool ShouldShowCombatPage(
            bool isInCombat,
            bool isLogVisible,
            bool isToggleOn) => isToggleOn && (isInCombat || isLogVisible);

        private static string FormatSkillLabel(
            CombatSkillDefinition skill,
            float cooldown,
            bool isDowned,
            bool isAttacking,
            int currentEnergy)
        {
            string state = isDowned
                ? "角色倒地"
                : isAttacking
                    ? "正在执行动作"
                    : cooldown > 0f
                        ? $"冷却 {cooldown:0.0}秒"
                        : currentEnergy < skill.EnergyCost
                            ? "精力不足"
                            : $"{skill.PowerMultiplier * 100f:0}% · " +
                              $"{skill.CooldownSeconds:0.#}秒冷却";
            return $"{skill.DisplayName}  {state}";
        }

        private void QueueSkill(int index)
        {
            CardInstance actor = CombatFocusService.SelectedActor ??
                activeCombat?.PlayerCombatants.FirstOrDefault();
            CombatSkillDefinition skill = CombatSkillService.Resolve(actor)
                .Where(value => value != null).Take(3).ElementAtOrDefault(index);
            if (activeCombat == null || actor == null || skill == null)
                return;
            targeting.SubmitSkillToAutomaticTarget(
                activeCombat,
                actor,
                CombatFocusService.SelectedTarget,
                skill);
            Refresh();
        }

        private void QueueRetreat()
        {
            CardInstance actor = CombatFocusService.SelectedActor ??
                activeCombat?.PlayerCombatants.FirstOrDefault();
            if (actor == null || subscribedManager == null)
                return;
            subscribedManager.TryRetreat(actor);
            Refresh();
        }

        private void SetVisible(bool visible)
        {
            if (panel == null)
                return;
            panel.alpha = visible ? 1f : 0f;
            panel.blocksRaycasts = visible;
            panel.interactable = visible;

            if (locationView == null)
                return;
            if (visible)
            {
                if (!locationViewSuppressed)
                {
                    CanvasGroup locationGroup =
                        locationView.GetComponent<CanvasGroup>();
                    locationViewWasVisible = locationGroup != null &&
                        locationGroup.alpha > 0.5f &&
                        locationGroup.blocksRaycasts;
                }
                locationView.ToggleView(false);
                locationViewSuppressed = true;
            }
            else if (locationViewSuppressed)
            {
                locationView.ToggleView(locationViewWasVisible);
                locationViewSuppressed = false;
            }
        }
    }
}
