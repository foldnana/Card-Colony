using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public sealed class CombatTargetingController
    {
        private CombatTask pendingTask;
        private CardInstance pendingActor;
        private CombatSkillDefinition pendingSkill;
        private readonly List<CardInstance> highlightedTargets = new();

        public bool IsSelectingTarget => pendingSkill != null;
        public string PendingDefinitionId => pendingSkill?.Id;

        public CombatCommandResult SubmitBasicAttack(
            CombatTask task,
            CardInstance actor,
            CardInstance target)
        {
            return Submit(task, actor, target, CombatCommandType.BasicAttack, null);
        }

        public CombatCommandResult SubmitSkill(
            CombatTask task,
            CardInstance actor,
            CardInstance target,
            CombatSkillDefinition skill)
        {
            return Submit(task, actor, target, CombatCommandType.UseSkill, skill?.Id);
        }

        public bool BeginSkillTargeting(
            CombatTask task,
            CardInstance actor,
            CombatSkillDefinition skill)
        {
            if (task?.IsOngoing != true || actor == null || skill == null ||
                actor.Combatant?.CurrentCombatTask != task)
                return false;
            pendingTask = task;
            pendingActor = actor;
            pendingSkill = skill;
            ClearHighlights();
            highlightedTargets.AddRange(task.LivingEnemiesOf(actor)
                .Where(target => target != null));
            foreach (CardInstance target in highlightedTargets)
                target.SetHighlighted(true, new Color(0.18f, 0.9f, 1f));
            return true;
        }

        public CombatCommandResult TryAcceptSelectedCard(CardInstance selected)
        {
            if (!IsSelectingTarget)
                return CombatCommandResult.Failure(
                    CombatCommandResultCode.InvalidTarget,
                    "当前没有等待选择目标的行动。");
            if (selected?.Combatant?.CurrentCombatTask != pendingTask ||
                selected.CurrentHealth <= 0 ||
                selected.Definition?.Faction == CardFaction.Player)
                return CombatCommandResult.Failure(
                    CombatCommandResultCode.InvalidTarget,
                    "请选择当前战斗中的存活敌人。");

            CombatCommandResult result = SubmitSkill(
                pendingTask,
                pendingActor,
                selected,
                pendingSkill);
            if (result.Accepted)
                Cancel();
            return result;
        }

        public void Cancel()
        {
            ClearHighlights();
            pendingTask = null;
            pendingActor = null;
            pendingSkill = null;
        }

        private void ClearHighlights()
        {
            foreach (CardInstance target in highlightedTargets)
                target?.SetHighlighted(false);
            highlightedTargets.Clear();
        }

        private static CombatCommandResult Submit(
            CombatTask task,
            CardInstance actor,
            CardInstance target,
            CombatCommandType type,
            string definitionId)
        {
            if (task == null || CombatManager.Instance == null)
                return CombatCommandResult.Failure(
                    CombatCommandResultCode.SessionNotFound,
                    "没有可用的战斗。");
            return CombatManager.Instance.TrySubmitCommand(new CombatCommand
            {
                SessionId = task.SessionId,
                Type = type,
                ActorId = actor?.PersistentId,
                TargetId = target?.PersistentId,
                DefinitionId = definitionId
            });
        }
    }
}
