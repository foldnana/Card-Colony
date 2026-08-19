using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    /// <summary>
    /// Owns one deterministic combat session and coordinates rules, commands and presentation.
    /// </summary>
    public sealed class CombatTask
    {
        private static long nextCreatedSequence;

        public List<CardInstance> Attackers { get; private set; }
        public List<CardInstance> Defenders { get; private set; }
        public bool PlayerIsAttacker { get; private set; }
        public bool IsOngoing => Phase != CombatPhase.Finished;
        public CombatRect Rect { get; private set; }
        public string SessionId { get; private set; }
        public CombatPhase Phase { get; private set; }
        public uint RandomState => random.State;
        public long CreatedSequence { get; private set; }
        public IEnumerable<CombatCommand> QueuedCommands => commands.Commands;
        public IReadOnlyCollection<string> ResolvedDefeatIds => resolvedDefeatIds;
        public IEnumerable<CardInstance> PlayerCombatants => combatants.Where(
            card => card?.Definition?.Faction == CardFaction.Player &&
                card.CurrentHealth > 0);

        public IEnumerable<CardInstance> LivingEnemiesOf(CardInstance actor)
        {
            return actor == null
                ? Enumerable.Empty<CardInstance>()
                : GetEnemies(actor).Where(card => card != null && card.CurrentHealth > 0);
        }

        private readonly List<CardInstance> combatants = new();
        private readonly HashSet<CardInstance> participants = new();
        private readonly HashSet<string> resolvedDefeatIds = new();
        private readonly HashSet<string> defeatedActorIds =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> retreatedPlayerIds =
            new(StringComparer.Ordinal);
        private readonly Dictionary<CardInstance, long> joinSequences = new();
        private readonly CombatCommandQueue commands = new();
        private readonly CombatActionScheduler scheduler = new();
        private readonly CombatActionResolver resolver = new();
        private readonly CombatRewardService rewardService = new();
        private readonly CombatPresentationController presentation;
        private readonly MonoBehaviour coroutineRunner;
        private readonly CombatRandom random;
        private readonly CombatItemReservationService reservations;
        private Coroutine actionCoroutine;
        private long eventSequence;
        private long actionSequence;
        private long pendingActionSequence;
        private long nextJoinSequence;
        private CombatActionPlan pendingPlan;
        private CardInstance pendingActor;
        private CardInstance pendingTarget;
        private float presentationElapsed;
        private bool impactApplied;

        public CombatTask(
            List<CardInstance> attackers,
            List<CardInstance> defenders,
            bool playerIsAttacker,
            CombatRect rect)
            : this(
                attackers,
                defenders,
                playerIsAttacker,
                rect,
                Guid.NewGuid().ToString("N"),
                CreateSeed(nextCreatedSequence + 1),
                ++nextCreatedSequence,
                restored: false)
        {
        }

        internal CombatTask(
            List<CardInstance> attackers,
            List<CardInstance> defenders,
            bool playerIsAttacker,
            CombatRect rect,
            string sessionId,
            uint randomState,
            long createdSequence,
            bool restored)
        {
            coroutineRunner = CombatManager.Instance;
            presentation = new CombatPresentationController();
            Attackers = attackers ?? new List<CardInstance>();
            Defenders = defenders ?? new List<CardInstance>();
            PlayerIsAttacker = playerIsAttacker;
            Rect = rect;
            SessionId = string.IsNullOrWhiteSpace(sessionId)
                ? Guid.NewGuid().ToString("N")
                : sessionId;
            CreatedSequence = createdSequence > 0
                ? createdSequence
                : ++nextCreatedSequence;
            nextCreatedSequence = Math.Max(nextCreatedSequence, CreatedSequence);
            random = new CombatRandom(randomState);
            reservations = new CombatItemReservationService(BackpackService.Current);
            Phase = CombatPhase.Starting;

            combatants.AddRange(Attackers.Where(card => card != null));
            combatants.AddRange(Defenders.Where(card => card != null));
            foreach (CardInstance card in combatants)
            {
                participants.Add(card);
                joinSequences[card] = nextJoinSequence++;
                if (!restored)
                    card.Combatant?.InitializeCombatActionProgress();
            }

            Phase = CombatPhase.Running;
            Publish(restored
                ? CombatEventType.CombatRestored
                : CombatEventType.CombatStarted);
        }

        private static uint CreateSeed(long createdSequence)
        {
            int locationSeed = 1;
            if (GameDirector.Instance?.GameData?.TryGetScene(out SceneData scene) == true)
                locationSeed = scene.RandomSeed == 0 ? 1 : scene.RandomSeed;
            int worldMinute = TimeManager.Instance?.CurrentWorldMinute ?? 0;
            return CombatRandom.MixSeed(
                locationSeed,
                worldMinute,
                createdSequence);
        }

        public void Update(float delta)
        {
            if (!IsOngoing || delta <= 0f)
                return;

            foreach (CardInstance card in combatants.ToList())
            {
                if (card?.Combatant == null || card.IsDowned)
                    continue;
                card.Combatant.TickCombatRuntime(delta);
                card.Combatant.SetActionProgress(scheduler.AddProgress(
                    card.Combatant.ActionProgress,
                    card.Stats.AttackSpeed.Value,
                    delta));
            }


            if (Phase == CombatPhase.ResolvingAction)
                presentationElapsed += delta;

            if (Phase == CombatPhase.ResolvingAction &&
                presentationElapsed >= 2f)
            {
                if (actionCoroutine != null && coroutineRunner != null)
                    coroutineRunner.StopCoroutine(actionCoroutine);
                actionCoroutine = null;
                NotifyPresentationImpact(pendingActionSequence);
                NotifyPresentationCompleted(pendingActionSequence);
                return;
            }

            if (Phase == CombatPhase.Running)
                ResolveNextReadyAction();
        }

        public CombatCommandResult QueueCommand(CombatCommand command) =>
            TrySubmitCommand(command);

        public CombatCommandResult TrySubmitCommand(CombatCommand command)
        {
            if (!IsOngoing)
                return CombatCommandResult.Failure(
                    CombatCommandResultCode.SessionNotRunning,
                    "战斗已经结束。");
            if (command == null)
                return CombatCommandResult.Failure(
                    CombatCommandResultCode.ActorNotFound,
                    "行动命令为空。");

            command.SessionId = SessionId;

            CardInstance actor = FindCombatant(command.ActorId);
            if (actor == null)
                return CombatCommandResult.Failure(
                    CombatCommandResultCode.ActorNotFound,
                    "角色不在当前战斗中。");
            if (actor.CurrentHealth <= 0)
                return CombatCommandResult.Failure(
                    CombatCommandResultCode.ActorUnavailable,
                    "倒下的角色无法行动。");
            if (actor.Definition.Faction != CardFaction.Player)
                return CombatCommandResult.Failure(
                    CombatCommandResultCode.ActorNotControllable,
                    "只能为玩家角色安排主动行动。");

            if (command.Type == CombatCommandType.Retreat)
            {
                if (Phase != CombatPhase.Running || actor.IsDowned ||
                    actor.Combatant?.IsAttacking == true)
                    return CombatCommandResult.Failure(
                        CombatCommandResultCode.RetreatNotAllowed,
                        "角色当前不能撤退。");
                TryResolveRetreat(actor);
                return CombatCommandResult.Success();
            }
            if (command.Type == CombatCommandType.ForcedAttack)
                return CombatCommandResult.Failure(
                    CombatCommandResultCode.ActorNotControllable,
                    "强制攻击不能由界面提交。");

            CardInstance target = string.IsNullOrWhiteSpace(command.TargetId)
                ? null
                : FindCombatant(command.TargetId);
            CombatCommand previous = commands.PeekForActor(command.ActorId);
            bool transferringSameItem = previous?.Type == CombatCommandType.UseItem &&
                command.Type == CombatCommandType.UseItem &&
                previous.BackpackEntryId == command.BackpackEntryId;
            if (transferringSameItem)
                reservations.ReleaseCommand(previous.CommandId);

            CombatCommandResult validation = ValidateSpecificCommand(
                command,
                actor,
                target);
            if (!validation.Accepted)
            {
                if (transferringSameItem)
                    reservations.TryReserve(
                        previous.BackpackEntryId,
                        SessionId,
                        previous.CommandId);
                return validation;
            }

            if (previous != null)
            {
                reservations.ReleaseCommand(previous.CommandId);
                Publish(
                    CombatEventType.ActionCancelled,
                    actor,
                    target,
                    detail: "行动已被新的指令替换。");
            }

            commands.Queue(command);
            Publish(CombatEventType.ActionQueued, actor, target);
            return CombatCommandResult.Success();
        }

        private CombatCommandResult ValidateSpecificCommand(
            CombatCommand command,
            CardInstance actor,
            CardInstance target)
        {
            switch (command.Type)
            {
                case CombatCommandType.UseSkill:
                {
                    CombatSkillDefinition skill = FindSkill(actor, command.DefinitionId);
                    if (skill == null)
                        return CombatCommandResult.Failure(
                            CombatCommandResultCode.SkillNotKnown,
                            "角色没有这个技能。");
                    if (skill.TargetRule != CombatTargetRule.SingleLivingEnemy)
                        return CombatCommandResult.Failure(
                            CombatCommandResultCode.InvalidTarget,
                            "这个技能的目标规则尚不受支持。");
                    if (target == null)
                        return CombatCommandResult.Failure(
                            CombatCommandResultCode.TargetNotFound,
                            "没有找到技能目标。");
                    if (IsAlly(actor, target) || target.CurrentHealth <= 0)
                        return CombatCommandResult.Failure(
                            CombatCommandResultCode.InvalidTarget,
                            "请选择一个存活的敌人。");
                    if (actor.CurrentEnergy < skill.EnergyCost)
                        return CombatCommandResult.Failure(
                            CombatCommandResultCode.InsufficientEnergy,
                            "体力不足。");
                    if (actor.Combatant.GetSkillCooldown(skill.Id) > 0f)
                        return CombatCommandResult.Failure(
                            CombatCommandResultCode.SkillOnCooldown,
                            "技能尚未冷却。");
                    break;
                }
                case CombatCommandType.UseItem:
                    CombatItemDefinition item = FindCombatItem(command);
                    if (item == null)
                        return CombatCommandResult.Failure(
                            BackpackService.Current?.Find(command.BackpackEntryId) == null
                                ? CombatCommandResultCode.BackpackEntryNotFound
                                : CombatCommandResultCode.ItemNotCombatUsable,
                            "这个物品不能在战斗中使用。");
                    if (item.TargetRule != CombatTargetRule.SingleLivingAlly ||
                        item.EffectType != CombatItemEffectType.Heal)
                        return CombatCommandResult.Failure(
                            CombatCommandResultCode.ItemNotCombatUsable,
                            "这个物品的战斗效果尚不受支持。");
                    if (target == null || !IsAlly(actor, target) || target.CurrentHealth <= 0)
                        return CombatCommandResult.Failure(
                            CombatCommandResultCode.InvalidTarget,
                            "战斗药品只能用于存活的友方角色。");
                    if (target.CurrentHealth >= target.Stats.MaxHealth.Value)
                        return CombatCommandResult.Failure(
                            CombatCommandResultCode.InvalidTarget,
                            "目标的生命已经全满。");
                    if (!reservations.TryReserve(
                            command.BackpackEntryId,
                            SessionId,
                            command.CommandId))
                        return CombatCommandResult.Failure(
                            CombatCommandResultCode.ItemAlreadyReserved,
                            "物品不存在或已被其他行动预留。");
                    break;
                case CombatCommandType.BasicAttack:
                    if (target != null && IsAlly(actor, target))
                        return CombatCommandResult.Failure(
                            CombatCommandResultCode.InvalidTarget,
                            "普通攻击不能以友方为目标。");
                    break;
                case CombatCommandType.Retreat:
                case CombatCommandType.ForcedAttack:
                    break;
            }

            return CombatCommandResult.Success();
        }

        private void ResolveNextReadyAction()
        {
            if (Attackers.Count == 0 || Defenders.Count == 0)
            {
                EndCombat();
                return;
            }

            CardInstance actor = combatants
                .Where(card => card != null && card.CurrentHealth > 0 &&
                    !card.IsDowned &&
                    !card.IsBeingDragged && card.Combatant != null &&
                    card.Combatant.ActionProgress >= CombatActionScheduler.ActionThreshold)
                .OrderByDescending(card => card.Combatant.ActionProgress)
                .ThenBy(card => joinSequences.TryGetValue(card, out long value)
                    ? value
                    : long.MaxValue)
                .FirstOrDefault();
            if (actor == null)
                return;

            CombatCommand command = commands.TakeForActor(actor.PersistentId) ??
                CreateBasicAttackCommand(actor);
            CardInstance target = ResolveCommandTarget(actor, command);
            if (target == null && command.Type == CombatCommandType.UseSkill)
            {
                Publish(
                    CombatEventType.ActionCancelled,
                    actor,
                    detail: "技能目标已失效，改为普通攻击。");
                command = CreateBasicAttackCommand(actor);
                target = ResolveCommandTarget(actor, command);
            }
            else if (target == null && command.Type == CombatCommandType.UseItem)
            {
                FallbackToBasicAttack(
                    actor,
                    command,
                    "物品目标已失效，改为普通攻击。");
                return;
            }
            if (target == null)
            {
                actor.Combatant.ConsumeActionProgress();
                reservations.ReleaseCommand(command.CommandId);
                return;
            }

            if (command.Type == CombatCommandType.UseSkill)
            {
                CombatSkillDefinition skill = FindSkill(actor, command.DefinitionId);
                if (skill == null || actor.CurrentEnergy < skill.EnergyCost ||
                    actor.Combatant.GetSkillCooldown(skill.Id) > 0f)
                {
                    FallbackToBasicAttack(
                        actor,
                        command,
                        "技能当前不可用，改为普通攻击。");
                    return;
                }
            }
            else if (command.Type == CombatCommandType.UseItem &&
                     (FindCombatItem(command) == null ||
                      !reservations.IsReservedBy(
                          command.BackpackEntryId,
                          SessionId,
                          command.CommandId) ||
                      target.CurrentHealth >= target.Stats.MaxHealth.Value))
            {
                FallbackToBasicAttack(
                    actor,
                    command,
                    "物品当前不可用，改为普通攻击。");
                return;
            }

            StartAction(actor, target, command);
        }

        private void FallbackToBasicAttack(
            CardInstance actor,
            CombatCommand failedCommand,
            string reason)
        {
            reservations.ReleaseCommand(failedCommand?.CommandId);
            Publish(
                CombatEventType.ActionCancelled,
                actor,
                detail: reason);
            CombatCommand fallback = CreateBasicAttackCommand(actor);
            CardInstance target = ResolveCommandTarget(actor, fallback);
            if (target != null)
                StartAction(actor, target, fallback);
            else
                actor?.Combatant?.ConsumeActionProgress();
        }

        private CombatCommand CreateBasicAttackCommand(CardInstance actor)
        {
            return new CombatCommand
            {
                Type = CombatCommandType.BasicAttack,
                ActorId = actor.PersistentId
            };
        }

        private CardInstance ResolveCommandTarget(
            CardInstance actor,
            CombatCommand command)
        {
            CardInstance requested = FindCombatant(command.TargetId);
            bool needsAlly = command.Type == CombatCommandType.UseItem;
            if (requested != null && requested.CurrentHealth > 0 &&
                IsAlly(actor, requested) == needsAlly)
                return requested;

            if (command.Type == CombatCommandType.UseItem)
                return null;
            if (command.Type == CombatCommandType.UseSkill &&
                FindSkill(actor, command.DefinitionId)?.RetargetIfInvalid != true)
                return null;

            List<CardInstance> candidates = (needsAlly
                    ? GetAllies(actor)
                    : GetEnemies(actor))
                .Where(card => card != null && card.CurrentHealth > 0)
                .ToList();
            return candidates.Count == 0
                ? null
                : candidates[random.NextInt(0, candidates.Count)];
        }

        private void StartAction(
            CardInstance actor,
            CardInstance target,
            CombatCommand command)
        {
            Phase = CombatPhase.ResolvingAction;
            actor.Combatant.SetAttackingState(true);

            if (command.Type == CombatCommandType.UseSkill)
            {
                CombatSkillDefinition skill = FindSkill(actor, command.DefinitionId);
                if (skill == null || !actor.TrySpendEnergy(skill.EnergyCost))
                {
                    actor.Combatant.SetAttackingState(false);
                    Phase = CombatPhase.Running;
                    FallbackToBasicAttack(
                        actor,
                        command,
                        "技能消耗提交失败，改为普通攻击。");
                    return;
                }
                actor.Combatant.SetSkillCooldown(skill.Id, skill.CooldownSeconds);
            }

            Publish(CombatEventType.ActionStarted, actor, target);

            if (command.Type == CombatCommandType.UseSkill)
            {
                CombatSkillDefinition skill = FindSkill(actor, command.DefinitionId);
                Publish(
                    CombatEventType.SkillUsed,
                    actor,
                    target,
                    definitionId: skill.Id,
                    detail: skill.DisplayName);
            }

            CombatSkillDefinition plannedSkill =
                command.Type == CombatCommandType.UseSkill
                    ? FindSkill(actor, command.DefinitionId)
                    : null;
            pendingPlan = command.Type == CombatCommandType.UseItem
                ? resolver.PlanHealing(
                    command,
                    Mathf.Min(
                        FindCombatItem(command)?.Magnitude ?? 0,
                        Mathf.Max(0,
                            Mathf.RoundToInt(target.Stats.MaxHealth.Value) -
                            target.CurrentHealth)))
                : resolver.PlanAttack(
                    command,
                    actor.Stats.Attack.Value,
                    target.Stats.Defense.Value,
                    actor.Stats.Accuracy.Value,
                    target.Stats.Dodge.Value,
                    actor.Stats.CriticalChance.Value,
                    actor.Stats.CriticalMultiplier.Value,
                    actor.Definition.CombatType,
                    target.Definition.CombatType,
                    CombatManager.Instance?.AdvantageMultiplier ?? 1.5f,
                    CombatManager.Instance?.DisadvantageMultiplier ?? 0.75f,
                    random,
                    plannedSkill?.PowerMultiplier ?? 1f,
                    plannedSkill?.FlatPower ?? 0,
                    plannedSkill?.CanCritical ?? true);
            pendingActor = actor;
            pendingTarget = target;
            impactApplied = false;
            pendingActionSequence = ++actionSequence;
            presentationElapsed = 0f;

            if (coroutineRunner == null || !Application.isPlaying)
            {
                ApplyPendingImpact();
                FinishAction(actor, command);
                return;
            }

            actionCoroutine = coroutineRunner.StartCoroutine(
                presentation.Play(
                    actor,
                    target,
                    command,
                    pendingPlan,
                    Rect,
                    () => CombatManager.Instance?.NotifyPresentationImpact(
                        SessionId,
                        pendingActionSequence),
                    () => CombatManager.Instance?.NotifyPresentationCompleted(
                        SessionId,
                        pendingActionSequence)));
        }

        internal void NotifyPresentationImpact(long sequence)
        {
            if (Phase != CombatPhase.ResolvingAction ||
                sequence != pendingActionSequence)
                return;
            ApplyPendingImpact();
        }

        internal void NotifyPresentationCompleted(long sequence)
        {
            if (Phase != CombatPhase.ResolvingAction ||
                sequence != pendingActionSequence)
                return;
            ApplyPendingImpact();
            FinishAction(pendingActor, pendingPlan?.Command);
        }

        private void ApplyPendingImpact()
        {
            if (impactApplied)
                return;
            impactApplied = true;
            CardInstance actor = pendingActor;
            CardInstance target = pendingTarget;
            CombatCommand command = pendingPlan?.Command;
            if (actor == null || target == null || !combatants.Contains(target))
                return;

            if (command.Type == CombatCommandType.UseItem)
            {
                if (!reservations.ConsumeCommand(command.CommandId, out _))
                    return;
                int healing = pendingPlan.Healing;
                target.Heal(healing);
                Publish(
                    CombatEventType.ItemUsed,
                    actor,
                    target,
                    definitionId: command.DefinitionId,
                    detail: "药品");
                Publish(CombatEventType.HealingApplied, actor, target, healing);
                BackpackService.NotifyContentsChanged();
                return;
            }

            HitResult result = pendingPlan.Hit;

            if (!result.IsHit)
            {
                Publish(CombatEventType.AttackMissed, actor, target);
            }
            else
            {
                target.TakeDamage(result.Damage);
                if (result.IsCritical)
                {
                    Publish(
                        CombatEventType.CriticalHit,
                        actor,
                        target,
                        result.Damage,
                        hitType: result.Type,
                        advantage: result.Advantage);
                }
                Publish(
                    CombatEventType.DamageApplied,
                    actor,
                    target,
                    result.Damage,
                    hitType: result.Type,
                    advantage: result.Advantage);
            }

            if (target.CurrentHealth <= 0)
                ResolveDefeat(target);
        }

        private void ResolveDefeat(CardInstance defeated)
        {
            if (defeated == null)
                return;
            string persistentId = defeated.PersistentId ?? defeated.GetInstanceID().ToString();
            string key = SessionId + ":" + persistentId;
            if (!resolvedDefeatIds.Add(key))
                return;
            defeatedActorIds.Add(persistentId);

            CancelQueuedCommandForActor(
                defeated,
                "ActorDefeated");
            Attackers.Remove(defeated);
            Defenders.Remove(defeated);
            combatants.Remove(defeated);

            if (ProtagonistRules.IsProtagonist(defeated))
            {
                Publish(CombatEventType.CombatantDowned, target: defeated);
                defeated.EnterDownedState();
                Rect?.UpdateLayout();
                return;
            }

            Publish(CombatEventType.CombatantDefeated, target: defeated);
            CombatRewardResult reward = rewardService.ResolveDefeat(
                defeated,
                participants);
            if (reward.Experience > 0)
                Publish(
                    CombatEventType.ExperienceGranted,
                    reward.Protagonist,
                    defeated,
                    reward.Experience);
            defeated.Kill();
            Rect?.UpdateLayout();
        }

        private void FinishAction(CardInstance actor, CombatCommand command)
        {
            reservations.ReleaseCommand(command?.CommandId);
            if (actor?.Combatant != null)
            {
                actor.Combatant.SetAttackingState(false);
                actor.Combatant.ConsumeActionProgress();
            }
            Publish(CombatEventType.ActionCompleted, actor);
            actionCoroutine = null;
            pendingPlan = null;
            pendingActor = null;
            pendingTarget = null;
            impactApplied = false;
            if (Attackers.Count == 0 || Defenders.Count == 0)
                EndCombat();
            else
                Phase = CombatPhase.Running;
        }

        public bool AddCombatants(List<CardInstance> newCombatants)
        {
            if (newCombatants == null || newCombatants.Count == 0)
                return false;
            CardStack sourceStack = newCombatants[0]?.Stack;
            bool added = false;
            foreach (CardInstance card in newCombatants.Where(card => card != null))
            {
                if (combatants.Count >= 16)
                    break;
                List<CardInstance> side = card.Definition.Faction switch
                {
                    CardFaction.Player => PlayerIsAttacker ? Attackers : Defenders,
                    CardFaction.Mob => PlayerIsAttacker ? Defenders : Attackers,
                    _ => null
                };
                if (side == null || combatants.Contains(card))
                    continue;
                side.Add(card);
                combatants.Add(card);
                participants.Add(card);
                joinSequences[card] = nextJoinSequence++;
                card.Combatant.EnterCombat(this);
                card.Combatant.InitializeCombatActionProgress();
                Publish(CombatEventType.CombatantJoined, card);
                added = true;
            }
            if (!added)
                return false;
            Rect?.UpdateLayout();
            CombatManager.Instance?.CheckAndMergeCombats(this);
            CardManager.Instance?.ResolveOverlaps(Rect, sourceStack);
            return true;
        }

        public void RemoveCombatant(CardInstance card)
        {
            if (!IsOngoing || card == null)
                return;
            bool removed = Attackers.Remove(card) | Defenders.Remove(card);
            combatants.Remove(card);
            CancelQueuedCommandForActor(card, "ActorRemoved");
            if (!removed)
                return;
            Rect?.UpdateLayout();
            if (Attackers.Count == 0 || Defenders.Count == 0)
                EndCombat();
        }

        public bool Flee(CardInstance card) => TryResolveRetreat(card);

        internal bool TryResolveRetreat(CardInstance card)
        {
            if (!IsOngoing || Phase != CombatPhase.Running || card == null ||
                card.Definition.Faction != CardFaction.Player ||
                card.CurrentHealth <= 0 || card.IsDowned ||
                card.Combatant?.IsAttacking == true ||
                !combatants.Contains(card))
                return false;

            CombatCommand queued = commands.TakeForActor(card.PersistentId);
            if (queued != null)
            {
                reservations.ReleaseCommand(queued.CommandId);
                Publish(
                    CombatEventType.ActionCancelled,
                    card,
                    detail: "撤退前已取消准备中的行动。");
            }

            List<CardInstance> enemies = GetEnemies(card)
                .Where(enemy => enemy.CurrentHealth > 0)
                .ToList();
            float chance = CombatResolutionRules.CalculateRetreatChance(
                card.Stats.Dodge.Value,
                enemies.Count);
            Publish(CombatEventType.RetreatAttempted, card, chance: chance);
            if (random.NextFloat() <= chance)
            {
                Attackers.Remove(card);
                Defenders.Remove(card);
                combatants.Remove(card);
                commands.CancelForActor(card.PersistentId);
                card.Combatant.LeaveCombat();
                card.Combatant.GrantReaggroProtection(3f);
                if (!string.IsNullOrWhiteSpace(card.PersistentId))
                    retreatedPlayerIds.Add(card.PersistentId);
                Publish(CombatEventType.RetreatSucceeded, card, chance: chance);
                Rect?.UpdateLayout();
                if (Attackers.Count == 0 || Defenders.Count == 0)
                    EndCombat();
                return true;
            }

            card.Combatant.SetActionProgress(0f);
            Publish(CombatEventType.RetreatFailed, card, chance: chance);
            CardInstance counterAttacker = enemies
                .OrderByDescending(enemy => enemy.Combatant?.ActionProgress ?? 0f)
                .ThenBy(enemy => joinSequences.TryGetValue(enemy, out long value)
                    ? value
                    : long.MaxValue)
                .FirstOrDefault();
            if (counterAttacker != null)
            {
                counterAttacker.Combatant.SetActionProgress(Mathf.Max(
                    CombatActionScheduler.ActionThreshold,
                    counterAttacker.Combatant.ActionProgress));
                StartAction(counterAttacker, card, new CombatCommand
                {
                    Type = CombatCommandType.ForcedAttack,
                    ActorId = counterAttacker.PersistentId,
                    TargetId = card.PersistentId
                });
            }
            return false;
        }

        public void EndImmediately() => EndCombat(
            CombatOutcomeResult.Aborted,
            "EndedImmediately");

        private void EndCombat(
            CombatOutcomeResult? forcedResult = null,
            string forcedReason = null)
        {
            if (Phase == CombatPhase.Finished)
                return;
            Phase = CombatPhase.Ending;
            if (actionCoroutine != null && coroutineRunner != null)
            {
                coroutineRunner.StopCoroutine(actionCoroutine);
                actionCoroutine = null;
            }
            reservations.ReleaseSession(SessionId);
            commands.Clear();
            CombatOutcomeResult outcome = forcedResult ?? DetermineOutcome();
            string endReason = forcedReason ?? outcome switch
            {
                CombatOutcomeResult.Victory => "EnemiesDefeated",
                CombatOutcomeResult.Defeat => "PlayerDefeated",
                CombatOutcomeResult.Retreated => "PlayerRetreated",
                _ => "CombatAborted"
            };
            string[] survivors = combatants
                .Where(card => card != null && card.CurrentHealth > 0 &&
                    !card.IsDowned &&
                    !string.IsNullOrWhiteSpace(card.PersistentId))
                .Select(card => card.PersistentId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Publish(CombatEventType.CombatEnded);
            if (Rect != null)
            {
                Rect.Close();
                Rect = null;
            }
            foreach (CardInstance card in Attackers.Concat(Defenders)
                         .Where(card => card != null).Distinct().ToList())
            {
                card.Combatant.LeaveCombat();
                Vector3 boardPosition =
                    CombatRect.GetBoardReturnPosition(card);
                CardStack stack = new(card, boardPosition);
                CardManager.Instance?.RegisterStack(stack);
                Vector3 position = Board.Instance != null
                    ? Board.Instance.EnforcePlacementRules(boardPosition, stack)
                    : boardPosition;
                stack.SetTargetPosition(position);
            }
            Phase = CombatPhase.Finished;
            CombatManager.Instance?.PublishOutcome(
                SessionId,
                outcome,
                endReason,
                survivors,
                defeatedActorIds);
        }

        private CombatOutcomeResult DetermineOutcome()
        {
            bool playerAlive = combatants.Any(card =>
                card != null && card.Definition?.Faction == CardFaction.Player &&
                card.CurrentHealth > 0 && !card.IsDowned);
            bool mobAlive = combatants.Any(card =>
                card != null && card.Definition?.Faction == CardFaction.Mob &&
                card.CurrentHealth > 0 && !card.IsDowned);
            return CombatOutcomeRules.Resolve(
                playerAlive,
                mobAlive,
                retreatedPlayerIds.Count > 0,
                forceAbort: false);
        }

        public void CleanUpForMerge()
        {
            if (Phase == CombatPhase.Finished)
                return;
            if (actionCoroutine != null && coroutineRunner != null)
                coroutineRunner.StopCoroutine(actionCoroutine);
            actionCoroutine = null;
            if (Rect != null)
            {
                Rect.Close();
                Rect = null;
            }
            Phase = CombatPhase.Finished;
        }

        internal void CompletePendingActionImmediately()
        {
            if (Phase != CombatPhase.ResolvingAction)
                return;
            if (actionCoroutine != null && coroutineRunner != null)
                coroutineRunner.StopCoroutine(actionCoroutine);
            actionCoroutine = null;
            ApplyPendingImpact();
            FinishAction(pendingActor, pendingPlan?.Command);
        }

        internal void ResetJoinOrderForMerge()
        {
            joinSequences.Clear();
            nextJoinSequence = 0;
        }

        internal void CompleteJoinOrderForMerge()
        {
            foreach (CardInstance card in combatants.Where(card => card != null))
            {
                if (!joinSequences.ContainsKey(card))
                    joinSequences[card] = nextJoinSequence++;
            }
        }

        internal void RestoreRuntime(CombatData data)
        {
            if (data == null)
                return;
            foreach (CombatantRuntimeData runtime in data.RuntimeStates ?? new())
            {
                CardInstance card = FindCombatant(runtime.PersistentId);
                if (card?.Combatant == null)
                    continue;
                card.Combatant.SetActionProgress(runtime.ActionProgress);
                joinSequences[card] = runtime.JoinSequence;
                card.Combatant.GrantReaggroProtection(
                    runtime.RetreatProtectionRemaining);
                foreach (CombatSkillCooldownData cooldown in
                         runtime.SkillCooldowns ?? new())
                    card.Combatant.SetSkillCooldown(
                        cooldown.SkillId,
                        cooldown.RemainingSeconds);
            }
            foreach (CombatCommand command in data.QueuedCommands ?? new())
            {
                if (IsRestorableCommand(command))
                {
                    command.SessionId = SessionId;
                    commands.Queue(command);
                }
                else
                {
                    reservations.ReleaseCommand(command?.CommandId);
                }
            }
            foreach (string key in data.ResolvedDefeatIds ?? new())
            {
                resolvedDefeatIds.Add(key);
                int separator = key?.LastIndexOf(':') ?? -1;
                if (separator >= 0 && separator < key.Length - 1)
                    defeatedActorIds.Add(key[(separator + 1)..]);
            }
        }

        internal long GetJoinSequence(CardInstance card, long fallback)
        {
            return card != null && joinSequences.TryGetValue(card, out long value)
                ? value
                : fallback;
        }

        private bool IsRestorableCommand(CombatCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.CommandId))
                return false;
            CardInstance actor = FindCombatant(command.ActorId);
            if (actor?.Definition?.Faction != CardFaction.Player ||
                actor.CurrentHealth <= 0 || actor.IsDowned)
                return false;

            CardInstance target = FindCombatant(command.TargetId);
            switch (command.Type)
            {
                case CombatCommandType.BasicAttack:
                    return target != null && target.CurrentHealth > 0 &&
                        !IsAlly(actor, target);
                case CombatCommandType.UseSkill:
                    CombatSkillDefinition skill = FindSkill(
                        actor,
                        command.DefinitionId);
                    return skill != null &&
                        target != null && target.CurrentHealth > 0 &&
                        !IsAlly(actor, target);
                case CombatCommandType.UseItem:
                    BackpackEntryData entry = BackpackService.Current?.Find(
                        command.BackpackEntryId);
                    return FindCombatItem(command) != null &&
                        target != null && target.CurrentHealth > 0 &&
                        IsAlly(actor, target) && entry?.IsReserved == true &&
                        entry.ReservationOwnerId == SessionId &&
                        entry.ReservationCommandId == command.CommandId;
                default:
                    return false;
            }
        }

        private void CancelQueuedCommandForActor(
            CardInstance card,
            string reason)
        {
            if (card == null)
                return;
            CombatCommand cancelled = commands.TakeForActor(card.PersistentId);
            if (cancelled == null)
                return;
            reservations.ReleaseCommand(cancelled.CommandId);
            Publish(
                CombatEventType.ActionCancelled,
                card,
                definitionId: cancelled.DefinitionId,
                detail: reason);
        }

        internal void ImportRuntimeFrom(CombatTask source)
        {
            if (source == null)
                return;
            foreach (CardInstance participant in source.participants)
            {
                if (participant != null)
                    participants.Add(participant);
            }
            foreach (CardInstance card in source.joinSequences
                         .OrderBy(pair => pair.Value)
                         .Select(pair => pair.Key))
            {
                if (card != null && combatants.Contains(card) &&
                    !joinSequences.ContainsKey(card))
                    joinSequences[card] = nextJoinSequence++;
            }
            foreach (CombatCommand command in source.QueuedCommands
                         .OrderBy(value => value.RequestedSequence))
            {
                command.SessionId = SessionId;
                CombatCommand existing = commands.PeekForActor(command.ActorId);
                if (existing == null ||
                    command.RequestedSequence >= existing.RequestedSequence)
                    commands.Queue(command);
            }
            foreach (string key in source.ResolvedDefeatIds)
                resolvedDefeatIds.Add(key);
            defeatedActorIds.UnionWith(source.defeatedActorIds);
            retreatedPlayerIds.UnionWith(source.retreatedPlayerIds);

            BackpackData backpack = BackpackService.Current;
            if (backpack?.Entries != null && source.SessionId != SessionId)
            {
                foreach (BackpackEntryData entry in backpack.Entries.Where(
                             value => value?.ReservationOwnerId == source.SessionId))
                    entry.ReservationOwnerId = SessionId;
            }
        }

        internal void PublishMerged()
        {
            Publish(CombatEventType.CombatMerged);
        }

        private CardInstance FindCombatant(string persistentId)
        {
            return string.IsNullOrWhiteSpace(persistentId)
                ? null
                : combatants.FirstOrDefault(card => card?.PersistentId == persistentId);
        }

        internal bool ContainsCombatant(string persistentId) =>
            FindCombatant(persistentId) != null;

        private bool IsAlly(CardInstance first, CardInstance second)
        {
            return first != null && second != null &&
                (Attackers.Contains(first) == Attackers.Contains(second));
        }

        private List<CardInstance> GetEnemies(CardInstance actor) =>
            Attackers.Contains(actor) ? Defenders : Attackers;

        private List<CardInstance> GetAllies(CardInstance actor) =>
            Attackers.Contains(actor) ? Attackers : Defenders;

        private static CombatSkillDefinition FindSkill(
            CardInstance actor,
            string skillId)
        {
            return CombatSkillService.Resolve(actor)
                .FirstOrDefault(skill => skill != null && skill.Id == skillId);
        }

        private static CombatItemDefinition FindCombatItem(CombatCommand command)
        {
            BackpackEntryData entry = BackpackService.Current?
                .Find(command?.BackpackEntryId);
            if (entry?.Card == null)
                return null;
            CardDefinition definition = CardManager.Instance?.GetDefinitionById(
                entry.Card.Id) ?? Resources.LoadAll<CardDefinition>("Cards")
                .FirstOrDefault(value => value != null && value.Id == entry.Card.Id);
            CombatItemDefinition item = definition?.CombatItemDefinition;
            if (item == null ||
                (!string.IsNullOrWhiteSpace(command.DefinitionId) &&
                 command.DefinitionId != item.Id))
                return null;
            return item;
        }

        private void Publish(
            CombatEventType type,
            CardInstance actor = null,
            CardInstance target = null,
            int amount = 0,
            float chance = 0f,
            string definitionId = null,
            HitType hitType = HitType.Normal,
            CombatTypeAdvantage advantage = CombatTypeAdvantage.None,
            string detail = null)
        {
            CombatManager.Instance?.PublishEvent(new CombatEvent
            {
                SessionId = SessionId,
                Sequence = ++eventSequence,
                Timestamp = Time.realtimeSinceStartup,
                Type = type,
                SourceId = actor?.PersistentId,
                SourceName = actor?.Definition?.DisplayName,
                TargetId = target?.PersistentId,
                TargetName = target?.Definition?.DisplayName,
                DefinitionId = definitionId,
                Value = amount,
                Chance = chance,
                HitType = hitType,
                Advantage = advantage,
                Detail = detail
            });
        }

    }
}
