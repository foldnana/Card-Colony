using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public enum NarrativeCommandType
    {
        ShowNarration = 0,
        ShowDialogue = 1,
        ShowChoice = 2,
        Jump = 3,
        SetWorldFact = 4,
        StartQuest = 5,
        ExecuteInteraction = 6,
        EndNarrative = 7,
        Wait = 8,
        AcquireActorControl = 9,
        ReleaseActorControl = 10,
        MoveToActor = 11,
        MoveToMarker = 12,
        FaceActor = 13,
        ReturnToOrigin = 14,
        ShowSpeechBubble = 15,
        ShowEmote = 16,
        SpawnActor = 17,
        DespawnActor = 18,
        PlayCinematicAttack = 19,
        EnterVisualNovelMode = 20,
        ExitVisualNovelMode = 21,
        ShowFullscreenImage = 22,
        HideFullscreenImage = 23,
        FocusActor = 24,
        ShakeCamera = 25,
        Checkpoint = 26,
        SceneTransition = 27,
        IrreversibleConfirmation = 28,
        ApplyDamage = 29
    }

    public enum NarrativeBarrierType
    {
        None = 0,
        ImportantChoice = 1,
        GameplayInteraction = 2,
        SceneTransition = 3,
        IrreversibleConfirmation = 4,
        Checkpoint = 5,
        NarrativeEnd = 6
    }

    public enum NarrativeActorResolveMode
    {
        PersistentId = 0,
        PartyLeader = 1,
        CardDefinitionId = 2,
        PresentationOnly = 3,
        SpawnTemporary = 4
    }

    public enum NarrativeFailurePolicy
    {
        FailNarrative = 0,
        SkipCommand = 1,
        JumpToFailureNode = 2
    }

    public enum NarrativeEffectType
    {
        SetWorldFactBool = 0,
        SetWorldFactInt = 1,
        SetWorldFactString = 2,
        IncrementWorldFactInt = 3,
        StartQuest = 4,
        ReportQuestEvent = 5,
        ApplyDamage = 6
    }

    [Serializable]
    public sealed class NarrativeFlowParameters
    {
        [SerializeField] private string targetNodeId;

        public string TargetNodeId => targetNodeId;
    }

    [Serializable]
    public sealed class NarrativeChoiceDefinition
    {
        [SerializeField] private string choiceId;
        [SerializeField] private string textKey;
        [SerializeField, TextArea] private string fallbackText;
        [SerializeField] private string targetNodeId;
        [SerializeField] private bool important = true;

        public string ChoiceId => choiceId;
        public string TextKey => textKey;
        public string FallbackText => fallbackText;
        public string TargetNodeId => targetNodeId;
        public bool Important => important;
    }

    [Serializable]
    public sealed class NarrativeDialogueParameters
    {
        [SerializeField] private string actorRole;
        [SerializeField] private string textKey;
        [SerializeField, TextArea] private string fallbackText;
        [SerializeField] private string choiceTitleKey;
        [SerializeField] private string choiceTitleFallback;
        [SerializeField] private List<NarrativeChoiceDefinition> choices =
            new();

        public string ActorRole => actorRole;
        public string TextKey => textKey;
        public string FallbackText => fallbackText;
        public string ChoiceTitleKey => choiceTitleKey;
        public string ChoiceTitleFallback => choiceTitleFallback;
        public IReadOnlyList<NarrativeChoiceDefinition> Choices => choices;
    }

    [Serializable]
    public sealed class NarrativeActorActionParameters
    {
        [SerializeField] private string actorRole;
        [SerializeField] private string targetRole;
        [SerializeField, Min(0f)] private float speed = 1f;
        [SerializeField, Min(0f)] private float duration = 0.5f;
        [SerializeField] private Vector3 markerPosition;
        [SerializeField] private Vector3 offset;
        [SerializeField, TextArea] private string message;
        [SerializeField] private string emoteId;

        public string ActorRole => actorRole;
        public string TargetRole => targetRole;
        public float Speed => Mathf.Max(0f, speed);
        public float Duration => Mathf.Max(0f, duration);
        public Vector3 MarkerPosition => markerPosition;
        public Vector3 Offset => offset;
        public string Message => message;
        public string EmoteId => emoteId;
    }

    [Serializable]
    public sealed class NarrativeTimingParameters
    {
        [SerializeField, Min(0f)] private float duration;

        public float Duration => Mathf.Max(0f, duration);
    }

    [Serializable]
    public sealed class NarrativeMediaParameters
    {
        [SerializeField] private UnityEngine.Object assetReference;
        [SerializeField] private bool waitForCompletion;
        [SerializeField, Min(0f)] private float duration = 0.35f;

        public UnityEngine.Object AssetReference => assetReference;
        public bool WaitForCompletion => waitForCompletion;
        public float Duration => Mathf.Max(0f, duration);
    }

    [Serializable]
    public sealed class NarrativeInteractionArgument
    {
        [SerializeField] private string key;
        [SerializeField] private InteractionValueType valueType;
        [SerializeField] private string stringValue;
        [SerializeField] private int intValue;
        [SerializeField] private float floatValue;
        [SerializeField] private bool boolValue;

        public string Key => key;
        public InteractionValueType ValueType => valueType;
        public string StringValue => stringValue;
        public int IntValue => intValue;
        public float FloatValue => floatValue;
        public bool BoolValue => boolValue;
    }

    [Serializable]
    public sealed class NarrativeInteractionOutcomeBranch
    {
        [SerializeField] private string outcomeId;
        [SerializeField] private string targetNodeId;

        public string OutcomeId => outcomeId;
        public string TargetNodeId => targetNodeId;
    }

    [Serializable]
    public sealed class NarrativeInteractionParameters
    {
        [SerializeField] private string actionId;
        [SerializeField] private List<string> initiatorRoles = new();
        [SerializeField] private List<string> targetRoles = new();
        [SerializeField] private string contextId;
        [SerializeField] private List<NarrativeInteractionArgument>
            arguments = new();
        [SerializeField] private List<NarrativeInteractionOutcomeBranch>
            outcomeBranches = new();
        [SerializeField, Min(0f)] private float timeoutSeconds;

        public string ActionId => actionId;
        public IReadOnlyList<string> InitiatorRoles => initiatorRoles;
        public IReadOnlyList<string> TargetRoles => targetRoles;
        public string ContextId => contextId;
        public IReadOnlyList<NarrativeInteractionArgument> Arguments =>
            arguments;
        public IReadOnlyList<NarrativeInteractionOutcomeBranch>
            OutcomeBranches => outcomeBranches;
        public float TimeoutSeconds => Mathf.Max(0f, timeoutSeconds);
    }

    [Serializable]
    public sealed class NarrativeEffectParameters
    {
        [SerializeField] private NarrativeEffectType effectType;
        [SerializeField] private string resultId;
        [SerializeField] private string targetId;
        [SerializeField] private string secondaryTargetId;
        [SerializeField] private string stringValue;
        [SerializeField] private int intValue;
        [SerializeField] private bool boolValue;

        public NarrativeEffectType EffectType => effectType;
        public string ResultId => resultId;
        public string TargetId => targetId;
        public string SecondaryTargetId => secondaryTargetId;
        public string StringValue => stringValue;
        public int IntValue => intValue;
        public bool BoolValue => boolValue;
    }

    [Serializable]
    public sealed class NarrativeCommandDefinition
    {
        [SerializeField] private string commandId;
        [SerializeField] private NarrativeCommandType type;
        [SerializeField] private NarrativeFlowParameters flowParameters =
            new();
        [SerializeField] private NarrativeDialogueParameters
            dialogueParameters = new();
        [SerializeField] private NarrativeActorActionParameters
            actorActionParameters = new();
        [SerializeField] private NarrativeTimingParameters timingParameters =
            new();
        [SerializeField] private NarrativeMediaParameters mediaParameters =
            new();
        [SerializeField] private NarrativeInteractionParameters
            interactionParameters = new();
        [SerializeField] private NarrativeEffectParameters effectParameters =
            new();
        [SerializeField] private NarrativeFailurePolicy failurePolicy;
        [SerializeField] private string failureNodeId;
        [SerializeField] private NarrativeBarrierType barrierType;

        public string CommandId => commandId;
        public NarrativeCommandType Type => type;
        public NarrativeFlowParameters FlowParameters => flowParameters;
        public NarrativeDialogueParameters DialogueParameters =>
            dialogueParameters;
        public NarrativeActorActionParameters ActorActionParameters =>
            actorActionParameters;
        public NarrativeTimingParameters TimingParameters => timingParameters;
        public NarrativeMediaParameters MediaParameters => mediaParameters;
        public NarrativeInteractionParameters InteractionParameters =>
            interactionParameters;
        public NarrativeEffectParameters EffectParameters => effectParameters;
        public NarrativeFailurePolicy FailurePolicy => failurePolicy;
        public string FailureNodeId => failureNodeId;
        public NarrativeBarrierType BarrierType => barrierType;
    }

    [Serializable]
    public sealed class NarrativeNodeDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private List<NarrativeCommandDefinition> commands =
            new();
        [SerializeField] private string nextNodeId;
        [SerializeField] private string failureNodeId;

        public string Id => id;
        public IReadOnlyList<NarrativeCommandDefinition> Commands => commands;
        public string NextNodeId => nextNodeId;
        public string FailureNodeId => failureNodeId;
    }

    [Serializable]
    public sealed class NarrativeActorBinding
    {
        [SerializeField] private string roleId;
        [SerializeField] private NarrativeActorResolveMode resolveMode;
        [SerializeField] private string persistentId;
        [SerializeField] private string cardDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private Texture portrait;

        public string RoleId => roleId;
        public NarrativeActorResolveMode ResolveMode => resolveMode;
        public string PersistentId => persistentId;
        public string CardDefinitionId => cardDefinitionId;
        public string DisplayName => displayName;
        public Texture Portrait => portrait;
    }

    [CreateAssetMenu(
        fileName = "NarrativeDefinition",
        menuName = "StackCraft/Narrative/Definition")]
    public sealed class NarrativeDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField, Min(1)] private int version = 1;
        [SerializeField] private string displayName;
        [SerializeField] private bool canSkip = true;
        [SerializeField] private bool allowCameraInput;
        [SerializeField] private string entryNodeId;
        [SerializeField] private List<NarrativeActorBinding> actorBindings =
            new();
        [SerializeField] private List<NarrativeNodeDefinition> nodes = new();

        public string Id => id;
        public int Version => Mathf.Max(1, version);
        internal int SerializedVersion => version;
        public string DisplayName => displayName;
        public bool CanSkip => canSkip;
        public bool AllowCameraInput => allowCameraInput;
        public string EntryNodeId => entryNodeId;
        public IReadOnlyList<NarrativeActorBinding> ActorBindings =>
            actorBindings;
        public IReadOnlyList<NarrativeNodeDefinition> Nodes => nodes;

        public NarrativeNodeDefinition FindNode(string nodeId) =>
            nodes?.FirstOrDefault(node => node != null && node.Id == nodeId);
    }
}
