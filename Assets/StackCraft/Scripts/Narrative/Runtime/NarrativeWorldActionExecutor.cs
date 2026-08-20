using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public sealed class NarrativeWorldActionExecutor :
        INarrativeCommandExecutor
    {
        private readonly IReadOnlyDictionary<string, NarrativeActorHandle>
            actors;
        private readonly NarrativeActorControlService controls;
        private readonly INarrativePresentationService presentation;
        private readonly HashSet<string> temporaryActorRoles =
            new(StringComparer.Ordinal);
        private readonly List<INarrativeCommandOperation> operations = new();
        private readonly Dictionary<string, NarrativeCommandDefinition>
            backgroundCombats = new(StringComparer.Ordinal);
        private readonly HashSet<string> suppressedBackgroundBranches =
            new(StringComparer.Ordinal);
        private CardInstance conflictInitiator;
        private CardInstance conflictTarget;
        private bool disposed;

        public event Action<string> BackgroundBranchRequested;

        public NarrativeWorldActionExecutor(
            IReadOnlyDictionary<string, NarrativeActorHandle> actors,
            NarrativeActorControlService controls,
            INarrativePresentationService presentation)
        {
            this.actors = actors ?? throw new ArgumentNullException(
                nameof(actors));
            this.controls = controls ?? throw new ArgumentNullException(
                nameof(controls));
            this.presentation = presentation;
        }

        public INarrativeCommandOperation Execute(
            NarrativeCommandDefinition command,
            bool completeImmediately)
        {
            if (disposed || command == null)
                return Failure("NarrativeExecutorUnavailable");

            NarrativeActorActionParameters parameters =
                command.ActorActionParameters ??
                new NarrativeActorActionParameters();
            NarrativeActorHandle actor = Resolve(parameters.ActorRole);
            INarrativeCommandOperation operation = command.Type switch
            {
                NarrativeCommandType.AcquireActorControl =>
                    controls.Acquire(actor)
                        ? Success()
                        : Failure("ActorControlAcquireFailed"),
                NarrativeCommandType.ReleaseActorControl =>
                    controls.Release(parameters.ActorRole)
                        ? Success()
                        : Failure("ActorControlReleaseFailed"),
                NarrativeCommandType.MoveToActor => MoveToActor(
                    actor, Resolve(parameters.TargetRole), parameters),
                NarrativeCommandType.MoveToMarker => MoveTo(
                    actor, parameters.MarkerPosition + parameters.Offset,
                    parameters.Duration),
                NarrativeCommandType.FaceActor => FaceActor(
                    actor, Resolve(parameters.TargetRole), parameters.Duration),
                NarrativeCommandType.ReturnToOrigin => ReturnToOrigin(
                    actor, parameters),
                NarrativeCommandType.SpawnActor => SpawnActor(
                    actor, parameters),
                NarrativeCommandType.DespawnActor => DespawnActor(actor),
                NarrativeCommandType.PlayCinematicAttack => CinematicAttack(
                    actor, Resolve(parameters.TargetRole), parameters),
                NarrativeCommandType.BeginConflictInteraction =>
                    BeginConflictInteraction(
                        actor, Resolve(parameters.TargetRole)),
                NarrativeCommandType.EndConflictInteraction =>
                    EndConflictInteraction(),
                NarrativeCommandType.BeginBackgroundCombat =>
                    BeginBackgroundCombat(command),
                NarrativeCommandType.ShowSpeechBubble or
                    NarrativeCommandType.ShowEmote or
                    NarrativeCommandType.EnterVisualNovelMode or
                    NarrativeCommandType.ExitVisualNovelMode or
                    NarrativeCommandType.ShowFullscreenImage or
                    NarrativeCommandType.HideFullscreenImage or
                    NarrativeCommandType.FocusActor or
                    NarrativeCommandType.ShakeCamera =>
                    presentation?.Execute(command, actor, completeImmediately) ??
                    Failure("NarrativePresentationUnavailable"),
                _ => Failure($"UnsupportedPresentationCommand:{command.Type}")
            };
            if (completeImmediately && !operation.IsCompleted)
                operation.CompleteImmediately();
            if (!operation.IsCompleted)
                operations.Add(operation);
            return operation;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            string[] activeBackgroundSessions = backgroundCombats.Keys
                .Select(sessionId => CombatManager.Instance?
                    .ResolveFinalSessionId(sessionId) ?? sessionId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            foreach (INarrativeCommandOperation operation in operations)
                if (!operation.IsCompleted)
                    operation.Cancel("NarrativeFinished");
            operations.Clear();
            if (CombatManager.Instance != null)
                CombatManager.Instance.OutcomePublished -=
                    HandleBackgroundCombatOutcome;
            backgroundCombats.Clear();
            suppressedBackgroundBranches.Clear();
            foreach (CombatTask task in CombatManager.Instance?.ActiveCombats
                         ?.Where(task => task != null && task.IsOngoing &&
                             activeBackgroundSessions.Contains(
                                 task.SessionId,
                                 StringComparer.Ordinal))
                         .ToArray() ?? Array.Empty<CombatTask>())
            {
                task.EndImmediately();
            }
            EndConflictInteraction();
            foreach (string role in new List<string>(temporaryActorRoles))
                DespawnActor(Resolve(role));
            temporaryActorRoles.Clear();
            presentation?.Dispose();
            controls.Dispose();
        }

        private NarrativeActorHandle Resolve(string roleId) =>
            !string.IsNullOrWhiteSpace(roleId) &&
            actors.TryGetValue(roleId, out NarrativeActorHandle actor)
                ? actor
                : null;

        private INarrativeCommandOperation MoveToActor(
            NarrativeActorHandle actor,
            NarrativeActorHandle target,
            NarrativeActorActionParameters parameters)
        {
            if (target?.Card == null)
                return Failure("TargetActorMissing");
            Vector3 targetPosition = target.Card.Stack?.TargetPosition ??
                target.Card.transform.position;
            Vector3 offset = parameters.Offset;
            if (offset.sqrMagnitude <= 0.0001f)
            {
                Vector3 from = actor?.Card != null
                    ? actor.Card.transform.position
                    : targetPosition + Vector3.left;
                Vector3 direction = (from - targetPosition).Flatten();
                if (direction.sqrMagnitude <= 0.0001f)
                    direction = Vector3.left;
                offset = direction.normalized * 1.15f;
            }
            return MoveTo(actor, targetPosition + offset,
                parameters.Duration);
        }

        private INarrativeCommandOperation MoveTo(
            NarrativeActorHandle actor,
            Vector3 destination,
            float duration,
            Action afterMove = null)
        {
            if (actor?.Card == null)
                return Failure("ActorMissing");
            CardInstance card = actor.Card;
            if (Board.Instance != null && card.Stack != null)
            {
                destination = Board.Instance.EnforcePlacementRules(
                    destination, card.Stack);
            }
            destination.y = card.Stack?.TargetPosition.y ??
                card.transform.position.y;
            Vector3 finalPosition = destination;
            Action finish = () =>
            {
                if (card == null)
                    return;
                if (card.Stack != null)
                    card.Stack.SetTargetPosition(finalPosition, instant: true);
                else
                    card.SetTargetInstant(finalPosition);
                afterMove?.Invoke();
            };
            if (duration <= 0f)
            {
                finish();
                return Success();
            }
            Tween tween = card.transform.DOMove(finalPosition, duration)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
            return new TweenNarrativeCommandOperation(tween, finish);
        }

        private INarrativeCommandOperation FaceActor(
            NarrativeActorHandle actor,
            NarrativeActorHandle target,
            float duration)
        {
            if (actor?.Card == null || target?.Card == null)
                return Failure("ActorMissing");

            // World cards are flat presentation objects rather than upright
            // character models. Rotating their transform toward another card
            // makes them turn edge-on to the camera and corrupts the rotation
            // later restored by ReturnToOrigin. Facing remains a semantic
            // staging command until a separate character visual layer exists.
            return Success();
        }

        private INarrativeCommandOperation BeginConflictInteraction(
            NarrativeActorHandle initiator,
            NarrativeActorHandle target)
        {
            if (initiator?.Card == null || target?.Card == null)
                return Failure("ActorMissing");
            NpcInteractionManager interaction =
                NpcInteractionManager.Instance ??
                NpcInteractionManager.Ensure(
                    CombatManager.Instance?.gameObject);
            if (interaction == null)
                return Failure("ConflictInteractionUnavailable");

            if (interaction.IsActive)
            {
                bool isOwnedConflict = interaction.IsConflict &&
                    interaction.Participants.Contains(initiator.Card) &&
                    interaction.Participants.Contains(target.Card);
                if (isOwnedConflict)
                    return Success();
                interaction.EndInteraction();
            }

            if (!interaction.TryStartConflictInteraction(
                    initiator.Card, target.Card))
            {
                return Failure("ConflictInteractionCouldNotStart");
            }
            conflictInitiator = initiator.Card;
            conflictTarget = target.Card;
            return Success();
        }

        private INarrativeCommandOperation EndConflictInteraction()
        {
            NpcInteractionManager interaction =
                NpcInteractionManager.Instance;
            if (interaction != null && interaction.IsConflict &&
                (conflictInitiator == null ||
                 interaction.Participants.Contains(conflictInitiator)) &&
                (conflictTarget == null ||
                 interaction.Participants.Contains(conflictTarget)))
            {
                interaction.EndInteraction();
            }
            conflictInitiator = null;
            conflictTarget = null;
            return Success();
        }

        private INarrativeCommandOperation BeginBackgroundCombat(
            NarrativeCommandDefinition command)
        {
            CombatManager manager = CombatManager.Instance;
            NarrativeInteractionParameters parameters =
                command?.InteractionParameters;
            List<CardInstance> friendly = ResolveCards(
                parameters?.InitiatorRoles);
            List<CardInstance> enemies = ResolveCards(
                parameters?.TargetRoles);
            if (manager == null || friendly.Count == 0 || enemies.Count == 0 ||
                friendly.Any(card => card.Combatant == null) ||
                enemies.Any(card => card.Combatant == null))
            {
                return Failure("BackgroundCombatActorsUnavailable");
            }

            NpcInteractionManager interaction =
                NpcInteractionManager.Instance;
            if (interaction?.IsActive == true &&
                interaction.Participants.Any(friendly.Contains))
            {
                foreach (string roleId in parameters.InitiatorRoles ??
                         Array.Empty<string>())
                {
                    NarrativeActorHandle handle = Resolve(roleId);
                    if (handle?.Card != null && interaction.TryGetReturnPosition(
                            handle.Card, out Vector3 returnPosition))
                    {
                        controls.OverrideOrigin(
                            roleId,
                            returnPosition,
                            handle.Card.transform.rotation);
                    }
                }
                interaction.EndInteractionForCombatTransition(
                    friendly.Concat(enemies));
            }

            string originalSessionId =
                $"narrative-background:{command.CommandId}:" +
                Guid.NewGuid().ToString("N");
            CombatTask task = manager.StartCombat(
                friendly,
                enemies,
                playerIsAttacker: true,
                originalSessionId);
            if (task == null)
                return Failure("BackgroundCombatCouldNotStart");

            task.ConfigureDefeatRules(
                ParseDefeatRule(parameters, "friendlyDefeatRule"),
                ParseDefeatRule(parameters, "enemyDefeatRule"));
            if (backgroundCombats.Count == 0)
                manager.OutcomePublished += HandleBackgroundCombatOutcome;
            backgroundCombats[originalSessionId] = command;
            return Success();
        }

        public List<NarrativeBackgroundCombatData>
            CaptureBackgroundCombats()
        {
            CombatManager manager = CombatManager.Instance;
            return backgroundCombats.Select(pair =>
                new NarrativeBackgroundCombatData
                {
                    OriginalSessionId = pair.Key,
                    FinalSessionId = manager?.ResolveFinalSessionId(pair.Key) ??
                        pair.Key,
                    CommandId = pair.Value?.CommandId ?? string.Empty,
                    ContextId = pair.Value?.InteractionParameters?.ContextId ??
                        string.Empty,
                    InitiatorActorIds = ResolveCards(pair.Value?
                            .InteractionParameters?.InitiatorRoles)
                        .Select(card => card.PersistentId).ToList(),
                    TargetActorIds = ResolveCards(pair.Value?
                            .InteractionParameters?.TargetRoles)
                        .Select(card => card.PersistentId).ToList(),
                    Actors = CaptureBackgroundActors(pair.Value)
                }).ToList();
        }

        private List<NarrativeBackgroundActorData> CaptureBackgroundActors(
            NarrativeCommandDefinition command)
        {
            IEnumerable<string> roles = (command?.InteractionParameters?
                    .InitiatorRoles ?? Array.Empty<string>())
                .Concat(command?.InteractionParameters?.TargetRoles ??
                    Array.Empty<string>())
                .Distinct(StringComparer.Ordinal);
            var saved = new List<NarrativeBackgroundActorData>();
            foreach (string roleId in roles)
            {
                NarrativeActorHandle handle = Resolve(roleId);
                if (handle?.Card == null)
                    continue;
                var actor = new NarrativeBackgroundActorData
                {
                    RoleId = roleId,
                    PersistentId = handle.Card.PersistentId
                };
                if (controls.TryGetOrigin(roleId, out var origin))
                {
                    actor.OriginPosition = new[]
                    {
                        origin.Position.x,
                        origin.Position.y,
                        origin.Position.z
                    };
                    actor.OriginRotation = new[]
                    {
                        origin.Rotation.x,
                        origin.Rotation.y,
                        origin.Rotation.z,
                        origin.Rotation.w
                    };
                }
                saved.Add(actor);
            }
            return saved;
        }

        public void RestoreBackgroundCombats(
            IEnumerable<NarrativeBackgroundCombatData> saved,
            NarrativeDefinition definition)
        {
            CombatManager manager = CombatManager.Instance;
            if (manager == null || definition == null)
                return;
            Dictionary<string, NarrativeCommandDefinition> commands =
                definition.Nodes
                    .SelectMany(node => node?.Commands ??
                        Array.Empty<NarrativeCommandDefinition>())
                    .Where(command => command != null &&
                        !string.IsNullOrWhiteSpace(command.CommandId))
                    .GroupBy(command => command.CommandId,
                        StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First(),
                        StringComparer.Ordinal);
            foreach (NarrativeBackgroundCombatData binding in saved ??
                     Enumerable.Empty<NarrativeBackgroundCombatData>())
            {
                if (binding == null ||
                    string.IsNullOrWhiteSpace(binding.OriginalSessionId) ||
                    !commands.TryGetValue(binding.CommandId, out var command))
                    continue;
                string finalSessionId = string.IsNullOrWhiteSpace(
                        binding.FinalSessionId)
                    ? binding.OriginalSessionId
                    : binding.FinalSessionId;
                CombatTask active = manager.ActiveCombats.FirstOrDefault(
                    task => task?.IsOngoing == true &&
                        string.Equals(task.SessionId, finalSessionId,
                            StringComparison.Ordinal));
                active ??= manager.ActiveCombats.FirstOrDefault(task =>
                    task?.IsOngoing == true &&
                    (binding.InitiatorActorIds ?? new List<string>())
                        .Concat(binding.TargetActorIds ?? new List<string>())
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .All(task.ContainsCombatant));
                if (active == null)
                    continue;
                AttachRestoredActors(
                    command.InteractionParameters?.InitiatorRoles,
                    binding.InitiatorActorIds,
                    active);
                AttachRestoredActors(
                    command.InteractionParameters?.TargetRoles,
                    binding.TargetActorIds,
                    active);
                RestoreActorControl(binding.Actors, active);
                manager.RegisterSessionAlias(
                    binding.OriginalSessionId, active.SessionId);
                backgroundCombats[binding.OriginalSessionId] = command;
            }
            if (backgroundCombats.Count > 0)
            {
                manager.OutcomePublished -= HandleBackgroundCombatOutcome;
                manager.OutcomePublished += HandleBackgroundCombatOutcome;
            }
        }

        private void RestoreActorControl(
            IEnumerable<NarrativeBackgroundActorData> savedActors,
            CombatTask task)
        {
            foreach (NarrativeBackgroundActorData saved in savedActors ??
                     Enumerable.Empty<NarrativeBackgroundActorData>())
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.RoleId))
                    continue;
                NarrativeActorHandle handle = Resolve(saved.RoleId);
                CardInstance card = task.FindParticipant(saved.PersistentId);
                if (handle == null || card == null)
                    continue;
                handle.AttachCard(card);
                controls.Acquire(handle);
                if (saved.OriginPosition?.Length == 3 &&
                    saved.OriginRotation?.Length == 4)
                {
                    controls.OverrideOrigin(
                        saved.RoleId,
                        new Vector3(
                            saved.OriginPosition[0],
                            saved.OriginPosition[1],
                            saved.OriginPosition[2]),
                        new Quaternion(
                            saved.OriginRotation[0],
                            saved.OriginRotation[1],
                            saved.OriginRotation[2],
                            saved.OriginRotation[3]));
                }
            }
        }

        private void AttachRestoredActors(
            IEnumerable<string> roles,
            IReadOnlyList<string> persistentIds,
            CombatTask task)
        {
            string[] roleArray = (roles ?? Array.Empty<string>()).ToArray();
            for (int index = 0; index < roleArray.Length &&
                 index < (persistentIds?.Count ?? 0); index++)
            {
                NarrativeActorHandle handle = Resolve(roleArray[index]);
                CardInstance card = task.FindParticipant(
                    persistentIds[index]);
                if (handle != null && card != null)
                    handle.AttachCard(card);
            }
        }

        private List<CardInstance> ResolveCards(IEnumerable<string> roles)
        {
            return (roles ?? Array.Empty<string>())
                .Select(Resolve)
                .Where(handle => handle?.Card != null)
                .Select(handle => handle.Card)
                .Distinct()
                .ToList();
        }

        private static CombatDefeatRule ParseDefeatRule(
            NarrativeInteractionParameters parameters,
            string key)
        {
            string value = parameters?.Arguments?
                .FirstOrDefault(argument => argument != null &&
                    string.Equals(argument.Key, key,
                        StringComparison.OrdinalIgnoreCase))
                ?.StringValue;
            return Enum.TryParse(value, ignoreCase: true,
                out CombatDefeatRule parsed)
                ? parsed
                : CombatDefeatRule.Lethal;
        }

        private void HandleBackgroundCombatOutcome(CombatOutcome outcome)
        {
            if (outcome == null ||
                !backgroundCombats.Remove(
                    outcome.OriginalSessionId,
                    out NarrativeCommandDefinition command))
            {
                return;
            }
            if (backgroundCombats.Count == 0 && CombatManager.Instance != null)
            {
                CombatManager.Instance.OutcomePublished -=
                    HandleBackgroundCombatOutcome;
            }

            bool suppressBranch = suppressedBackgroundBranches.Remove(
                outcome.OriginalSessionId);
            if (suppressBranch)
                return;

            string outcomeId = outcome.Result switch
            {
                CombatOutcomeResult.Victory => "victory",
                CombatOutcomeResult.Defeat => "defeat",
                CombatOutcomeResult.Retreated => "retreated",
                _ => "aborted"
            };
            NarrativeInteractionOutcomeBranch branch =
                command.InteractionParameters?.OutcomeBranches?
                    .FirstOrDefault(candidate => candidate != null &&
                        string.Equals(candidate.OutcomeId, outcomeId,
                            StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(branch?.TargetNodeId))
                BackgroundBranchRequested?.Invoke(branch.TargetNodeId);
        }

        public void SuppressBackgroundBranchForContext(string contextId)
        {
            if (string.IsNullOrWhiteSpace(contextId))
                return;
            foreach (KeyValuePair<string, NarrativeCommandDefinition> pair in
                     backgroundCombats)
            {
                if (string.Equals(
                        pair.Value?.InteractionParameters?.ContextId,
                        contextId,
                        StringComparison.Ordinal))
                {
                    suppressedBackgroundBranches.Add(pair.Key);
                }
            }
        }

        private INarrativeCommandOperation ReturnToOrigin(
            NarrativeActorHandle actor,
            NarrativeActorActionParameters parameters)
        {
            if (actor == null ||
                !controls.TryGetOrigin(actor.RoleId, out var origin))
                return Failure("ActorOriginMissing");
            return MoveTo(
                actor,
                origin.Position,
                parameters.Duration,
                () =>
                {
                    if (actor.Card != null)
                        actor.Card.transform.rotation = origin.Rotation;
                });
        }

        private INarrativeCommandOperation SpawnActor(
            NarrativeActorHandle actor,
            NarrativeActorActionParameters parameters)
        {
            if (actor == null || actor.Card != null ||
                CardManager.Instance == null)
                return Failure("TemporaryActorSpawnFailed");
            CardDefinition definition = CardManager.Instance.GetDefinitionById(
                actor.CardDefinitionId);
            if (definition == null)
                return Failure("TemporaryActorDefinitionMissing");
            CardInstance card = CardManager.Instance.CreateCardInstance(
                definition,
                ResolveSpawnPosition(parameters),
                CardStack.RefuseAll,
                allowAggressiveInFriendlyMode: true);
            if (card == null)
                return Failure("TemporaryActorSpawnFailed");
            card.MarkNarrativeTemporary();
            actor.AttachCard(card);
            temporaryActorRoles.Add(actor.RoleId);
            return Success();
        }

        private Vector3 ResolveSpawnPosition(
            NarrativeActorActionParameters parameters)
        {
            NarrativeActorHandle target = Resolve(parameters.TargetRole);
            if (target?.Card != null)
            {
                Vector3 targetPosition = target.Card.Stack?.TargetPosition ??
                    target.Card.transform.position;
                return targetPosition + parameters.Offset;
            }
            return parameters.MarkerPosition + parameters.Offset;
        }

        private INarrativeCommandOperation DespawnActor(
            NarrativeActorHandle actor)
        {
            if (actor == null ||
                !temporaryActorRoles.Remove(actor.RoleId))
                return Failure("TemporaryActorMissing");
            CardInstance card = actor.Card;
            actor.AttachCard(null);
            // Formal combat may already have destroyed a temporary enemy.
            // Removing the tracked role is still a successful, idempotent cleanup.
            if (card == null)
                return Success();
            if (card.Stack != null)
                card.Stack.DestroyCard(card);
            else
                UnityEngine.Object.Destroy(card.gameObject);
            return Success();
        }

        private INarrativeCommandOperation CinematicAttack(
            NarrativeActorHandle attacker,
            NarrativeActorHandle target,
            NarrativeActorActionParameters parameters)
        {
            if (attacker?.Card == null || target?.Card == null)
                return Failure("ActorMissing");
            Transform attackerTransform = attacker.Card.transform;
            Transform targetTransform = target.Card.transform;
            Vector3 origin = attackerTransform.position;
            Vector3 direction = (origin - targetTransform.position).Flatten();
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.left;
            Vector3 strikePosition = targetTransform.position +
                direction.normalized * 0.55f;
            float duration = Mathf.Max(0.12f, parameters.Duration);
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(attackerTransform.DOMove(
                strikePosition, duration * 0.35f));
            sequence.Append(attackerTransform.DOPunchRotation(
                new Vector3(0f, 0f, -12f), duration * 0.2f, 8));
            sequence.Join(targetTransform.DOPunchPosition(
                direction.normalized * 0.12f, duration * 0.2f, 8));
            sequence.Append(attackerTransform.DOMove(
                origin, duration * 0.45f));
            return new TweenNarrativeCommandOperation(
                sequence,
                () =>
                {
                    if (attacker.Card != null)
                        attacker.Card.SetTargetInstant(origin);
                });
        }

        private static INarrativeCommandOperation Success() =>
            new ImmediateNarrativeCommandOperation();

        private static INarrativeCommandOperation Failure(string error) =>
            new ImmediateNarrativeCommandOperation(false, error);

        private sealed class TweenNarrativeCommandOperation :
            INarrativeCommandOperation
        {
            private Tween tween;
            private Action finish;
            private NarrativeCommandResult result;

            public TweenNarrativeCommandOperation(Tween tween, Action finish)
            {
                this.tween = tween;
                this.finish = finish;
                if (tween == null)
                {
                    Complete(true, string.Empty);
                    return;
                }
                tween.OnComplete(() => Complete(true, string.Empty));
            }

            public bool IsCompleted { get; private set; }
            public NarrativeCommandResult Result => result;
            public event Action<NarrativeCommandResult> Completed;

            public void CompleteImmediately()
            {
                if (IsCompleted)
                    return;
                tween?.Kill(false);
                Complete(true, string.Empty);
            }

            public void Cancel(string reason)
            {
                if (IsCompleted)
                    return;
                tween?.Kill(false);
                finish = null;
                Complete(false, reason ?? "Cancelled");
            }

            private void Complete(bool success, string error)
            {
                if (IsCompleted)
                    return;
                Tween currentTween = tween;
                tween = null;
                if (success)
                    finish?.Invoke();
                finish = null;
                IsCompleted = true;
                result = new NarrativeCommandResult(success, error);
                Completed?.Invoke(result);
                currentTween?.Kill(false);
            }
        }
    }
}
