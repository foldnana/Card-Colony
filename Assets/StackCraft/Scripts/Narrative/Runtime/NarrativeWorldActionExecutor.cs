using System;
using System.Collections.Generic;
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
        private bool disposed;

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
            foreach (INarrativeCommandOperation operation in operations)
                if (!operation.IsCompleted)
                    operation.Cancel("NarrativeFinished");
            operations.Clear();
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
            Vector3 delta = (target.Card.transform.position -
                actor.Card.transform.position).Flatten();
            if (delta.sqrMagnitude <= 0.0001f)
                return Success();
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            Vector3 euler = actor.Card.transform.eulerAngles;
            Quaternion rotation = Quaternion.Euler(euler.x, yaw, euler.z);
            if (duration <= 0f)
            {
                actor.Card.transform.rotation = rotation;
                return Success();
            }
            Tween tween = actor.Card.transform
                .DORotateQuaternion(rotation, duration)
                .SetUpdate(true);
            return new TweenNarrativeCommandOperation(
                tween,
                () =>
                {
                    if (actor.Card != null)
                        actor.Card.transform.rotation = rotation;
                });
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
            if (actor?.Card == null ||
                !temporaryActorRoles.Remove(actor.RoleId))
                return Failure("TemporaryActorMissing");
            CardInstance card = actor.Card;
            actor.AttachCard(null);
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
