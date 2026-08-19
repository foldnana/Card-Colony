using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public enum WorldQuestCategory
    {
        Main = 0,
        Side = 1,
        Contract = 2,
        Tutorial = 3,
        Hidden = 4
    }

    public enum WorldQuestStartMode
    {
        ManualAccept = 0,
        AutoActivate = 1
    }

    public enum WorldQuestTurnInMode
    {
        ManualNpc = 0,
        AutoOnFinalStage = 1
    }

    public enum WorldQuestRepeatPolicy
    {
        Never = 0,
        Daily = 1,
        CooldownHours = 2
    }

    public enum WorldQuestVisibility
    {
        HiddenUntilAvailable = 0,
        ShowWhenLocked = 1,
        Always = 2
    }

    public enum WorldQuestObjectiveCompletionMode
    {
        All = 0,
        Any = 1
    }

    public enum WorldQuestObjectiveType
    {
        TalkToNpc = 0,
        MarketPurchase = 1,
        MarketSale = 2,
        EnterLocation = 3,
        DefeatCard = 4,
        ObtainCard = 5,
        DeliverCard = 6,
        CraftCard = 7,
        OwnCardCount = 8,
        CurrencyBalance = 9,
        ProtagonistLevel = 10,
        WorldDayReached = 11,
        DialogueChoice = 12,
        Interaction = 13
    }

    public enum WorldQuestActorRequirement
    {
        Any = 0,
        PlayerParty = 1,
        Protagonist = 2
    }

    public enum WorldQuestProgressMode
    {
        AddEventAmount = 0,
        SetToEventAmount = 1,
        SetToCurrentSnapshot = 2
    }

    public enum WorldQuestConditionMode
    {
        All = 0,
        Any = 1
    }

    public enum WorldQuestConditionType
    {
        QuestStatus = 0,
        QuestOutcome = 1,
        WorldFactBool = 2,
        WorldFactInt = 3,
        WorldFactString = 4,
        ProtagonistLevel = 5,
        OwnedCardCount = 6,
        CurrentLocation = 7,
        WorldDay = 8,
        SelectedChoiceEquals = 9
    }

    public enum WorldQuestComparison
    {
        Equal = 0,
        NotEqual = 1,
        Less = 2,
        LessOrEqual = 3,
        Greater = 4,
        GreaterOrEqual = 5
    }

    public enum WorldQuestEffectType
    {
        GrantCard = 0,
        GrantProtagonistExperience = 1,
        SetWorldFactBool = 2,
        SetWorldFactInt = 3,
        SetWorldFactString = 4,
        IncrementWorldFactInt = 5,
        MakeQuestAvailable = 6,
        ActivateQuest = 7,
        SuspendQuest = 8,
        ResumeQuest = 9,
        FailQuest = 10,
        CancelQuest = 11,
        TrackQuest = 12,
        ShowNotification = 13
    }

    [Serializable]
    public sealed class WorldQuestConditionSet
    {
        [SerializeField] private WorldQuestConditionMode mode;
        [SerializeField] private bool invertResult;
        [SerializeField] private List<WorldQuestConditionDefinition>
            conditions = new();

        public WorldQuestConditionMode Mode => mode;
        public bool InvertResult => invertResult;
        public IReadOnlyList<WorldQuestConditionDefinition> Conditions =>
            conditions;
    }

    [Serializable]
    public sealed class WorldQuestConditionDefinition
    {
        [SerializeField] private string conditionId;
        [SerializeField] private WorldQuestConditionType type;
        [SerializeField] private WorldQuestComparison comparison;
        [SerializeField] private string targetId;
        [SerializeField] private string stringValue;
        [SerializeField] private int intValue;
        [SerializeField] private bool boolValue;
        [SerializeField] private WorldQuestStatus questStatusValue;

        public string ConditionId => conditionId;
        public WorldQuestConditionType Type => type;
        public WorldQuestComparison Comparison => comparison;
        public string TargetId => targetId;
        public string StringValue => stringValue;
        public int IntValue => intValue;
        public bool BoolValue => boolValue;
        public WorldQuestStatus QuestStatusValue => questStatusValue;
    }

    [Serializable]
    public sealed class WorldQuestObjectiveDefinition
    {
        [SerializeField] private string objectiveId;
        [SerializeField] private WorldQuestObjectiveType type;
        [SerializeField] private string targetId;
        [SerializeField] private string secondaryTargetId;
        [SerializeField] private string contextId;
        [SerializeField, Min(1)] private int requiredAmount = 1;
        [SerializeField] private WorldQuestProgressMode progressMode;
        [SerializeField] private WorldQuestActorRequirement actorRequirement;
        [SerializeField] private GameplayInteractionPhase interactionPhase =
            GameplayInteractionPhase.Resolved;
        [SerializeField] private bool optional;
        [SerializeField] private bool showProgress = true;
        [SerializeField] private string displayText;

        public string ObjectiveId => objectiveId;
        public WorldQuestObjectiveType Type => type;
        public string TargetId => targetId;
        public string SecondaryTargetId => secondaryTargetId;
        public string ContextId => contextId;
        public int RequiredAmount => Mathf.Max(1, requiredAmount);
        internal int SerializedRequiredAmount => requiredAmount;
        public WorldQuestProgressMode ProgressMode => progressMode;
        public WorldQuestActorRequirement ActorRequirement => actorRequirement;
        public GameplayInteractionPhase InteractionPhase => interactionPhase;
        public bool Optional => optional;
        public bool ShowProgress => showProgress;
        public string DisplayText => displayText;

        // Compatibility for existing presentation code during the migration.
        public string Text => displayText;
    }

    [Serializable]
    public sealed class WorldQuestEffectDefinition
    {
        [SerializeField] private string effectId;
        [SerializeField] private WorldQuestEffectType type;
        [SerializeField] private string targetId;
        [SerializeField] private string secondaryTargetId;
        [SerializeField] private int intValue;
        [SerializeField] private bool boolValue;
        [SerializeField] private string stringValue;
        [SerializeField] private string reasonId;
        [SerializeField, TextArea] private string notification;

        public string EffectId => effectId;
        public WorldQuestEffectType Type => type;
        public string TargetId => targetId;
        public string SecondaryTargetId => secondaryTargetId;
        public int IntValue => intValue;
        public bool BoolValue => boolValue;
        public string StringValue => stringValue;
        public string ReasonId => reasonId;
        public string Notification => notification;
    }

    [Serializable]
    public sealed class WorldQuestStageTransitionDefinition
    {
        [SerializeField] private string transitionId;
        [SerializeField] private int priority;
        [SerializeField] private WorldQuestConditionSet conditions = new();
        [SerializeField] private string requiredChoiceId;
        [SerializeField] private string targetStageId;

        public string TransitionId => transitionId;
        public int Priority => priority;
        public WorldQuestConditionSet Conditions => conditions;
        public string RequiredChoiceId => requiredChoiceId;
        public string TargetStageId => targetStageId;
    }

    [Serializable]
    public sealed class WorldQuestStageDefinition
    {
        [SerializeField] private string stageId;
        [SerializeField] private string title;
        [SerializeField, TextArea(1, 4)] private string objectiveSummary;
        [SerializeField, TextArea(1, 4)] private string activeReminderText;
        [SerializeField] private WorldQuestObjectiveCompletionMode
            completionMode;
        [SerializeField] private List<WorldQuestObjectiveDefinition>
            objectives = new();
        [SerializeField] private List<WorldQuestEffectDefinition>
            onEnterEffects = new();
        [SerializeField] private List<WorldQuestEffectDefinition>
            onCompleteEffects = new();
        [SerializeField] private List<WorldQuestStageTransitionDefinition>
            transitions = new();

        public string StageId => stageId;
        public string Title => title;
        public string ObjectiveSummary => objectiveSummary;
        public string ActiveReminderText => activeReminderText;
        public WorldQuestObjectiveCompletionMode CompletionMode =>
            completionMode;
        public IReadOnlyList<WorldQuestObjectiveDefinition> Objectives =>
            objectives;
        public IReadOnlyList<WorldQuestEffectDefinition> OnEnterEffects =>
            onEnterEffects;
        public IReadOnlyList<WorldQuestEffectDefinition> OnCompleteEffects =>
            onCompleteEffects;
        public IReadOnlyList<WorldQuestStageTransitionDefinition> Transitions =>
            transitions;
    }

    [Serializable]
    public sealed class WorldQuestOutcomeDefinition
    {
        [SerializeField] private string outcomeId;
        [SerializeField] private int priority;
        [SerializeField] private bool isDefault;
        [SerializeField] private string choiceLabel;
        [SerializeField, TextArea(2, 5)] private string resolutionText;
        [SerializeField] private WorldQuestConditionSet selectionConditions =
            new();
        [SerializeField] private WorldQuestConditionSet turnInConditions =
            new();
        [SerializeField] private List<WorldQuestEffectDefinition> effects =
            new();

        public string OutcomeId => outcomeId;
        public int Priority => priority;
        public bool IsDefault => isDefault;
        public string ChoiceLabel => choiceLabel;
        public string ResolutionText => resolutionText;
        public WorldQuestConditionSet SelectionConditions =>
            selectionConditions;
        public WorldQuestConditionSet TurnInConditions => turnInConditions;
        public IReadOnlyList<WorldQuestEffectDefinition> Effects => effects;
    }

    [Serializable]
    public sealed class WorldQuestDialogueDefinition
    {
        [SerializeField, TextArea(2, 5)] private string offerText;
        [SerializeField] private string acceptLabel = "接受委托";
        [SerializeField] private string declineLabel = "暂不接受";
        [SerializeField, TextArea(2, 5)] private string acceptedText;
        [SerializeField, TextArea(2, 5)] private string suspendedText;
        [SerializeField, TextArea(2, 5)] private string completedText;
        [SerializeField, TextArea(2, 5)] private string failedText;
        [SerializeField, TextArea(2, 5)] private string cancelledText;
        [SerializeField] private string turnInLabel = "汇报";
        [SerializeField] private string postponeLabel = "稍后再说";

        public string OfferText => offerText;
        public string AcceptLabel => acceptLabel;
        public string DeclineLabel => declineLabel;
        public string AcceptedText => acceptedText;
        public string SuspendedText => suspendedText;
        public string CompletedText => completedText;
        public string FailedText => failedText;
        public string CancelledText => cancelledText;
        public string TurnInLabel => turnInLabel;
        public string PostponeLabel => postponeLabel;
    }

    [CreateAssetMenu(
        menuName = "StackCraft/World Quest V2",
        fileName = "WorldQuest_")]
    public sealed class WorldQuestDefinition : ScriptableObject
    {
        [SerializeField] private int schemaVersion = 2;
        [SerializeField] private string id;
        [SerializeField] private WorldQuestCategory category;
        [SerializeField] private int priority;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 6)] private string description;
        [SerializeField] private WorldQuestVisibility visibility;
        [SerializeField] private WorldQuestStartMode startMode;
        [SerializeField] private WorldQuestTurnInMode turnInMode;
        [SerializeField] private WorldQuestRepeatPolicy repeatPolicy;
        [SerializeField, Min(1)] private int cooldownHours = 24;
        [SerializeField] private bool defaultTracked;
        [SerializeField] private string giverNpcId;
        [SerializeField] private string acceptLocationId;
        [SerializeField] private List<string> turnInNpcIds = new();
        [SerializeField] private string entryStageId;
        [SerializeField] private WorldQuestConditionSet hardStartConditions =
            new();
        [SerializeField] private WorldQuestConditionSet availabilityConditions =
            new();
        [SerializeField] private WorldQuestConditionSet acceptConditions =
            new();
        [SerializeField] private List<WorldQuestEffectDefinition>
            onAcceptEffects = new();
        [SerializeField] private List<WorldQuestStageDefinition> stages = new();
        [SerializeField] private List<WorldQuestOutcomeDefinition> outcomes =
            new();
        [SerializeField] private WorldQuestDialogueDefinition dialogue = new();

        public int SchemaVersion => schemaVersion;
        public string Id => id;
        public WorldQuestCategory Category => category;
        public int Priority => priority;
        public string Title => title;
        public string Description => description;
        public WorldQuestVisibility Visibility => visibility;
        public WorldQuestStartMode StartMode => startMode;
        public WorldQuestTurnInMode TurnInMode => turnInMode;
        public WorldQuestRepeatPolicy RepeatPolicy => repeatPolicy;
        public int CooldownHours => Mathf.Max(1, cooldownHours);
        public bool DefaultTracked => defaultTracked;
        public string GiverNpcId => giverNpcId;
        public string AcceptLocationId => acceptLocationId;
        public IReadOnlyList<string> TurnInNpcIds => turnInNpcIds;
        public string EntryStageId => entryStageId;
        public WorldQuestConditionSet HardStartConditions =>
            hardStartConditions;
        public WorldQuestConditionSet AvailabilityConditions =>
            availabilityConditions;
        public WorldQuestConditionSet AcceptConditions => acceptConditions;
        public IReadOnlyList<WorldQuestEffectDefinition> OnAcceptEffects =>
            onAcceptEffects;
        public IReadOnlyList<WorldQuestStageDefinition> Stages => stages;
        public IReadOnlyList<WorldQuestOutcomeDefinition> Outcomes => outcomes;
        public WorldQuestDialogueDefinition Dialogue => dialogue;

        [Obsolete("Use Stages and the active stage instead.")]
        public IReadOnlyList<WorldQuestObjectiveDefinition> Objectives =>
            stages.SelectMany(stage => stage.Objectives).ToList();

        public WorldQuestStageDefinition FindStage(string stageId)
        {
            return stages?.Find(stage =>
                stage != null && stage.StageId == stageId);
        }

        public WorldQuestOutcomeDefinition FindOutcome(string outcomeId)
        {
            return outcomes?.Find(outcome =>
                outcome != null && outcome.OutcomeId == outcomeId);
        }
    }
}
