using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public enum NarrativeQueuePolicy
    {
        Reject = 0,
        Queue = 1,
        ReplaceQueued = 2
    }

    public enum NarrativeRunPolicy
    {
        OncePerSave = 0,
        OncePerSourceRun = 1,
        Repeatable = 2
    }

    public enum NarrativeTriggerSubmissionResult
    {
        Started = 0,
        Queued = 1,
        ReplacedQueued = 2,
        RejectedBusy = 3,
        RejectedDuplicate = 4,
        RejectedQueueFull = 5,
        InvalidRequest = 6
    }

    [Serializable]
    public sealed class NarrativeTriggerRequest
    {
        public string TriggerInstanceId { get; }
        public string NarrativeId { get; }
        public int Priority { get; }
        public NarrativeQueuePolicy QueuePolicy { get; }
        public NarrativeRunPolicy RunPolicy { get; }
        public string SourceRunId { get; }
        public string SourceSystemId { get; }
        public string SourceEventId { get; }
        public string RequiredLocationId { get; }
        public bool SurviveSceneLoad { get; }
        public Dictionary<string, string> ContextValues { get; } = new(
            StringComparer.Ordinal);

        public NarrativeTriggerRequest(
            string triggerInstanceId,
            string narrativeId,
            int priority,
            NarrativeQueuePolicy queuePolicy,
            NarrativeRunPolicy runPolicy,
            string sourceRunId)
            : this(
                triggerInstanceId,
                narrativeId,
                priority,
                queuePolicy,
                runPolicy,
                sourceRunId,
                string.Empty,
                string.Empty,
                string.Empty,
                false)
        {
        }

        public NarrativeTriggerRequest(
            string triggerInstanceId,
            string narrativeId,
            int priority,
            NarrativeQueuePolicy queuePolicy,
            NarrativeRunPolicy runPolicy,
            string sourceRunId,
            string sourceSystemId,
            string sourceEventId,
            string requiredLocationId)
            : this(
                triggerInstanceId,
                narrativeId,
                priority,
                queuePolicy,
                runPolicy,
                sourceRunId,
                sourceSystemId,
                sourceEventId,
                requiredLocationId,
                false)
        {
        }

        public NarrativeTriggerRequest(
            string triggerInstanceId,
            string narrativeId,
            int priority,
            NarrativeQueuePolicy queuePolicy,
            NarrativeRunPolicy runPolicy,
            string sourceRunId,
            string sourceSystemId,
            string sourceEventId,
            string requiredLocationId,
            bool surviveSceneLoad)
        {
            TriggerInstanceId = triggerInstanceId ?? string.Empty;
            NarrativeId = narrativeId ?? string.Empty;
            Priority = Math.Max(0, Math.Min(100, priority));
            QueuePolicy = queuePolicy;
            RunPolicy = runPolicy;
            SourceRunId = sourceRunId ?? string.Empty;
            SourceSystemId = sourceSystemId ?? string.Empty;
            SourceEventId = sourceEventId ?? string.Empty;
            RequiredLocationId = requiredLocationId ?? string.Empty;
            SurviveSceneLoad = surviveSceneLoad;
        }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(TriggerInstanceId) &&
            !string.IsNullOrWhiteSpace(NarrativeId);
    }

    public sealed class NarrativeTriggerScheduler
    {
        public const int MaximumQueuedRequests = 16;

        private sealed class QueuedRequest
        {
            public NarrativeTriggerRequest Request;
            public long Sequence;
        }

        private readonly List<QueuedRequest> queued = new();
        private readonly HashSet<string> seenTriggerIds =
            new(StringComparer.Ordinal);
        private long nextSequence;

        public NarrativeTriggerRequest ActiveRequest { get; private set; }
        public int QueueCount => queued.Count;
        public IEnumerable<NarrativeTriggerRequest> QueuedRequests => queued
            .OrderByDescending(item => item.Request.Priority)
            .ThenBy(item => item.Sequence)
            .Select(item => item.Request);

        public NarrativeTriggerSubmissionResult Submit(
            NarrativeTriggerRequest request) => Submit(
                request,
                canStartImmediately: true);

        public NarrativeTriggerSubmissionResult Submit(
            NarrativeTriggerRequest request,
            bool canStartImmediately)
        {
            if (request == null || !request.IsValid)
                return NarrativeTriggerSubmissionResult.InvalidRequest;
            if (seenTriggerIds.Contains(request.TriggerInstanceId))
                return NarrativeTriggerSubmissionResult.RejectedDuplicate;
            if (ActiveRequest == null && canStartImmediately)
            {
                ActiveRequest = request;
                seenTriggerIds.Add(request.TriggerInstanceId);
                return NarrativeTriggerSubmissionResult.Started;
            }
            if (request.QueuePolicy == NarrativeQueuePolicy.Reject)
                return NarrativeTriggerSubmissionResult.RejectedBusy;

            if (request.QueuePolicy == NarrativeQueuePolicy.ReplaceQueued)
            {
                QueuedRequest existing = queued.FirstOrDefault(item =>
                    item.Request.NarrativeId == request.NarrativeId &&
                    item.Request.SourceRunId == request.SourceRunId);
                if (existing != null)
                {
                    existing.Request = request;
                    seenTriggerIds.Add(request.TriggerInstanceId);
                    return NarrativeTriggerSubmissionResult.ReplacedQueued;
                }
            }

            if (queued.Count >= MaximumQueuedRequests)
                return NarrativeTriggerSubmissionResult.RejectedQueueFull;
            queued.Add(new QueuedRequest
            {
                Request = request,
                Sequence = nextSequence++
            });
            seenTriggerIds.Add(request.TriggerInstanceId);
            return NarrativeTriggerSubmissionResult.Queued;
        }

        public NarrativeTriggerRequest CompleteActive()
        {
            ActiveRequest = null;
            return PromoteNext();
        }

        public NarrativeTriggerRequest PromoteNext()
        {
            if (ActiveRequest != null)
                return ActiveRequest;
            QueuedRequest next = queued
                .OrderByDescending(item => item.Request.Priority)
                .ThenBy(item => item.Sequence)
                .FirstOrDefault();
            if (next == null)
                return null;
            queued.Remove(next);
            ActiveRequest = next.Request;
            return ActiveRequest;
        }

        public int DiscardQueuedForSceneLoad()
        {
            return queued.RemoveAll(item =>
                item?.Request?.SurviveSceneLoad != true);
        }
    }
}
