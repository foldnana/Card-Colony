using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CardColony.Tests
{
    public sealed class NarrativeP0CUnityTests
    {
        [Test]
        public void CombatOutcome_DeclaresAllTerminalResultsAndSessionMapping()
        {
            Type resultType = RequireType(
                "CryingSnow.StackCraft.CombatOutcomeResult");
            CollectionAssert.IsSubsetOf(
                new[] { "Victory", "Defeat", "Retreated", "Aborted" },
                Enum.GetNames(resultType));

            Type outcomeType = RequireType(
                "CryingSnow.StackCraft.CombatOutcome");
            foreach (string property in new[]
                     {
                         "OriginalSessionId", "FinalSessionId", "Result",
                         "EndReason", "SurvivingActorIds", "DefeatedActorIds"
                     })
            {
                Assert.That(outcomeType.GetProperty(property), Is.Not.Null,
                    $"CombatOutcome 缺少 {property}。");
            }
        }

        [Test]
        public void CombatSessionTracker_MergeRoutesFinalOutcomeToOriginalSession()
        {
            Type trackerType = RequireType(
                "CryingSnow.StackCraft.CombatSessionOutcomeTracker");
            Type resultType = RequireType(
                "CryingSnow.StackCraft.CombatOutcomeResult");
            object tracker = Activator.CreateInstance(trackerType);

            Invoke(tracker, "Register", "story-session", "combat-a");
            Invoke(tracker, "RemapFinalSession", "combat-a", "combat-b");
            object outcome = Invoke(
                tracker,
                "CreateOutcome",
                "story-session",
                Enum.Parse(resultType, "Victory"),
                "EnemiesDefeated",
                new[] { "hero" },
                new[] { "goblin" });

            Assert.That(GetProperty<string>(outcome, "OriginalSessionId"),
                Is.EqualTo("story-session"));
            Assert.That(GetProperty<string>(outcome, "FinalSessionId"),
                Is.EqualTo("combat-b"));
            Assert.That(GetProperty<object>(outcome, "Result").ToString(),
                Is.EqualTo("Victory"));
        }

        [Test]
        public void CombatOutcomeRules_DistinguishAllFourTerminalStates()
        {
            Type rules = RequireType(
                "CryingSnow.StackCraft.CombatOutcomeRules");
            Assert.That(InvokeStatic(rules, "Resolve",
                    true, false, false, false).ToString(),
                Is.EqualTo("Victory"));
            Assert.That(InvokeStatic(rules, "Resolve",
                    false, true, false, false).ToString(),
                Is.EqualTo("Defeat"));
            Assert.That(InvokeStatic(rules, "Resolve",
                    false, true, true, false).ToString(),
                Is.EqualTo("Retreated"));
            Assert.That(InvokeStatic(rules, "Resolve",
                    true, true, false, true).ToString(),
                Is.EqualTo("Aborted"));
        }

        [Test]
        public void InvestigationHandler_UsesGenericGatewayAndPublishesResult()
        {
            Type registryType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionRegistry");
            Type sinkType = RequireType(
                "CryingSnow.StackCraft.BufferedGameplayInteractionEventSink");
            Type gatewayType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionGateway");
            Type requestType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionRequest");
            Type handlerType = RequireType(
                "CryingSnow.StackCraft.InvestigationGameplayInteractionHandler");

            object registry = Activator.CreateInstance(registryType);
            object sink = Activator.CreateInstance(sinkType);
            object handler = Activator.CreateInstance(handlerType);
            Assert.That(Invoke(registry, "Register", handler), Is.True);
            object gateway = Activator.CreateInstance(
                gatewayType, registry, sink);
            object request = Activator.CreateInstance(
                requestType, "investigate-op", "exploration.investigate");
            requestType.GetProperty("ContextId")?.SetValue(
                request, "riverbend.old-well");

            object operation = Invoke(gateway, "Execute", request);
            object result = GetProperty<object>(operation, "Result");

            Assert.That(GetProperty<object>(result, "State").ToString(),
                Is.EqualTo("Completed"));
            Assert.That(GetProperty<string>(result, "OutcomeId"),
                Is.EqualTo("clue_found"));
            IList events = GetProperty<IList>(sink, "Events");
            Assert.That(events.Count, Is.EqualTo(2),
                "调查也必须只通过通用 Gateway 发布 Started 与 Resolved。");
        }

        [Test]
        public void NarrativeSaveData_CapturesSafeCheckpointAndWaitingInteraction()
        {
            Type runType = RequireType(
                "CryingSnow.StackCraft.NarrativeRunStateData");
            Type waitType = RequireType(
                "CryingSnow.StackCraft.NarrativeWaitingInteractionData");
            object run = Activator.CreateInstance(runType);

            foreach (string field in new[]
                     {
                         "NarrativeId", "NarrativeVersion", "RunId",
                         "CheckpointNodeId", "CheckpointCommandIndex",
                         "SelectedChoiceIds", "CommittedResultIds",
                         "WaitingInteraction"
                     })
            {
                Assert.That(runType.GetField(field), Is.Not.Null,
                    $"剧情运行存档缺少 {field}。");
            }
            foreach (string field in new[]
                     {
                         "OperationId", "ActionId", "ContextId",
                         "RecoveryToken",
                         "InitiatorActorIds", "TargetActorIds"
                     })
            {
                Assert.That(waitType.GetField(field), Is.Not.Null,
                    $"等待互动存档缺少 {field}。");
            }
        }

        [Test]
        public void NarrativeCommands_ExposeExplicitInteractionOutcomeBranch()
        {
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Assert.That(Enum.GetNames(commandType),
                Does.Contain("BranchByInteractionOutcome"));
        }

        [Test]
        public void CheckpointResume_DoesNotRepeatEffect_ButNewRunCanApply()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type effectType = RequireType(
                "CryingSnow.StackCraft.NarrativeEffectParameters");
            Type effectKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeEffectType");
            Type dataType = RequireType("CryingSnow.StackCraft.GameData");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.NarrativeRuntime");

            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                SetField(definition, "id", "story.p0c.checkpoint");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");
                object node = Activator.CreateInstance(nodeType);
                SetField(node, "id", "begin");
                IList commands = CreateList(commandType);

                object increment = CreateCommand(
                    commandType, commandKindType, "increment", "SetWorldFact");
                object effect = Activator.CreateInstance(effectType);
                SetField(effect, "effectType", Enum.Parse(
                    effectKindType, "IncrementWorldFactInt"));
                SetField(effect, "resultId", "increment_once");
                SetField(effect, "targetId", "p0c.checkpoint.counter");
                SetField(effect, "intValue", 1);
                SetField(increment, "effectParameters", effect);
                commands.Add(increment);
                commands.Add(CreateCommand(
                    commandType, commandKindType, "safe", "Checkpoint"));
                commands.Add(CreateCommand(
                    commandType, commandKindType, "end", "EndNarrative"));
                SetField(node, "commands", commands);
                IList nodes = CreateList(nodeType);
                nodes.Add(node);
                SetField(definition, "nodes", nodes);

                object data = Activator.CreateInstance(dataType);
                object effects = Activator.CreateInstance(serviceType, data);
                object first = Activator.CreateInstance(runtimeType, effects);
                Assert.That(Invoke(first, "Start", definition, "run-1"),
                    Is.True);
                Assert.That(GetProperty<object>(first, "State").ToString(),
                    Is.EqualTo("WaitingAtBarrier"));

                object history = dataType.GetField("Narrative").GetValue(data);
                object saved = history.GetType().GetField("ActiveRun")
                    .GetValue(history);
                object resumed = Activator.CreateInstance(runtimeType, effects);
                Assert.That(Invoke(resumed, "Resume", definition, saved),
                    Is.True);
                Assert.That(GetProperty<object>(resumed, "State").ToString(),
                    Is.EqualTo("Completed"));
                Assert.That(ReadIntFact(data, "p0c.checkpoint.counter"),
                    Is.EqualTo(1));

                object secondRun = Activator.CreateInstance(runtimeType, effects);
                Assert.That(Invoke(secondRun, "Start", definition, "run-2"),
                    Is.True);
                Assert.That(ReadIntFact(data, "p0c.checkpoint.counter"),
                    Is.EqualTo(2),
                    "新的 RunId 必须允许可重复剧情再次提交正式结果。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Runtime_ExplicitBranchCommandUsesLastInteractionOutcome()
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
            Type branchType = RequireType(
                "CryingSnow.StackCraft.NarrativeInteractionOutcomeBranch");
            Type registryType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionRegistry");
            Type sinkType = RequireType(
                "CryingSnow.StackCraft.BufferedGameplayInteractionEventSink");
            Type gatewayType = RequireType(
                "CryingSnow.StackCraft.GameplayInteractionGateway");
            Type handlerType = RequireType(
                "CryingSnow.StackCraft.ValidationGameplayInteractionHandler");
            Type dataType = RequireType("CryingSnow.StackCraft.GameData");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.NarrativeRuntime");

            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                SetField(definition, "id", "story.p0c.explicit-branch");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");
                object execute = CreateCommand(commandType, commandKindType,
                    "investigate", "ExecuteInteraction");
                object executeParameters = Activator.CreateInstance(
                    interactionType);
                SetField(executeParameters, "actionId", "test.investigate");
                SetField(execute, "interactionParameters", executeParameters);

                object branch = CreateCommand(commandType, commandKindType,
                    "branch", "BranchByInteractionOutcome");
                object branchParameters = Activator.CreateInstance(
                    interactionType);
                IList branches = CreateList(branchType);
                object foundBranch = Activator.CreateInstance(branchType);
                SetField(foundBranch, "outcomeId", "clue_found");
                SetField(foundBranch, "targetNodeId", "found");
                branches.Add(foundBranch);
                SetField(branchParameters, "outcomeBranches", branches);
                SetField(branch, "interactionParameters", branchParameters);

                object begin = Activator.CreateInstance(nodeType);
                SetField(begin, "id", "begin");
                IList beginCommands = CreateList(commandType);
                beginCommands.Add(execute);
                beginCommands.Add(branch);
                SetField(begin, "commands", beginCommands);
                object found = Activator.CreateInstance(nodeType);
                SetField(found, "id", "found");
                IList foundCommands = CreateList(commandType);
                foundCommands.Add(CreateCommand(
                    commandType, commandKindType, "end", "EndNarrative"));
                SetField(found, "commands", foundCommands);
                IList nodes = CreateList(nodeType);
                nodes.Add(begin);
                nodes.Add(found);
                SetField(definition, "nodes", nodes);

                object registry = Activator.CreateInstance(registryType);
                Invoke(registry, "Register", Activator.CreateInstance(
                    handlerType, "test.investigate", "clue_found"));
                object gateway = Activator.CreateInstance(
                    gatewayType,
                    registry,
                    Activator.CreateInstance(sinkType));
                object data = Activator.CreateInstance(dataType);
                object runtime = Activator.CreateInstance(
                    runtimeType,
                    Activator.CreateInstance(serviceType, data),
                    gateway);

                Assert.That(Invoke(runtime, "Start", definition, "run-1"),
                    Is.True);
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("Completed"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CoinReward_UsesSharedIdempotencyAndAllowsNewRun()
        {
            Type dataType = RequireType("CryingSnow.StackCraft.GameData");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            Type requestType = RequireType(
                "CryingSnow.StackCraft.WorldEffectRequest");
            Type effectType = RequireType(
                "CryingSnow.StackCraft.NarrativeEffectType");
            object data = Activator.CreateInstance(dataType);
            object service = Activator.CreateInstance(serviceType, data);
            object request = Activator.CreateInstance(
                requestType,
                Enum.Parse(effectType, "GiveCoins"),
                "reward",
                "4bda315463bf4b73b63f1d232fb522e4",
                string.Empty,
                5,
                false);

            object first = Invoke(service, "Apply",
                "story.reward", 1, "run-1", "victory", request);
            object duplicate = Invoke(service, "Apply",
                "story.reward", 1, "run-1", "victory", request);
            Assert.That(GetProperty<bool>(first, "Applied"), Is.True);
            Assert.That(GetProperty<bool>(duplicate, "AlreadyApplied"),
                Is.True);
            Assert.That(BackpackCount(data), Is.EqualTo(5));

            object secondRun = Invoke(service, "Apply",
                "story.reward", 1, "run-2", "victory", request);
            Assert.That(GetProperty<bool>(secondRun, "Applied"), Is.True);
            Assert.That(BackpackCount(data), Is.EqualTo(10));
        }

        [Test]
        public void MerchantAmbushAsset_UsesCombatInvestigationAndReward()
        {
            const string path =
                "Assets/StackCraft/Resources/Narratives/" +
                "Narrative_Riverbend_MerchantThreat.asset";
            ScriptableObject definition = AssetDatabase.LoadAssetAtPath<
                ScriptableObject>(path);
            Assert.That(definition, Is.Not.Null);
            Assert.That(GetProperty<string>(definition, "Id"),
                Is.EqualTo("npc_event.riverbend-grocer-ambush"));

            IList nodes = GetProperty<IList>(definition, "Nodes");
            object[] commands = nodes.Cast<object>()
                .SelectMany(node => GetProperty<IList>(node, "Commands")
                    .Cast<object>())
                .ToArray();
            string[] kinds = commands.Select(command =>
                GetProperty<object>(command, "Type").ToString()).ToArray();
            Assert.That(kinds, Does.Contain("ExecuteInteraction"));
            Assert.That(kinds, Does.Contain("BranchByInteractionOutcome"));
            Assert.That(kinds, Does.Contain("Checkpoint"));

            string[] actions = commands
                .Where(command => GetProperty<object>(command, "Type")
                    .ToString() == "ExecuteInteraction")
                .Select(command => GetProperty<object>(command,
                    "InteractionParameters"))
                .Select(value => GetProperty<string>(value, "ActionId"))
                .ToArray();
            Assert.That(actions, Does.Contain("core.combat"));
            Assert.That(actions, Does.Contain("exploration.investigate"));
        }

        private static Type RequireType(string name)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name, false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, $"未找到类型 {name}。");
            return type;
        }

        private static object Invoke(object target, string name,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Where(value => value.Name == name)
                .FirstOrDefault(value =>
                    value.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null,
                $"未找到 {target.GetType().Name}.{name}。");
            return method.Invoke(target, arguments);
        }

        private static object InvokeStatic(Type type, string name,
            params object[] arguments)
        {
            MethodInfo method = type.GetMethods(
                    BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .FirstOrDefault(value => value.Name == name &&
                    value.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null,
                $"未找到 {type.Name}.{name}。");
            return method.Invoke(null, arguments);
        }

        private static T GetProperty<T>(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null,
                $"未找到 {target.GetType().Name}.{name}。");
            return (T)property.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"未找到 {target.GetType().Name}.{name}。");
            field.SetValue(target, value);
        }

        private static IList CreateList(Type itemType) =>
            (IList)Activator.CreateInstance(
                typeof(System.Collections.Generic.List<>).MakeGenericType(
                    itemType));

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

        private static int ReadIntFact(object gameData, string key)
        {
            IList facts = (IList)gameData.GetType().GetField("WorldFacts")
                .GetValue(gameData);
            object fact = facts.Cast<object>().First(value =>
                (string)value.GetType().GetField("Key").GetValue(value) == key);
            return (int)fact.GetType().GetField("IntValue").GetValue(fact);
        }

        private static int BackpackCount(object gameData)
        {
            object backpack = Invoke(gameData, "EnsureBackpack");
            return GetProperty<int>(backpack, "Count");
        }
    }
}
