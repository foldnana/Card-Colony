using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CryingSnow.StackCraft
{
    public enum NarrativeDirectorState
    {
        Idle = 0,
        Preparing = 1,
        Playing = 2,
        Completing = 3,
        Failed = 4
    }

    [DisallowMultipleComponent]
    public sealed class NarrativeDirector : MonoBehaviour
    {
        [SerializeField] private DialoguePanelView dialoguePanel;
        [SerializeField] private List<NarrativeDefinition> definitions = new();

        private readonly object narrativeInputLock = new();
        private readonly NarrativeTriggerScheduler scheduler = new();
        private readonly GameplayInteractionRegistry interactionRegistry = new();
        private readonly Dictionary<string, NarrativeActorHandle>
            resolvedActors = new(StringComparer.Ordinal);
        private NarrativeRuntime runtime;
        private NarrativeActorControlService actorControls;
        private INarrativeCommandExecutor commandExecutor;
        private NarrativeCleanupScope cleanupScope;
        private NarrativeDefinition activeDefinition;
        private NarrativeTriggerRequest activeTrigger;
        private bool ownsInputLock;
        private bool closeDialogueOnFinish;
        private NarrativeRuntimeState lastPresentedRuntimeState =
            NarrativeRuntimeState.Idle;

        public static NarrativeDirector Instance { get; private set; }
        public NarrativeDirectorState State { get; private set; } =
            NarrativeDirectorState.Idle;
        public NarrativeRuntime Runtime => runtime;
        public GameplayInteractionRegistry InteractionRegistry =>
            interactionRegistry;
        public string FailureReason { get; private set; } = string.Empty;

        public static NarrativeDirector Ensure(GameObject host)
        {
            if (Instance != null)
                return Instance;
            if (host == null)
                return null;
            NarrativeDirector director = host.GetComponent<NarrativeDirector>();
            return director != null
                ? director
                : host.AddComponent<NarrativeDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            foreach (NarrativeDefinition definition in
                     Resources.LoadAll<NarrativeDefinition>("Narratives"))
            {
                if (definition != null && definitions.All(value =>
                        value == null || value.Id != definition.Id))
                    definitions.Add(definition);
            }
            interactionRegistry.Register(
                new ValidationGameplayInteractionHandler(
                    "narrative.validation",
                    "validated"));
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Update()
        {
            if (State != NarrativeDirectorState.Playing || runtime == null)
                return;

            runtime.AdvanceTime(Time.unscaledDeltaTime);
            if (runtime.State != lastPresentedRuntimeState)
            {
                try
                {
                    PresentRuntimeState();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    Fail(exception.Message);
                }
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            runtime?.Cancel("DirectorDestroyed");
            CleanupPresentationAndInput();
            if (closeDialogueOnFinish &&
                DialogueManager.Instance?.IsActive == true)
                DialogueManager.Instance.EndDialogue();
            if (Instance == this)
                Instance = null;
        }

        public NarrativeTriggerSubmissionResult Submit(
            NarrativeTriggerRequest request)
        {
            bool canStartImmediately = State == NarrativeDirectorState.Idle &&
                scheduler.ActiveRequest == null;
            NarrativeTriggerSubmissionResult result = scheduler.Submit(
                request,
                canStartImmediately);
            if (result == NarrativeTriggerSubmissionResult.Started)
                StartScheduled(request);
            return result;
        }

        public bool Play(NarrativeDefinition definition, string runId)
        {
            if (definition == null || State != NarrativeDirectorState.Idle)
                return false;

            State = NarrativeDirectorState.Preparing;
            FailureReason = string.Empty;
            activeDefinition = definition;
            cleanupScope = new NarrativeCleanupScope();
            try
            {
                if (dialoguePanel == null)
                    dialoguePanel = FindObjectOfType<DialoguePanelView>(true);
                if (dialoguePanel == null && RequiresDialoguePanel(definition))
                {
                    Fail("DialoguePanelView is unavailable.");
                    return false;
                }
                NarrativeValidationReport validation =
                    NarrativeValidator.ValidateInteractions(
                        definition,
                        interactionRegistry);
                if (!validation.IsValid)
                {
                    Fail(string.Join("\n", validation.Errors));
                    return false;
                }
                if (!TryResolveActors(definition, out string actorError))
                {
                    Fail(actorError);
                    return false;
                }
                AcquireInput(definition.AllowCameraInput);
                GameData gameData = GameDirector.Instance?.GameData ??
                    new GameData();
                IWorldQuestRuntime quests = WorldQuestRuntime.Instance;
                var effects = new WorldEffectService(gameData, quests);
                var eventSink = new WorldQuestInteractionEventSink(
                    quests, gameData);
                var interactions = new GameplayInteractionGateway(
                    interactionRegistry,
                    eventSink,
                    new GameplayInteractionContext(gameData));
                NarrativePresentationView presentationView =
                    NarrativePresentationView.Instance ??
                    FindObjectOfType<NarrativePresentationView>(true);
                var presentation = new NarrativePresentationService(
                    presentationView,
                    FindObjectOfType<CameraController>(true));
                presentationView?.BindSkip(() => SkipToNextBarrier());
                cleanupScope.Push(() => presentationView?.BindSkip(null));
                actorControls = new NarrativeActorControlService();
                commandExecutor = new NarrativeWorldActionExecutor(
                    resolvedActors,
                    actorControls,
                    presentation);
                cleanupScope.Push(() => commandExecutor?.Dispose());
                runtime = new NarrativeRuntime(
                    effects,
                    interactions,
                    resolvedActors,
                    commandExecutor);
                State = NarrativeDirectorState.Playing;
                if (!runtime.Start(definition, runId))
                {
                    Fail(runtime.FailureReason);
                    return false;
                }

                PresentRuntimeState();
                return State != NarrativeDirectorState.Failed;
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
                Debug.LogException(exception, this);
                return false;
            }
        }

        public bool Play(string narrativeId, string runId)
        {
            NarrativeDefinition definition = definitions.FirstOrDefault(value =>
                value != null && string.Equals(value.Id, narrativeId,
                    StringComparison.Ordinal));
            return definition != null && Play(definition, runId);
        }

        public bool TryPlayQuestOffer(string questId, string sourceRunId)
        {
            if (string.IsNullOrWhiteSpace(questId))
                return false;
            string narrativeId = $"quest_offer.{questId}";
            if (definitions.All(value => value == null ||
                    !string.Equals(value.Id, narrativeId,
                        StringComparison.Ordinal)))
                return false;
            closeDialogueOnFinish = true;
            var request = new NarrativeTriggerRequest(
                $"quest-offer:{questId}:{Guid.NewGuid():N}",
                narrativeId,
                50,
                NarrativeQueuePolicy.Reject,
                NarrativeRunPolicy.Repeatable,
                string.IsNullOrWhiteSpace(sourceRunId)
                    ? Guid.NewGuid().ToString("N")
                    : sourceRunId,
                "dialogue",
                questId,
                GameDirector.Instance?.GameData?.ActiveLocationId ??
                    string.Empty);
            NarrativeTriggerSubmissionResult result = Submit(request);
            if (result != NarrativeTriggerSubmissionResult.Started)
                closeDialogueOnFinish = false;
            return result == NarrativeTriggerSubmissionResult.Started;
        }

        public bool TryPlayNpcNarrative(string npcId, string sourceRunId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                return false;
            string narrativeId = $"npc_event.{npcId}";
            if (definitions.All(value => value == null ||
                    !string.Equals(value.Id, narrativeId,
                        StringComparison.Ordinal)))
            {
                return false;
            }
            GameData data = GameDirector.Instance?.GameData;
            if (data?.Narrative?.CompletedOnceNarrativeIds?.Contains(
                    narrativeId) == true)
            {
                return false;
            }

            closeDialogueOnFinish = true;
            var request = new NarrativeTriggerRequest(
                $"npc-event:{npcId}:{Guid.NewGuid():N}",
                narrativeId,
                60,
                NarrativeQueuePolicy.Reject,
                NarrativeRunPolicy.OncePerSave,
                string.IsNullOrWhiteSpace(sourceRunId)
                    ? Guid.NewGuid().ToString("N")
                    : sourceRunId,
                "dialogue",
                npcId,
                data?.ActiveLocationId ??
                    string.Empty);
            NarrativeTriggerSubmissionResult result = Submit(request);
            if (result != NarrativeTriggerSubmissionResult.Started)
                closeDialogueOnFinish = false;
            return result == NarrativeTriggerSubmissionResult.Started;
        }

        public void Continue()
        {
            if (State != NarrativeDirectorState.Playing || runtime == null)
                return;
            try
            {
                runtime.Continue();
                PresentRuntimeState();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Fail(exception.Message);
            }
        }

        public bool SelectChoice(string choiceId)
        {
            if (State != NarrativeDirectorState.Playing || runtime == null)
                return false;
            try
            {
                bool selected = runtime.SelectChoice(choiceId);
                if (selected)
                    PresentRuntimeState();
                return selected;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Fail(exception.Message);
                return false;
            }
        }

        public bool SkipToNextBarrier()
        {
            if (State != NarrativeDirectorState.Playing || runtime == null)
                return false;
            try
            {
                bool skipped = runtime.SkipToNextBarrier();
                if (skipped)
                    PresentRuntimeState();
                return skipped;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Fail(exception.Message);
                return false;
            }
        }

        public void Cancel(string reason = "Cancelled")
        {
            if (State == NarrativeDirectorState.Idle)
                return;
            runtime?.Cancel(reason);
            FailureReason = reason ?? string.Empty;
            State = NarrativeDirectorState.Failed;
            Finish(saveResult: false);
        }

        private void PresentRuntimeState()
        {
            if (runtime == null)
                return;
            lastPresentedRuntimeState = runtime.State;
            switch (runtime.State)
            {
                case NarrativeRuntimeState.WaitingForInput:
                    ShowLine();
                    break;
                case NarrativeRuntimeState.WaitingForChoice:
                    ShowChoices();
                    break;
                case NarrativeRuntimeState.WaitingAtBarrier:
                    ShowBarrier();
                    break;
                case NarrativeRuntimeState.Completed:
                    State = NarrativeDirectorState.Completing;
                    Finish(saveResult: true);
                    break;
                case NarrativeRuntimeState.Failed:
                    Fail(runtime.FailureReason);
                    break;
            }
        }

        private void ShowLine()
        {
            if (dialoguePanel == null || runtime.CurrentLine == null)
                return;
            NarrativeActorHandle actor = ResolveActor(
                runtime.CurrentLine.ActorRole);
            dialoguePanel.ShowNarrative(
                actor?.DisplayName ?? string.Empty,
                actor?.Portrait,
                runtime.CurrentLine.FallbackText,
                string.Empty,
                new[]
                {
                    new DialogueChoiceOption("继续", Continue)
                });
        }

        private void ShowChoices()
        {
            if (dialoguePanel == null)
                return;
            var options = runtime.CurrentChoices
                .Cast<NarrativeChoiceDefinition>()
                .Select(choice => new DialogueChoiceOption(
                    choice.FallbackText,
                    () => SelectChoice(choice.ChoiceId)))
                .ToArray();
            dialoguePanel.ShowNarrative(
                string.Empty,
                null,
                string.Empty,
                string.IsNullOrWhiteSpace(runtime.CurrentChoiceTitle)
                    ? "你的选择"
                    : runtime.CurrentChoiceTitle,
                options);
        }

        private void ShowBarrier()
        {
            if (dialoguePanel == null)
                return;
            dialoguePanel.ShowNarrative(
                string.Empty,
                null,
                "剧情已到达安全节点。",
                string.Empty,
                new[]
                {
                    new DialogueChoiceOption("继续", Continue)
                });
        }

        private NarrativeActorHandle ResolveActor(string roleId)
        {
            return !string.IsNullOrWhiteSpace(roleId) &&
                resolvedActors.TryGetValue(roleId, out var actor)
                    ? actor
                    : null;
        }

        private void StartScheduled(NarrativeTriggerRequest request)
        {
            activeTrigger = request;
            NarrativeDefinition definition = definitions.FirstOrDefault(value =>
                value != null && value.Id == request.NarrativeId);
            if (definition == null || !CanStart(request))
            {
                scheduler.CompleteActive();
                activeTrigger = null;
                StartNextScheduled();
                return;
            }

            string runId = string.IsNullOrWhiteSpace(request.SourceRunId)
                ? request.TriggerInstanceId
                : request.SourceRunId;
            Play(definition, runId);
        }

        private bool CanStart(NarrativeTriggerRequest request)
        {
            GameData data = GameDirector.Instance?.GameData;
            if (!string.IsNullOrWhiteSpace(request.RequiredLocationId) &&
                !string.Equals(data?.ActiveLocationId,
                    request.RequiredLocationId, StringComparison.Ordinal))
                return false;
            if (request.RunPolicy == NarrativeRunPolicy.OncePerSave &&
                data?.Narrative?.CompletedOnceNarrativeIds?.Contains(
                    request.NarrativeId) == true)
                return false;
            if (request.RunPolicy == NarrativeRunPolicy.OncePerSourceRun &&
                data?.Narrative?.CompletedSourceRunKeys?.Contains(
                    GetSourceRunKey(request)) == true)
                return false;
            return true;
        }

        private void Finish(bool saveResult)
        {
            if (saveResult)
            {
                GameData data = GameDirector.Instance?.GameData;
                if (data != null && activeTrigger?.RunPolicy ==
                    NarrativeRunPolicy.OncePerSave)
                {
                    data.Narrative ??= new NarrativeHistoryData();
                    data.Narrative.CompletedOnceNarrativeIds ??=
                        new List<string>();
                    if (!data.Narrative.CompletedOnceNarrativeIds.Contains(
                            activeDefinition.Id))
                    {
                        data.Narrative.CompletedOnceNarrativeIds.Add(
                            activeDefinition.Id);
                    }
                }
                if (data != null && activeTrigger?.RunPolicy ==
                    NarrativeRunPolicy.OncePerSourceRun)
                {
                    data.Narrative ??= new NarrativeHistoryData();
                    data.Narrative.CompletedSourceRunKeys ??=
                        new List<string>();
                    string key = GetSourceRunKey(activeTrigger);
                    if (!data.Narrative.CompletedSourceRunKeys.Contains(key))
                        data.Narrative.CompletedSourceRunKeys.Add(key);
                }
                if (data?.Narrative?.ActiveRun != null &&
                    string.Equals(data.Narrative.ActiveRun.NarrativeId,
                        activeDefinition?.Id, StringComparison.Ordinal))
                {
                    data.Narrative.ActiveRun = null;
                }
                GameDirector.Instance?.SaveGame();
            }

            CleanupPresentationAndInput();
            if (closeDialogueOnFinish &&
                DialogueManager.Instance?.IsActive == true)
                DialogueManager.Instance.EndDialogue();
            closeDialogueOnFinish = false;
            runtime = null;
            commandExecutor = null;
            actorControls = null;
            activeDefinition = null;
            resolvedActors.Clear();
            lastPresentedRuntimeState = NarrativeRuntimeState.Idle;
            State = NarrativeDirectorState.Idle;
            if (activeTrigger != null)
            {
                scheduler.CompleteActive();
                activeTrigger = null;
            }
            StartNextScheduled();
        }

        private void StartNextScheduled()
        {
            NarrativeTriggerRequest next = scheduler.ActiveRequest ??
                scheduler.PromoteNext();
            if (next != null && State == NarrativeDirectorState.Idle)
                StartScheduled(next);
        }

        private void Fail(string reason)
        {
            runtime?.Cancel(reason);
            FailureReason = reason ?? string.Empty;
            State = NarrativeDirectorState.Failed;
            Finish(saveResult: false);
        }

        private void AcquireInput(bool allowCameraInput)
        {
            if (ownsInputLock || InputManager.Instance == null)
                return;
            InputManager.Instance.AddLock(
                narrativeInputLock,
                allowCameraInput);
            ownsInputLock = true;
            cleanupScope?.Push(ReleaseInput);
        }

        private void CleanupPresentationAndInput()
        {
            dialoguePanel?.Hide();
            try
            {
                cleanupScope?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            cleanupScope = null;
            commandExecutor?.Dispose();
            actorControls?.Dispose();
            ReleaseInput();
        }

        private void ReleaseInput()
        {
            if (!ownsInputLock)
                return;
            InputManager.Instance?.RemoveLock(narrativeInputLock);
            ownsInputLock = false;
        }

        private static string GetSourceRunKey(
            NarrativeTriggerRequest request) =>
            $"{request.NarrativeId}:{request.SourceRunId}";

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            scheduler.DiscardQueuedForSceneLoad();
            if (State != NarrativeDirectorState.Idle)
                Cancel("SceneLoaded");
        }

        private static bool RequiresDialoguePanel(
            NarrativeDefinition definition) =>
            definition?.Nodes?.Any(node => node?.Commands?.Any(command =>
                command != null && command.Type is
                    NarrativeCommandType.ShowNarration or
                    NarrativeCommandType.ShowDialogue or
                    NarrativeCommandType.ShowChoice) == true) == true;

        private bool TryResolveActors(
            NarrativeDefinition definition,
            out string error)
        {
            resolvedActors.Clear();
            var resolver = new NarrativeActorResolver();
            foreach (NarrativeActorBinding binding in
                     definition.ActorBindings ??
                     Array.Empty<NarrativeActorBinding>())
            {
                if (binding == null)
                    continue;
                NarrativeActorHandle actor = resolver.Resolve(binding);
                bool needsCard = binding.ResolveMode is not
                    NarrativeActorResolveMode.PresentationOnly and not
                    NarrativeActorResolveMode.SpawnTemporary;
                if (actor == null || needsCard && actor.Card == null)
                {
                    error = $"无法解析剧情参与者：{binding.RoleId}";
                    resolvedActors.Clear();
                    return false;
                }
                resolvedActors[binding.RoleId] = actor;
            }
            error = string.Empty;
            return true;
        }
    }
}
