using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CardColony.Tests
{
    public sealed class NarrativeP0AUnityTests
    {
        [Test]
        public void Definition_UsesTypedParameters_AndRejectsDuplicateNodes()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type validatorType = RequireType(
                "CryingSnow.StackCraft.NarrativeValidator");

            Assert.That(commandType.GetProperty("DialogueParameters"),
                Is.Not.Null);
            Assert.That(commandType.GetProperty("ActorActionParameters"),
                Is.Not.Null);
            Assert.That(commandType.GetProperty("InteractionParameters"),
                Is.Not.Null);
            Assert.That(commandType.GetProperty("EffectParameters"),
                Is.Not.Null);
            Assert.That(commandType.GetProperty("NumberParameters"), Is.Null);
            Assert.That(commandType.GetProperty("StringParameters"), Is.Null);

            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                SetField(definition, "id", "test_duplicate_nodes");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");
                IList nodes = CreateList(nodeType);
                nodes.Add(CreateNode(nodeType, "begin"));
                nodes.Add(CreateNode(nodeType, "begin"));
                SetField(definition, "nodes", nodes);

                object report = validatorType.GetMethod(
                        "Validate",
                        BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new object[] { definition });

                Assert.That(report, Is.Not.Null);
                Assert.That(GetProperty<bool>(report, "IsValid"), Is.False);
                IEnumerable errors = GetProperty<IEnumerable>(report, "Errors");
                Assert.That(
                    errors.Cast<object>().Any(error =>
                        error.ToString().Contains("begin")),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void TriggerScheduler_DeduplicatesAndPromotesByPriorityThenFifo()
        {
            Type schedulerType = RequireType(
                "CryingSnow.StackCraft.NarrativeTriggerScheduler");
            Type requestType = RequireType(
                "CryingSnow.StackCraft.NarrativeTriggerRequest");
            Type queuePolicyType = RequireType(
                "CryingSnow.StackCraft.NarrativeQueuePolicy");
            Type runPolicyType = RequireType(
                "CryingSnow.StackCraft.NarrativeRunPolicy");
            object scheduler = Activator.CreateInstance(schedulerType);
            object queue = Enum.Parse(queuePolicyType, "Queue");
            object repeatable = Enum.Parse(runPolicyType, "Repeatable");

            object active = CreateTrigger(
                requestType, "trigger-active", "story.active", 10,
                queue, repeatable, "run-1");
            object low = CreateTrigger(
                requestType, "trigger-low", "story.low", 10,
                queue, repeatable, "run-1");
            object highFirst = CreateTrigger(
                requestType, "trigger-high-a", "story.high.a", 90,
                queue, repeatable, "run-1");
            object highSecond = CreateTrigger(
                requestType, "trigger-high-b", "story.high.b", 90,
                queue, repeatable, "run-1");

            Assert.That(Submit(scheduler, active), Is.EqualTo("Started"));
            Assert.That(Submit(scheduler, low), Is.EqualTo("Queued"));
            Assert.That(Submit(scheduler, highFirst), Is.EqualTo("Queued"));
            Assert.That(Submit(scheduler, highSecond), Is.EqualTo("Queued"));
            Assert.That(Submit(scheduler, highFirst),
                Is.EqualTo("RejectedDuplicate"));

            CompleteActive(scheduler);
            Assert.That(GetActiveNarrativeId(scheduler),
                Is.EqualTo("story.high.a"));
            CompleteActive(scheduler);
            Assert.That(GetActiveNarrativeId(scheduler),
                Is.EqualTo("story.high.b"));
            CompleteActive(scheduler);
            Assert.That(GetActiveNarrativeId(scheduler),
                Is.EqualTo("story.low"));
            CompleteActive(scheduler);
            Assert.That(Submit(scheduler, active),
                Is.EqualTo("RejectedDuplicate"),
                "A trigger instance is an idempotency key for the session.");
        }

        [Test]
        public void Validator_RejectsBrokenJumpsDuplicateResultsAndEmptyActionIds()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type flowType = RequireType(
                "CryingSnow.StackCraft.NarrativeFlowParameters");
            Type effectType = RequireType(
                "CryingSnow.StackCraft.NarrativeEffectParameters");
            Type interactionType = RequireType(
                "CryingSnow.StackCraft.NarrativeInteractionParameters");
            Type validatorType = RequireType(
                "CryingSnow.StackCraft.NarrativeValidator");
            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                SetField(definition, "id", "story.invalid");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");
                object begin = CreateNode(nodeType, "begin");
                IList commands = CreateList(commandType);

                object jump = CreateCommand(
                    commandType, commandKindType, "jump", "Jump");
                object flow = Activator.CreateInstance(flowType);
                SetField(flow, "targetNodeId", "missing-node");
                SetField(jump, "flowParameters", flow);
                commands.Add(jump);

                for (int i = 0; i < 2; i++)
                {
                    object effect = CreateCommand(
                        commandType, commandKindType, $"effect-{i}",
                        "SetWorldFact");
                    object parameters = Activator.CreateInstance(effectType);
                    SetField(parameters, "resultId", "duplicate-result");
                    SetField(parameters, "targetId", $"story.fact.{i}");
                    SetField(effect, "effectParameters", parameters);
                    commands.Add(effect);
                }

                object interaction = CreateCommand(
                    commandType, commandKindType, "interaction",
                    "ExecuteInteraction");
                SetField(interaction, "interactionParameters",
                    Activator.CreateInstance(interactionType));
                commands.Add(interaction);
                SetField(begin, "commands", commands);
                IList nodes = CreateList(nodeType);
                nodes.Add(begin);
                SetField(definition, "nodes", nodes);

                object report = validatorType.GetMethod(
                        "Validate",
                        BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new object[] { definition });
                string[] errors = GetProperty<IEnumerable>(report, "Errors")
                    .Cast<object>().Select(value => value.ToString()).ToArray();

                Assert.That(errors.Any(value => value.Contains("missing-node")),
                    Is.True);
                Assert.That(errors.Any(value => value.Contains("duplicate-result")),
                    Is.True);
                Assert.That(errors.Any(value => value.Contains("ActionId")),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Validator_RejectsDuplicateNarrativeIdsAcrossAssets()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type validatorType = RequireType(
                "CryingSnow.StackCraft.NarrativeValidator");
            ScriptableObject first = ScriptableObject.CreateInstance(
                definitionType);
            ScriptableObject second = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                foreach (ScriptableObject definition in new[] { first, second })
                {
                    SetField(definition, "id", "story.duplicate");
                    SetField(definition, "version", 1);
                    SetField(definition, "entryNodeId", "begin");
                    IList nodes = CreateList(nodeType);
                    nodes.Add(CreateNode(nodeType, "begin"));
                    SetField(definition, "nodes", nodes);
                }
                IList definitions = CreateList(definitionType);
                definitions.Add(first);
                definitions.Add(second);
                MethodInfo validateAll = validatorType.GetMethod(
                    "ValidateAll",
                    BindingFlags.Public | BindingFlags.Static);

                Assert.That(validateAll, Is.Not.Null);
                object report = validateAll.Invoke(null,
                    new object[] { definitions });
                Assert.That(GetProperty<bool>(report, "IsValid"), Is.False);
                Assert.That(GetProperty<IEnumerable>(report, "Errors")
                    .Cast<object>().Any(value =>
                        value.ToString().Contains("story.duplicate")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void Validator_RejectsUnregisteredInteractionActions()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type interactionType = RequireType(
                "CryingSnow.StackCraft.NarrativeInteractionParameters");
            Type registryType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionRegistry");
            Type validatorType = RequireType(
                "CryingSnow.StackCraft.NarrativeValidator");
            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                SetField(definition, "id", "story.unknown.action");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");
                object command = CreateCommand(
                    commandType, commandKindType, "unknown",
                    "ExecuteInteraction");
                object parameters = Activator.CreateInstance(interactionType);
                SetField(parameters, "actionId", "future.unknown_action");
                SetField(command, "interactionParameters", parameters);
                object node = CreateNode(nodeType, "begin");
                IList commands = CreateList(commandType);
                commands.Add(command);
                SetField(node, "commands", commands);
                IList nodes = CreateList(nodeType);
                nodes.Add(node);
                SetField(definition, "nodes", nodes);
                object registry = Activator.CreateInstance(registryType);
                MethodInfo validate = validatorType.GetMethod(
                    "ValidateInteractions",
                    BindingFlags.Public | BindingFlags.Static);

                Assert.That(validate, Is.Not.Null);
                object report = validate.Invoke(null,
                    new[] { definition, registry });
                Assert.That(GetProperty<IEnumerable>(report, "Errors")
                    .Cast<object>().Any(value =>
                        value.ToString().Contains("future.unknown_action")),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void InteractionGateway_UsesRegisteredHandler_AndPublishesLifecycle()
        {
            Type registryType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionRegistry");
            Type sinkType = RequireType(
                "CryingSnow.StackCraft.BufferedGameplayInteractionEventSink");
            Type gatewayType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionGateway");
            Type handlerType = RequireType(
                "CryingSnow.StackCraft.ValidationGameplayInteractionHandler");
            Type requestType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionRequest");

            object registry = Activator.CreateInstance(registryType);
            object sink = Activator.CreateInstance(sinkType);
            object gateway = Activator.CreateInstance(
                gatewayType,
                registry,
                sink);
            object handler = Activator.CreateInstance(
                handlerType,
                "exploration.investigate",
                "clue_found");
            Invoke(registry, "Register", handler);
            object request = Activator.CreateInstance(
                requestType,
                "operation-1",
                "exploration.investigate");

            object operation = Invoke(gateway, "Execute", request);

            Assert.That(
                GetProperty<object>(operation, "State").ToString(),
                Is.EqualTo("Completed"));
            object result = GetProperty<object>(operation, "Result");
            Assert.That(GetProperty<string>(result, "OutcomeId"),
                Is.EqualTo("clue_found"));
            IList events = GetProperty<IList>(sink, "Events");
            CollectionAssert.AreEqual(
                new[] { "Started", "Resolved" },
                events.Cast<object>()
                    .Select(value => GetProperty<object>(value, "Phase")
                        .ToString())
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { 1, 1 },
                events.Cast<object>()
                    .Select(value => GetProperty<int>(value, "Quantity"))
                    .ToArray(),
                "Each lifecycle occurrence must be able to advance a phase-filtered task.");
            Assert.That(
                events.Cast<object>().Select(value =>
                    GetProperty<string>(value, "PersistedEventId"))
                    .Distinct().Count(),
                Is.EqualTo(2));
            Assert.That(events.Cast<object>().All(value =>
                    !GetProperty<bool>(value, "ProtagonistParticipated") &&
                    !GetProperty<bool>(value, "CreditedToParty")),
                Is.True,
                "An interaction without declared player actors must not receive player credit.");
        }

        [Test]
        public void WorldQuestInteractionObjective_FiltersActionPhaseOutcomeAndContext()
        {
            Type objectiveType = RequireType(
                "CryingSnow.StackCraft.WorldQuestObjectiveDefinition");
            Type objectiveKindType = RequireType(
                "CryingSnow.StackCraft.WorldQuestObjectiveType");
            Type eventType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEvent");
            Type eventKindType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEventType");
            Type phaseType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionPhase");
            Type directionType = RequireType(
                "CryingSnow.StackCraft.WorldQuestTradeDirection");
            Type engineType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEngine");
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");

            Assert.That(Enum.GetNames(objectiveKindType),
                Does.Contain("Interaction"));
            Assert.That(Enum.GetNames(eventKindType),
                Does.Contain("GameplayInteraction"));

            object objective = Activator.CreateInstance(objectiveType);
            SetField(objective, "type", Enum.Parse(
                objectiveKindType, "Interaction"));
            SetField(objective, "targetId", "exploration.investigate");
            SetField(objective, "secondaryTargetId", "clue_found");
            SetField(objective, "contextId", "riverbend.well");
            SetField(objective, "interactionPhase", Enum.Parse(
                phaseType, "Progressed"));

            object matching = CreateQuestInteractionEvent(
                eventType, eventKindType, directionType, phaseType,
                "interaction-event-1", "Progressed", "clue_found",
                "riverbend.well");
            object wrongPhase = CreateQuestInteractionEvent(
                eventType, eventKindType, directionType, phaseType,
                "interaction-event-2", "Resolved", "clue_found",
                "riverbend.well");
            MethodInfo matches = engineType.GetMethod(
                "Matches",
                BindingFlags.NonPublic | BindingFlags.Static);
            object gameData = Activator.CreateInstance(gameDataType);

            Assert.That(matches, Is.Not.Null);
            Assert.That((bool)matches.Invoke(
                null, new[] { objective, matching, gameData }), Is.True);
            Assert.That((bool)matches.Invoke(
                null, new[] { objective, wrongPhase, gameData }), Is.False);
        }

        [Test]
        public void WorldEffect_IncrementFact_IsIdempotentWithinOneRun()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            Type requestType = RequireType(
                "CryingSnow.StackCraft.WorldEffectRequest");
            Type effectType = RequireType(
                "CryingSnow.StackCraft.NarrativeEffectType");
            object gameData = Activator.CreateInstance(gameDataType);
            object service = Activator.CreateInstance(serviceType, gameData);
            object request = Activator.CreateInstance(
                requestType,
                Enum.Parse(effectType, "IncrementWorldFactInt"),
                "increment_clues",
                "story.clues",
                string.Empty,
                1,
                false);

            object first = Invoke(
                service, "Apply", "story.test", 1, "run-1", "begin", request);
            object duplicate = Invoke(
                service, "Apply", "story.test", 1, "run-1", "begin", request);
            object secondRun = Invoke(
                service, "Apply", "story.test", 1, "run-2", "begin", request);
            object replayFirstRun = Invoke(
                service, "Apply", "story.test", 1, "run-1", "begin", request);

            Assert.That(GetProperty<bool>(first, "Applied"), Is.True);
            Assert.That(GetProperty<bool>(duplicate, "AlreadyApplied"), Is.True);
            Assert.That(GetProperty<bool>(secondRun, "Applied"), Is.True);
            Assert.That(GetProperty<bool>(replayFirstRun, "AlreadyApplied"),
                Is.True, "A later run must not erase earlier commit history.");
            IList facts = (IList)gameDataType.GetField("WorldFacts")
                .GetValue(gameData);
            object fact = facts.Cast<object>().Single(value =>
                GetField<string>(value, "Key") == "story.clues");
            Assert.That(GetField<int>(fact, "IntValue"), Is.EqualTo(2));
        }

        [Test]
        public void WorldEffect_QuestEventUsesCompleteRunScopedEventId()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type questRuntimeType = RequireType(
                "CryingSnow.StackCraft.WorldQuestRuntime");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            Type requestType = RequireType(
                "CryingSnow.StackCraft.WorldEffectRequest");
            Type effectType = RequireType(
                "CryingSnow.StackCraft.NarrativeEffectType");
            var host = new GameObject("NarrativeQuestEventIdTest");
            try
            {
                object gameData = Activator.CreateInstance(gameDataType);
                gameDataType.GetField("ActiveLocationId")
                    .SetValue(gameData, "riverbend");
                Component quests = host.AddComponent(questRuntimeType);
                Invoke(quests, "Initialize", gameData);
                object service = Activator.CreateInstance(
                    serviceType, gameData, quests);
                object request = Activator.CreateInstance(
                    requestType,
                    Enum.Parse(effectType, "ReportQuestEvent"),
                    "report_interaction",
                    "exploration.investigate",
                    "clue_found",
                    "riverbend.well",
                    1,
                    false);

                object first = Invoke(service, "Apply",
                    "story.event.test", 1, "run-1", "begin", request);
                object second = Invoke(service, "Apply",
                    "story.event.test", 1, "run-2", "begin", request);

                Assert.That(GetProperty<bool>(first, "Applied"), Is.True);
                Assert.That(GetProperty<bool>(second, "Applied"), Is.True);
                IList ids = (IList)gameDataType.GetField(
                        "ProcessedWorldQuestEventIds")
                    .GetValue(gameData);
                CollectionAssert.IsSubsetOf(new[]
                {
                    "story.event.test:1:run-1:begin:report_interaction",
                    "story.event.test:1:run-2:begin:report_interaction"
                }, ids.Cast<object>().Select(value => value.ToString())
                    .ToArray());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NarrativeRuntime_PresentsLineAndChoice_ThenAppliesBranchEffect()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type dialogueParametersType = RequireType(
                "CryingSnow.StackCraft.NarrativeDialogueParameters");
            Type choiceType = RequireType(
                "CryingSnow.StackCraft.NarrativeChoiceDefinition");
            Type effectParametersType = RequireType(
                "CryingSnow.StackCraft.NarrativeEffectParameters");
            Type effectKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeEffectType");
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.NarrativeRuntime");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");

            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                SetField(definition, "id", "story.runtime.test");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");

                object line = CreateCommand(
                    commandType, commandKindType, "line", "ShowNarration");
                object lineParameters = Activator.CreateInstance(
                    dialogueParametersType);
                SetField(lineParameters, "textKey", "story.test.greeting");
                SetField(lineParameters, "fallbackText", "你好，旅行者。");
                SetField(line, "dialogueParameters", lineParameters);

                object choiceCommand = CreateCommand(
                    commandType, commandKindType, "choice", "ShowChoice");
                object choiceParameters = Activator.CreateInstance(
                    dialogueParametersType);
                IList choices = CreateList(choiceType);
                object accept = Activator.CreateInstance(choiceType);
                SetField(accept, "choiceId", "accept");
                SetField(accept, "textKey", "story.test.accept");
                SetField(accept, "fallbackText", "接受");
                SetField(accept, "targetNodeId", "accepted");
                choices.Add(accept);
                SetField(choiceParameters, "choices", choices);
                SetField(choiceCommand, "dialogueParameters", choiceParameters);

                object begin = CreateNode(nodeType, "begin");
                IList beginCommands = CreateList(commandType);
                beginCommands.Add(line);
                beginCommands.Add(choiceCommand);
                SetField(begin, "commands", beginCommands);

                object effectCommand = CreateCommand(
                    commandType, commandKindType, "effect", "SetWorldFact");
                object effectParameters = Activator.CreateInstance(
                    effectParametersType);
                SetField(effectParameters, "effectType", Enum.Parse(
                    effectKindType, "SetWorldFactBool"));
                SetField(effectParameters, "resultId", "accepted_fact");
                SetField(effectParameters, "targetId", "story.accepted");
                SetField(effectParameters, "boolValue", true);
                SetField(effectCommand, "effectParameters", effectParameters);
                object endCommand = CreateCommand(
                    commandType, commandKindType, "end", "EndNarrative");
                object accepted = CreateNode(nodeType, "accepted");
                IList acceptedCommands = CreateList(commandType);
                acceptedCommands.Add(effectCommand);
                acceptedCommands.Add(endCommand);
                SetField(accepted, "commands", acceptedCommands);

                IList nodes = CreateList(nodeType);
                nodes.Add(begin);
                nodes.Add(accepted);
                SetField(definition, "nodes", nodes);

                object gameData = Activator.CreateInstance(gameDataType);
                object effects = Activator.CreateInstance(serviceType, gameData);
                object runtime = Activator.CreateInstance(runtimeType, effects);

                Assert.That(Invoke(runtime, "Start", definition, "run-1"),
                    Is.EqualTo(true));
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("WaitingForInput"));
                object currentLine = GetProperty<object>(runtime, "CurrentLine");
                Assert.That(GetProperty<string>(currentLine, "FallbackText"),
                    Is.EqualTo("你好，旅行者。"));

                Invoke(runtime, "Continue");
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("WaitingForChoice"));
                IList currentChoices = GetProperty<IList>(
                    runtime, "CurrentChoices");
                Assert.That(currentChoices.Count, Is.EqualTo(1));
                Invoke(runtime, "SelectChoice", "accept");
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("Completed"));

                IList facts = (IList)gameDataType.GetField("WorldFacts")
                    .GetValue(gameData);
                object acceptedFact = facts.Cast<object>().Single(value =>
                    GetField<string>(value, "Key") == "story.accepted");
                Assert.That(GetField<bool>(acceptedFact, "BoolValue"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ActorResolver_PresentationOnlyActor_DoesNotRequireCardInstance()
        {
            Type bindingType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorBinding");
            Type modeType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorResolveMode");
            Type resolverType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorResolver");
            object binding = Activator.CreateInstance(bindingType);
            SetField(binding, "roleId", "Narrator");
            SetField(binding, "resolveMode", Enum.Parse(
                modeType, "PresentationOnly"));
            SetField(binding, "displayName", "旁白");
            object resolver = Activator.CreateInstance(resolverType);

            object actor = Invoke(resolver, "Resolve", binding);

            Assert.That(actor, Is.Not.Null);
            Assert.That(GetProperty<string>(actor, "RoleId"),
                Is.EqualTo("Narrator"));
            Assert.That(GetProperty<string>(actor, "DisplayName"),
                Is.EqualTo("旁白"));
            Assert.That(GetProperty<object>(actor, "Card"), Is.Null);
        }

        [Test]
        public void NarrativeRuntime_RegisteredInteractionBranchesByOutcome()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type interactionParametersType = RequireType(
                "CryingSnow.StackCraft.NarrativeInteractionParameters");
            Type branchType = RequireType(
                "CryingSnow.StackCraft.NarrativeInteractionOutcomeBranch");
            Type effectParametersType = RequireType(
                "CryingSnow.StackCraft.NarrativeEffectParameters");
            Type effectKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeEffectType");
            Type registryType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionRegistry");
            Type sinkType = RequireType(
                "CryingSnow.StackCraft.BufferedGameplayInteractionEventSink");
            Type gatewayType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionGateway");
            Type handlerType = RequireType(
                "CryingSnow.StackCraft.ValidationGameplayInteractionHandler");
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.NarrativeRuntime");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");

            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                SetField(definition, "id", "story.interaction.test");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");

                object execute = CreateCommand(
                    commandType, commandKindType, "investigate",
                    "ExecuteInteraction");
                object interaction = Activator.CreateInstance(
                    interactionParametersType);
                SetField(interaction, "actionId", "exploration.investigate");
                SetField(interaction, "contextId", "riverbend.well");
                IList branches = CreateList(branchType);
                object branch = Activator.CreateInstance(branchType);
                SetField(branch, "outcomeId", "clue_found");
                SetField(branch, "targetNodeId", "found");
                branches.Add(branch);
                SetField(interaction, "outcomeBranches", branches);
                SetField(execute, "interactionParameters", interaction);
                object begin = CreateNode(nodeType, "begin");
                IList beginCommands = CreateList(commandType);
                beginCommands.Add(execute);
                SetField(begin, "commands", beginCommands);

                object effect = CreateCommand(
                    commandType, commandKindType, "record", "SetWorldFact");
                object effectParameters = Activator.CreateInstance(
                    effectParametersType);
                SetField(effectParameters, "effectType", Enum.Parse(
                    effectKindType, "SetWorldFactBool"));
                SetField(effectParameters, "resultId", "found_clue");
                SetField(effectParameters, "targetId", "story.clue_found");
                SetField(effectParameters, "boolValue", true);
                SetField(effect, "effectParameters", effectParameters);
                object end = CreateCommand(
                    commandType, commandKindType, "end", "EndNarrative");
                object found = CreateNode(nodeType, "found");
                IList foundCommands = CreateList(commandType);
                foundCommands.Add(effect);
                foundCommands.Add(end);
                SetField(found, "commands", foundCommands);

                IList nodes = CreateList(nodeType);
                nodes.Add(begin);
                nodes.Add(found);
                SetField(definition, "nodes", nodes);

                object gameData = Activator.CreateInstance(gameDataType);
                object effects = Activator.CreateInstance(serviceType, gameData);
                object registry = Activator.CreateInstance(registryType);
                object sink = Activator.CreateInstance(sinkType);
                object handler = Activator.CreateInstance(
                    handlerType, "exploration.investigate", "clue_found");
                Invoke(registry, "Register", handler);
                object gateway = Activator.CreateInstance(
                    gatewayType, registry, sink);
                object runtime = Activator.CreateInstance(
                    runtimeType, effects, gateway);

                Assert.That(Invoke(runtime, "Start", definition, "run-1"),
                    Is.EqualTo(true));
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("Completed"));
                IList facts = (IList)gameDataType.GetField("WorldFacts")
                    .GetValue(gameData);
                Assert.That(facts.Cast<object>().Any(value =>
                    GetField<string>(value, "Key") == "story.clue_found" &&
                    GetField<bool>(value, "BoolValue")), Is.True);

                object cancelledData = Activator.CreateInstance(gameDataType);
                object cancelledEffects = Activator.CreateInstance(
                    serviceType, cancelledData);
                object cancelledRegistry = Activator.CreateInstance(
                    registryType);
                object delayedHandler = Activator.CreateInstance(
                    handlerType,
                    "exploration.investigate",
                    "clue_found",
                    false);
                Invoke(cancelledRegistry, "Register", delayedHandler);
                object cancelledSink = Activator.CreateInstance(sinkType);
                object cancelledGateway = Activator.CreateInstance(
                    gatewayType, cancelledRegistry, cancelledSink);
                object cancelledRuntime = Activator.CreateInstance(
                    runtimeType, cancelledEffects, cancelledGateway);

                Assert.That(Invoke(cancelledRuntime, "Start", definition,
                    "run-cancelled"), Is.EqualTo(true));
                Assert.That(GetProperty<object>(cancelledRuntime, "State")
                    .ToString(), Is.EqualTo("WaitingForInteraction"));
                Invoke(cancelledRuntime, "Cancel", "test-cancel");
                object delayedOperation = GetProperty<object>(
                    delayedHandler, "LastOperation");
                Invoke(delayedOperation, "Complete");

                Assert.That(GetProperty<object>(cancelledRuntime, "State")
                    .ToString(), Is.EqualTo("Failed"));
                IList cancelledFacts = (IList)gameDataType.GetField(
                        "WorldFacts")
                    .GetValue(cancelledData);
                Assert.That(cancelledFacts.Count, Is.EqualTo(0),
                    "A late completion after cancellation must not apply effects.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void NarrativeRuntime_InteractionTimeoutCancelsOperationAndFails()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type interactionParametersType = RequireType(
                "CryingSnow.StackCraft.NarrativeInteractionParameters");
            Type registryType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionRegistry");
            Type sinkType = RequireType(
                "CryingSnow.StackCraft.BufferedGameplayInteractionEventSink");
            Type gatewayType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionGateway");
            Type handlerType = RequireType(
                "CryingSnow.StackCraft.ValidationGameplayInteractionHandler");
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.NarrativeRuntime");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");

            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                SetField(definition, "id", "story.interaction.timeout");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");

                object execute = CreateCommand(
                    commandType, commandKindType, "wait", "ExecuteInteraction");
                object interaction = Activator.CreateInstance(
                    interactionParametersType);
                SetField(interaction, "actionId", "narrative.timeout_test");
                SetField(interaction, "timeoutSeconds", 1f);
                SetField(execute, "interactionParameters", interaction);
                object begin = CreateNode(nodeType, "begin");
                IList commands = CreateList(commandType);
                commands.Add(execute);
                SetField(begin, "commands", commands);
                IList nodes = CreateList(nodeType);
                nodes.Add(begin);
                SetField(definition, "nodes", nodes);

                object data = Activator.CreateInstance(gameDataType);
                object effects = Activator.CreateInstance(serviceType, data);
                object registry = Activator.CreateInstance(registryType);
                object sink = Activator.CreateInstance(sinkType);
                object handler = Activator.CreateInstance(
                    handlerType,
                    "narrative.timeout_test",
                    "completed",
                    false);
                Invoke(registry, "Register", handler);
                object gateway = Activator.CreateInstance(
                    gatewayType, registry, sink);
                object runtime = Activator.CreateInstance(
                    runtimeType, effects, gateway);

                Assert.That(Invoke(runtime, "Start", definition, "run-timeout"),
                    Is.EqualTo(true));
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("WaitingForInteraction"));

                Invoke(runtime, "AdvanceTime", 0.5f);
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("WaitingForInteraction"));
                Invoke(runtime, "AdvanceTime", 0.5f);

                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("Failed"));
                Assert.That(GetProperty<string>(runtime, "FailureReason"),
                    Is.EqualTo("InteractionTimeout"));
                object operation = GetProperty<object>(handler, "LastOperation");
                Assert.That(GetProperty<object>(operation, "State").ToString(),
                    Is.EqualTo("Cancelled"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void NarrativeDirector_MissingQuestOfferFallsBackToLegacyDialogue()
        {
            Type directorType = RequireType(
                "CryingSnow.StackCraft.NarrativeDirector");
            var host = new GameObject("MissingQuestOfferDirectorTest");
            try
            {
                Component director = host.AddComponent(directorType);
                Assert.That(Invoke(
                    director,
                    "TryPlayQuestOffer",
                    "quest_without_narrative_resource",
                    "dialogue-run"), Is.EqualTo(false));
                Assert.That(GetProperty<object>(director, "State").ToString(),
                    Is.EqualTo("Idle"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NarrativeDirector_ReleasesOnlyItsOwnInputLock()
        {
            Type inputType = RequireType(
                "CryingSnow.StackCraft.InputManager");
            Type directorType = RequireType(
                "CryingSnow.StackCraft.NarrativeDirector");
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            var inputHost = new GameObject("NarrativeInputManagerTest");
            var directorHost = new GameObject("NarrativeDirectorTest");
            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            object foreignLock = new object();
            try
            {
                Component input = inputHost.AddComponent(inputType);
                inputType.GetMethod("AddLock", new[] { typeof(object) })
                    .Invoke(input, new[] { foreignLock });
                Component director = directorHost.AddComponent(directorType);

                SetField(definition, "id", "story.director.test");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");
                object end = CreateCommand(
                    commandType, commandKindType, "end", "EndNarrative");
                object begin = CreateNode(nodeType, "begin");
                IList commands = CreateList(commandType);
                commands.Add(end);
                SetField(begin, "commands", commands);
                IList nodes = CreateList(nodeType);
                nodes.Add(begin);
                SetField(definition, "nodes", nodes);

                Assert.That(Invoke(director, "Play", definition, "run-1"),
                    Is.EqualTo(true));
                Assert.That(GetProperty<bool>(input, "IsInputEnabled"),
                    Is.False, "The foreign lock must remain after cleanup.");

                ScriptableObject invalid = ScriptableObject.CreateInstance(
                    definitionType);
                try
                {
                    SetField(invalid, "id", "story.invalid.director");
                    SetField(invalid, "version", 1);
                    SetField(invalid, "entryNodeId", "missing");
                    Assert.That(Invoke(director, "Play", invalid, "run-2"),
                        Is.EqualTo(false));
                    Assert.That(GetProperty<bool>(input, "IsInputEnabled"),
                        Is.False,
                        "Validation failure must release only the narrative lock.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(invalid);
                }
                inputType.GetMethod("RemoveLock", new[] { typeof(object) })
                    .Invoke(input, new[] { foreignLock });
                Assert.That(GetProperty<bool>(input, "IsInputEnabled"),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(directorHost);
                UnityEngine.Object.DestroyImmediate(inputHost);
            }
        }

        [Test]
        public void NarrativeDirector_QueuesSubmitWhileDirectPlaybackIsBusy()
        {
            Type directorType = RequireType(
                "CryingSnow.StackCraft.NarrativeDirector");
            Type panelType = RequireType(
                "CryingSnow.StackCraft.DialoguePanelView");
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type dialogueType = RequireType(
                "CryingSnow.StackCraft.NarrativeDialogueParameters");
            Type requestType = RequireType(
                "CryingSnow.StackCraft.NarrativeTriggerRequest");
            Type queuePolicyType = RequireType(
                "CryingSnow.StackCraft.NarrativeQueuePolicy");
            Type runPolicyType = RequireType(
                "CryingSnow.StackCraft.NarrativeRunPolicy");
            var host = new GameObject("NarrativeBusyQueueTest");
            ScriptableObject first = ScriptableObject.CreateInstance(
                definitionType);
            ScriptableObject queued = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                Component panel = host.AddComponent(panelType);
                Component director = host.AddComponent(directorType);
                SetField(director, "dialoguePanel", panel);

                SetField(first, "id", "story.direct");
                SetField(first, "version", 1);
                SetField(first, "entryNodeId", "begin");
                object line = CreateCommand(
                    commandType, commandKindType, "line", "ShowNarration");
                object dialogue = Activator.CreateInstance(dialogueType);
                SetField(dialogue, "fallbackText", "等待继续");
                SetField(line, "dialogueParameters", dialogue);
                object firstEnd = CreateCommand(
                    commandType, commandKindType, "end", "EndNarrative");
                object firstNode = CreateNode(nodeType, "begin");
                IList firstCommands = CreateList(commandType);
                firstCommands.Add(line);
                firstCommands.Add(firstEnd);
                SetField(firstNode, "commands", firstCommands);
                IList firstNodes = CreateList(nodeType);
                firstNodes.Add(firstNode);
                SetField(first, "nodes", firstNodes);

                SetField(queued, "id", "story.queued");
                SetField(queued, "version", 1);
                SetField(queued, "entryNodeId", "begin");
                object queuedNode = CreateNode(nodeType, "begin");
                IList queuedCommands = CreateList(commandType);
                queuedCommands.Add(CreateCommand(
                    commandType, commandKindType, "end", "EndNarrative"));
                SetField(queuedNode, "commands", queuedCommands);
                IList queuedNodes = CreateList(nodeType);
                queuedNodes.Add(queuedNode);
                SetField(queued, "nodes", queuedNodes);
                IList definitions = CreateList(definitionType);
                definitions.Add(queued);
                SetField(director, "definitions", definitions);

                Assert.That(Invoke(director, "Play", first, "run-direct"),
                    Is.EqualTo(true));
                object request = CreateTrigger(
                    requestType,
                    "trigger-queued",
                    "story.queued",
                    50,
                    Enum.Parse(queuePolicyType, "Queue"),
                    Enum.Parse(runPolicyType, "Repeatable"),
                    "run-queued");
                Assert.That(Invoke(director, "Submit", request).ToString(),
                    Is.EqualTo("Queued"));
                object scheduler = GetField<object>(director, "scheduler");
                Assert.That(GetProperty<int>(scheduler, "QueueCount"),
                    Is.EqualTo(1));

                Invoke(director, "Continue");

                Assert.That(GetProperty<int>(scheduler, "QueueCount"),
                    Is.EqualTo(0));
                Assert.That(GetProperty<object>(director, "State").ToString(),
                    Is.EqualTo("Idle"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(queued);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NarrativeDirector_MissingDialoguePanelFailsWithoutInputLock()
        {
            Type inputType = RequireType(
                "CryingSnow.StackCraft.InputManager");
            Type directorType = RequireType(
                "CryingSnow.StackCraft.NarrativeDirector");
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type dialogueType = RequireType(
                "CryingSnow.StackCraft.NarrativeDialogueParameters");
            var inputHost = new GameObject("NarrativeMissingUiInput");
            var directorHost = new GameObject("NarrativeMissingUiDirector");
            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                Component input = inputHost.AddComponent(inputType);
                Component director = directorHost.AddComponent(directorType);
                SetField(definition, "id", "story.missing.ui");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");
                object line = CreateCommand(
                    commandType, commandKindType, "line", "ShowNarration");
                object dialogue = Activator.CreateInstance(dialogueType);
                SetField(dialogue, "fallbackText", "无法显示的台词");
                SetField(line, "dialogueParameters", dialogue);
                object node = CreateNode(nodeType, "begin");
                IList commands = CreateList(commandType);
                commands.Add(line);
                SetField(node, "commands", commands);
                IList nodes = CreateList(nodeType);
                nodes.Add(node);
                SetField(definition, "nodes", nodes);

                Assert.That(Invoke(director, "Play", definition, "run-ui"),
                    Is.EqualTo(false));
                Assert.That(GetProperty<bool>(input, "IsInputEnabled"),
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(directorHost);
                UnityEngine.Object.DestroyImmediate(inputHost);
            }
        }

        [Test]
        public void NarrativeDirector_RevalidatesRequiredActorsBeforeStarting()
        {
            Type directorType = RequireType(
                "CryingSnow.StackCraft.NarrativeDirector");
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type bindingType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorBinding");
            Type modeType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorResolveMode");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            var host = new GameObject("NarrativeMissingActorTest");
            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                Component director = host.AddComponent(directorType);
                SetField(definition, "id", "story.missing.actor");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");
                object binding = Activator.CreateInstance(bindingType);
                SetField(binding, "roleId", "MissingNpc");
                SetField(binding, "resolveMode", Enum.Parse(
                    modeType, "CardDefinitionId"));
                SetField(binding, "cardDefinitionId", "missing-card");
                IList bindings = CreateList(bindingType);
                bindings.Add(binding);
                SetField(definition, "actorBindings", bindings);
                object node = CreateNode(nodeType, "begin");
                IList commands = CreateList(commandType);
                commands.Add(CreateCommand(
                    commandType, commandKindType, "end", "EndNarrative"));
                SetField(node, "commands", commands);
                IList nodes = CreateList(nodeType);
                nodes.Add(node);
                SetField(definition, "nodes", nodes);

                Assert.That(Invoke(director, "Play", definition, "run-actor"),
                    Is.EqualTo(false));
                Assert.That(GetProperty<string>(director, "FailureReason"),
                    Does.Contain("MissingNpc"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RiverbendSample_AcceptBranchStartsTheRealQuest()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type questRuntimeType = RequireType(
                "CryingSnow.StackCraft.WorldQuestRuntime");
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type effectsType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.NarrativeRuntime");
            UnityEngine.Object definition = Resources.Load(
                "Narratives/Narrative_Riverbend_FirstCommission",
                definitionType);
            Assert.That(definition, Is.Not.Null);
            var host = new GameObject("NarrativeQuestRuntimeTest");
            try
            {
                object gameData = Activator.CreateInstance(gameDataType);
                gameDataType.GetField("ActiveLocationId")
                    .SetValue(gameData, "riverbend");
                Component quests = host.AddComponent(questRuntimeType);
                Invoke(quests, "Initialize", gameData);
                object effects = Activator.CreateInstance(
                    effectsType, gameData, quests);
                object runtime = Activator.CreateInstance(
                    runtimeType, effects);

                Assert.That(Invoke(runtime, "Start", definition,
                    "sample-run"), Is.EqualTo(true));
                Invoke(runtime, "Continue");
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("WaitingForChoice"));
                Assert.That(GetProperty<string>(runtime,
                    "CurrentChoiceTitle"),
                    Is.EqualTo("是否接受委托？"));
                Invoke(runtime, "SelectChoice", "accept");
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("WaitingForInput"));
                Invoke(runtime, "Continue");
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("Completed"),
                    GetProperty<string>(runtime, "FailureReason"));

                IList states = (IList)gameDataType.GetField("WorldQuests")
                    .GetValue(gameData);
                object quest = states.Cast<object>().Single(value =>
                    GetField<string>(value, "QuestId") ==
                    "story_riverbend_whispering_forest_01");
                Assert.That(GetField<object>(quest, "Status").ToString(),
                    Is.EqualTo("Active"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static object CreateNode(Type nodeType, string id)
        {
            object node = Activator.CreateInstance(nodeType);
            SetField(node, "id", id);
            return node;
        }

        private static object CreateCommand(
            Type commandType,
            Type commandKindType,
            string id,
            string kind)
        {
            object command = Activator.CreateInstance(commandType);
            SetField(command, "commandId", id);
            SetField(command, "type", Enum.Parse(commandKindType, kind));
            return command;
        }

        private static object CreateTrigger(
            Type requestType,
            string triggerId,
            string narrativeId,
            int priority,
            object queuePolicy,
            object runPolicy,
            string sourceRunId)
        {
            return Activator.CreateInstance(
                requestType,
                triggerId,
                narrativeId,
                priority,
                queuePolicy,
                runPolicy,
                sourceRunId);
        }

        private static string Submit(object scheduler, object request) =>
            Invoke(scheduler, "Submit", request).ToString();

        private static void CompleteActive(object scheduler) =>
            Invoke(scheduler, "CompleteActive");

        private static string GetActiveNarrativeId(object scheduler)
        {
            object active = GetProperty<object>(scheduler, "ActiveRequest");
            return GetProperty<string>(active, "NarrativeId");
        }

        private static object CreateQuestInteractionEvent(
            Type eventType,
            Type eventKindType,
            Type directionType,
            Type phaseType,
            string eventId,
            string phase,
            string outcome,
            string context)
        {
            return Activator.CreateInstance(
                eventType,
                eventId,
                Enum.Parse(eventKindType, "GameplayInteraction"),
                0L,
                "riverbend",
                "player-1",
                true,
                true,
                "exploration.investigate",
                outcome,
                context,
                1,
                Enum.Parse(directionType, "None"),
                Enum.Parse(phaseType, phase));
        }

        private static IList CreateList(Type itemType) =>
            (IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(itemType));

        private static Type RequireType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Missing type {fullName}.");
            return type;
        }

        private static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Where(candidate => candidate.Name == methodName)
                .Where(candidate => candidate.GetParameters().Length ==
                    arguments.Length)
                .SingleOrDefault(candidate => candidate.GetParameters()
                    .Select((parameter, index) => new
                    {
                        parameter.ParameterType,
                        Value = arguments[index]
                    })
                    .All(pair => pair.Value == null ||
                        pair.ParameterType.IsInstanceOfType(pair.Value)));
            Assert.That(method, Is.Not.Null,
                $"Missing method {target.GetType().Name}.{methodName}.");
            return method.Invoke(target, arguments);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null,
                $"Missing property {target.GetType().Name}.{propertyName}.");
            return (T)property.GetValue(target);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"Missing field {target.GetType().Name}.{fieldName}.");
            return (T)field.GetValue(target);
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"Missing field {target.GetType().Name}.{fieldName}.");
            field.SetValue(target, value);
        }
    }
}
