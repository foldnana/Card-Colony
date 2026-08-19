using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public sealed class NarrativePresentationService :
        INarrativePresentationService
    {
        private readonly NarrativePresentationView view;
        private readonly CameraController cameraController;
        private readonly Vector3 cameraOrigin;
        private readonly bool hasCameraOrigin;
        private readonly List<GameObject> callouts = new();
        private readonly List<INarrativeCommandOperation> operations = new();
        private bool disposed;

        public NarrativePresentationService(
            NarrativePresentationView view,
            CameraController cameraController)
        {
            this.view = view;
            this.cameraController = cameraController;
            if (cameraController != null)
            {
                cameraOrigin = cameraController.GetRigPosition();
                hasCameraOrigin = true;
            }
        }

        public INarrativeCommandOperation Execute(
            NarrativeCommandDefinition command,
            NarrativeActorHandle actor,
            bool completeImmediately)
        {
            if (disposed || command == null)
                return Failure("NarrativePresentationUnavailable");
            NarrativeActorActionParameters actorParameters =
                command.ActorActionParameters ??
                new NarrativeActorActionParameters();
            NarrativeMediaParameters media = command.MediaParameters ??
                new NarrativeMediaParameters();
            switch (command.Type)
            {
                case NarrativeCommandType.EnterVisualNovelMode:
                    view?.SetVisualNovelMode(true);
                    return Success();
                case NarrativeCommandType.ExitVisualNovelMode:
                    view?.SetVisualNovelMode(false);
                    return Success();
                case NarrativeCommandType.ShowFullscreenImage:
                    Texture texture = media.AssetReference as Texture;
                    if (texture == null)
                        return Failure("FullscreenImageMissing");
                    view?.ShowFullscreenImage(texture);
                    return Success();
                case NarrativeCommandType.HideFullscreenImage:
                    view?.HideFullscreenImage();
                    return Success();
                case NarrativeCommandType.ShowSpeechBubble:
                    return ShowCallout(
                        actor,
                        actorParameters.Message,
                        Color.white,
                        actorParameters.Duration,
                        completeImmediately);
                case NarrativeCommandType.ShowEmote:
                    return ShowCallout(
                        actor,
                        GetEmoteText(string.IsNullOrWhiteSpace(
                            actorParameters.EmoteId)
                            ? actorParameters.Message
                            : actorParameters.EmoteId),
                        new Color(1f, 0.78f, 0.2f),
                        actorParameters.Duration,
                        completeImmediately);
                case NarrativeCommandType.FocusActor:
                    if (actor?.Card == null || cameraController == null)
                        return Failure("FocusActorUnavailable");
                    Tween focus = cameraController.FocusOn(
                        actor.Card.transform.position,
                        completeImmediately ? 0f : actorParameters.Duration);
                    return Track(new PresentationTweenOperation(focus));
                case NarrativeCommandType.ShakeCamera:
                    cameraController?.Shake(
                        completeImmediately ? 0f : actorParameters.Duration,
                        Mathf.Max(0.05f, actorParameters.Speed * 0.1f));
                    return Success();
                default:
                    return Failure(
                        $"UnsupportedNarrativePresentation:{command.Type}");
            }
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
            foreach (GameObject callout in callouts)
                if (callout != null)
                    UnityEngine.Object.Destroy(callout);
            callouts.Clear();
            if (hasCameraOrigin && cameraController != null)
                cameraController.SetRigPositionInstant(cameraOrigin);
            view?.Restore();
        }

        private INarrativeCommandOperation ShowCallout(
            NarrativeActorHandle actor,
            string text,
            Color color,
            float duration,
            bool completeImmediately)
        {
            if (actor?.Card == null || string.IsNullOrWhiteSpace(text))
                return Failure("NarrativeCalloutMissingData");
            GameObject callout = view?.ShowActorCallout(actor, text, color);
            if (callout == null)
                return Failure("NarrativeCalloutUnavailable");
            callouts.Add(callout);
            Action remove = () =>
            {
                callouts.Remove(callout);
                if (callout != null)
                    UnityEngine.Object.Destroy(callout);
            };
            if (completeImmediately || duration <= 0f)
            {
                remove();
                return Success();
            }
            Tween delay = DOVirtual.DelayedCall(duration, () => { })
                .SetUpdate(true);
            return Track(new PresentationTweenOperation(delay, remove));
        }

        private INarrativeCommandOperation Track(
            INarrativeCommandOperation operation)
        {
            if (operation != null && !operation.IsCompleted)
                operations.Add(operation);
            return operation;
        }

        private static string GetEmoteText(string emoteId) =>
            emoteId?.Trim().ToLowerInvariant() switch
            {
                "shock" => "！",
                "angry" => "怒",
                "question" => "？",
                "happy" => "♪",
                "sad" => "……",
                _ => string.IsNullOrWhiteSpace(emoteId) ? "！" : emoteId
            };

        private static INarrativeCommandOperation Success() =>
            new ImmediateNarrativeCommandOperation();

        private static INarrativeCommandOperation Failure(string error) =>
            new ImmediateNarrativeCommandOperation(false, error);

        private sealed class PresentationTweenOperation :
            INarrativeCommandOperation
        {
            private Tween tween;
            private Action finish;

            public PresentationTweenOperation(Tween tween, Action finish = null)
            {
                this.tween = tween;
                this.finish = finish;
                if (tween == null)
                {
                    Finish(true, string.Empty);
                    return;
                }
                tween.OnComplete(() => Finish(true, string.Empty));
            }

            public bool IsCompleted { get; private set; }
            public NarrativeCommandResult Result { get; private set; }
            public event Action<NarrativeCommandResult> Completed;

            public void CompleteImmediately()
            {
                if (IsCompleted)
                    return;
                tween?.Complete(false);
                Finish(true, string.Empty);
            }

            public void Cancel(string reason)
            {
                if (IsCompleted)
                    return;
                tween?.Kill(false);
                finish = null;
                Finish(false, reason ?? "Cancelled");
            }

            private void Finish(bool success, string error)
            {
                if (IsCompleted)
                    return;
                tween = null;
                if (success)
                    finish?.Invoke();
                finish = null;
                IsCompleted = true;
                Result = new NarrativeCommandResult(success, error);
                Completed?.Invoke(Result);
            }
        }
    }
}
