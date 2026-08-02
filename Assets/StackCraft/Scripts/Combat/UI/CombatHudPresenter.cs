using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class CombatHudPresenter : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Toggle combatToggle;
        [SerializeField] private TMP_Text combatToggleLabel;
        [SerializeField] private TMP_Text actorLabel;
        [SerializeField] private TMP_Text targetLabel;
        [SerializeField] private Button basicAttackButton;
        [SerializeField] private Button[] skillButtons = new Button[3];
        [SerializeField] private TMP_Text[] skillLabels = new TMP_Text[3];

        private CombatManager subscribedManager;
        private CombatTask activeCombat;
        private readonly CombatTargetingController targeting = new();
        private float nextRefreshAt;

        private void Awake()
        {
            panel ??= GetComponent<CanvasGroup>();
            basicAttackButton?.onClick.AddListener(QueueBasicAttack);
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
            basicAttackButton?.onClick.RemoveListener(QueueBasicAttack);
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
            CancelTargetingOnBlankClick();
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
            if (targeting.IsSelectingTarget)
                targeting.TryAcceptSelectedCard(CombatFocusService.LastSelectedCard);
            Refresh();
        }

        private void HandleCombatToggleChanged(bool isOn)
        {
            if (!isOn)
                targeting.Cancel();
            Refresh();
        }

        private void CancelTargetingOnBlankClick()
        {
            if (!targeting.IsSelectingTarget || !Input.GetMouseButtonDown(0) ||
                EventSystem.current?.IsPointerOverGameObject() == true)
                return;
            Camera camera = Camera.main;
            if (camera == null)
                return;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            bool clickedCard = Physics.RaycastAll(ray, 1000f)
                .Any(hit => hit.collider.GetComponentInParent<CardInstance>() != null);
            if (!clickedCard)
                targeting.Cancel();
        }

        private void Refresh()
        {
            bool inCombat = activeCombat?.IsOngoing == true &&
                activeCombat.PlayerCombatants.Any();
            if (combatToggleLabel != null)
                combatToggleLabel.text = inCombat ? "战斗" : "地点";
            SetVisible(inCombat && (combatToggle == null || combatToggle.isOn));
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
                    string reason = actor.IsDowned
                        ? "角色倒地"
                        : actor.Combatant.IsAttacking
                            ? "正在执行动作"
                            : cooldown > 0f
                                ? $"冷却 {cooldown:0.0}秒"
                                : actor.CurrentEnergy < skills[index].EnergyCost
                                    ? "精力不足"
                                    : $"消耗{skills[index].EnergyCost}精力";
                    skillLabels[index].text =
                        $"{skills[index].DisplayName}  {reason}";
                }
            }
        }

        private void QueueBasicAttack()
        {
            CardInstance actor = CombatFocusService.SelectedActor;
            CardInstance target = CombatFocusService.SelectedTarget;
            if (activeCombat == null || actor == null || target == null)
                return;
            targeting.SubmitBasicAttack(activeCombat, actor, target);
        }

        private void QueueSkill(int index)
        {
            CardInstance actor = CombatFocusService.SelectedActor;
            CardInstance target = CombatFocusService.SelectedTarget;
            CombatSkillDefinition skill = CombatSkillService.Resolve(actor)
                .Where(value => value != null).Take(3).ElementAtOrDefault(index);
            if (activeCombat == null || actor == null || target == null || skill == null)
                return;
            if (targeting.IsSelectingTarget &&
                targeting.PendingDefinitionId == skill.Id)
            {
                targeting.Cancel();
                Refresh();
                return;
            }
            targeting.BeginSkillTargeting(activeCombat, actor, skill);
            if (targetLabel != null)
                targetLabel.text = $"为 {skill.DisplayName} 选择一个敌人";
        }

        private void SetVisible(bool visible)
        {
            if (panel == null)
                return;
            panel.alpha = visible ? 1f : 0f;
            panel.blocksRaycasts = visible;
            panel.interactable = visible;
        }
    }
}
