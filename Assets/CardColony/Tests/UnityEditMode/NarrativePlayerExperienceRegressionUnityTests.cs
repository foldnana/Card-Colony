using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CardColony.Tests
{
    public sealed class NarrativePlayerExperienceRegressionUnityTests
    {
        [Test]
        public void NarrativeLine_AdvancesInlineWithoutOpeningChoiceTray()
        {
            Type viewType = RequireType(
                "CryingSnow.StackCraft.DialoguePanelView");
            var host = new GameObject("NarrativeLineView");
            var tray = new GameObject("ChoiceTray");
            tray.transform.SetParent(host.transform, false);
            tray.SetActive(true);
            try
            {
                Component view = host.AddComponent(viewType);
                SetField(view, "choiceTray", tray);
                int advances = 0;

                Invoke(view, "ShowNarrativeLine",
                    "村长", null, "去森林看看。", (Action)(() => advances++));

                Assert.That(tray.activeSelf, Is.False,
                    "纯对白不应伪装成只有“继续”的选择题。 ");
                Invoke(view, "AdvanceNarrative");
                Assert.That(advances, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Checkpoint_IsTechnicalAndDoesNotInterruptDialogueFlow()
        {
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
            Type actorBindingType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorBinding");
            Type resolveModeType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorResolveMode");
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.NarrativeRuntime");
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type effectsType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                SetField(definition, "id", "test.transparent-checkpoint");
                SetField(definition, "version", 1);
                SetField(definition, "entryNodeId", "begin");
                object mayor = Activator.CreateInstance(actorBindingType);
                SetField(mayor, "roleId", "Mayor");
                SetField(mayor, "resolveMode",
                    Enum.Parse(resolveModeType, "PresentationOnly"));
                IList actors = CreateList(actorBindingType);
                actors.Add(mayor);
                SetField(definition, "actorBindings", actors);
                object node = Activator.CreateInstance(nodeType);
                SetField(node, "id", "begin");
                IList commands = CreateList(commandType);
                commands.Add(CreateCommand(commandType, commandKindType,
                    "safe", "Checkpoint"));
                object line = CreateCommand(commandType, commandKindType,
                    "line", "ShowDialogue");
                object dialogue = Activator.CreateInstance(dialogueType);
                SetField(dialogue, "actorRole", "Mayor");
                SetField(dialogue, "fallbackText", "继续说正事。");
                SetField(line, "dialogueParameters", dialogue);
                commands.Add(line);
                commands.Add(CreateCommand(commandType, commandKindType,
                    "end", "EndNarrative"));
                SetField(node, "commands", commands);
                IList nodes = CreateList(nodeType);
                nodes.Add(node);
                SetField(definition, "nodes", nodes);

                object data = Activator.CreateInstance(gameDataType);
                object runtime = Activator.CreateInstance(runtimeType,
                    Activator.CreateInstance(effectsType, data));
                Assert.That(Invoke(runtime, "Start", definition, "run-1"),
                    Is.True);
                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("WaitingForInput"),
                    "保存检查点不能弹出“剧情已到达安全节点”的确认框。 ");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ExplicitCheckpoint_DoesNotBecomeSkipBarrier()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.NarrativeDefinition");
            Type nodeType = RequireType(
                "CryingSnow.StackCraft.NarrativeNodeDefinition");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type barrierType = RequireType(
                "CryingSnow.StackCraft.NarrativeBarrierType");
            Type dialogueType = RequireType(
                "CryingSnow.StackCraft.NarrativeDialogueParameters");
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.NarrativeRuntime");
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type effectsType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            ScriptableObject definition = ScriptableObject.CreateInstance(
                definitionType);
            try
            {
                SetField(definition, "id", "test.skip-checkpoint");
                SetField(definition, "version", 1);
                SetField(definition, "canSkip", true);
                SetField(definition, "entryNodeId", "begin");
                object node = Activator.CreateInstance(nodeType);
                SetField(node, "id", "begin");
                IList commands = CreateList(commandType);
                object line = CreateCommand(commandType, commandKindType,
                    "line", "ShowNarration");
                object dialogue = Activator.CreateInstance(dialogueType);
                SetField(dialogue, "fallbackText", "一段可以跳过的演出。 ");
                SetField(line, "dialogueParameters", dialogue);
                commands.Add(line);
                object checkpoint = CreateCommand(
                    commandType, commandKindType, "safe", "Checkpoint");
                SetField(checkpoint, "barrierType",
                    Enum.Parse(barrierType, "Checkpoint"));
                commands.Add(checkpoint);
                commands.Add(CreateCommand(commandType, commandKindType,
                    "end", "EndNarrative"));
                SetField(node, "commands", commands);
                IList nodes = CreateList(nodeType);
                nodes.Add(node);
                SetField(definition, "nodes", nodes);
                object runtime = Activator.CreateInstance(runtimeType,
                    Activator.CreateInstance(effectsType,
                        Activator.CreateInstance(gameDataType)));

                Assert.That(Invoke(runtime, "Start", definition, "run-1"),
                    Is.True);
                Assert.That(Invoke(runtime, "SkipToNextBarrier"), Is.True);

                Assert.That(GetProperty<object>(runtime, "State").ToString(),
                    Is.EqualTo("Completed"),
                    "显式标记的 Checkpoint 也不能重新弹出继续选择框。 ");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void FaceActor_DoesNotRotateFlatPhysicalCards()
        {
            Type cardType = RequireType(
                "CryingSnow.StackCraft.CardInstance");
            Type handleType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorHandle");
            Type controlsType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorControlService");
            Type executorType = RequireType(
                "CryingSnow.StackCraft.NarrativeWorldActionExecutor");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type actorParametersType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorActionParameters");
            var actorHost = new GameObject("MerchantCard");
            var targetHost = new GameObject("ThreatCard");
            try
            {
                Component actorCard = actorHost.AddComponent(cardType);
                Component targetCard = targetHost.AddComponent(cardType);
                actorHost.transform.position = Vector3.zero;
                targetHost.transform.position = new Vector3(2f, 0f, 1f);
                Quaternion cardRotation = Quaternion.Euler(90f, 0f, 0f);
                actorHost.transform.rotation = cardRotation;
                object actor = Activator.CreateInstance(handleType,
                    "Merchant", "杂货商", null, actorCard);
                object target = Activator.CreateInstance(handleType,
                    "Threat", "哥布林", null, targetCard);
                Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(
                    typeof(string), handleType);
                object actors = Activator.CreateInstance(dictionaryType);
                dictionaryType.GetMethod("Add")?.Invoke(actors,
                    new[] { "Merchant", actor });
                dictionaryType.GetMethod("Add")?.Invoke(actors,
                    new[] { "Threat", target });
                object controls = Activator.CreateInstance(controlsType);
                object executor = Activator.CreateInstance(executorType,
                    actors, controls, null);
                object command = CreateCommand(commandType, commandKindType,
                    "face", "FaceActor");
                object parameters = Activator.CreateInstance(
                    actorParametersType);
                SetField(parameters, "actorRole", "Merchant");
                SetField(parameters, "targetRole", "Threat");
                SetField(parameters, "duration", 0.5f);
                SetField(command, "actorActionParameters", parameters);

                Invoke(executor, "Execute", command, true);

                Assert.That(Quaternion.Angle(
                    actorHost.transform.rotation, cardRotation),
                    Is.LessThan(0.1f),
                    "卡牌不是3D角色模型，面向指令不能把整张卡横过来。 ");
                Invoke(executor, "Dispose");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actorHost);
                UnityEngine.Object.DestroyImmediate(targetHost);
            }
        }

        [Test]
        public void MerchantAmbush_TransitionsParticipantsAndUsesTwoAllies()
        {
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Assert.That(Enum.GetNames(commandKindType),
                Does.Contain("BeginBackgroundCombat"),
                "剧情需要显式切换为杂货商—哥布林真实战斗。 ");

            const string assetPath =
                "Assets/StackCraft/Resources/Narratives/" +
                "Narrative_Riverbend_MerchantThreat.asset";
            ScriptableObject definition = AssetDatabase.LoadAssetAtPath<
                ScriptableObject>(assetPath);
            Assert.That(definition, Is.Not.Null);
            Assert.That(GetProperty<bool>(definition, "AllowCameraInput"),
                Is.True, "聚焦完成后必须允许玩家控制地图镜头。 ");

            object[] commands = GetProperty<IList>(definition, "Nodes")
                .Cast<object>()
                .SelectMany(node => GetProperty<IList>(node, "Commands")
                    .Cast<object>())
                .ToArray();
            Assert.That(commands.Select(command => GetProperty<object>(
                    command, "Type").ToString()),
                Does.Contain("BeginBackgroundCombat"));
            object combat = commands.Single(command =>
                GetProperty<object>(command, "Type").ToString() ==
                "ExecuteInteraction" &&
                GetProperty<string>(GetProperty<object>(command,
                    "InteractionParameters"), "ActionId") == "core.combat");
            object parameters = GetProperty<object>(combat,
                "InteractionParameters");
            CollectionAssert.AreEquivalent(
                new[] { "Player", "Merchant" },
                GetProperty<IEnumerable>(parameters, "InitiatorRoles")
                    .Cast<string>(),
                "玩家选择帮忙后，玩家和杂货商应属于同一战斗方。 ");

            string directorSource = File.ReadAllText(
                "Assets/StackCraft/Scripts/Narrative/Runtime/" +
                "NarrativeDirector.cs");
            Assert.That(directorSource,
                Does.Not.Contain("EndTriggeringDialogueInteraction();"),
                "剧情启动不能统一关闭人物交互；普通 NPC 对话必须保持交互框，"
                + "只有显式切换冲突时才能结束原会话。 ");
        }

        [Test]
        public void NarrativeTemporaryActor_DoesNotRunAutonomousCombatAiBeforeDecision()
        {
            Type cardType = RequireType(
                "CryingSnow.StackCraft.CardInstance");
            Type combatantType = RequireType(
                "CryingSnow.StackCraft.CardCombatant");
            Type aiType = RequireType(
                "CryingSnow.StackCraft.CardAI");
            var host = new GameObject("NarrativeTemporaryThreat");
            try
            {
                Component card = host.AddComponent(cardType);
                host.AddComponent(combatantType);
                Component ai = host.AddComponent(aiType);
                Invoke(card, "MarkNarrativeTemporary");
                Invoke(ai, "Awake");

                Assert.That(Invoke(ai, "CanMove"), Is.EqualTo(false),
                    "剧情临时怪物只能由剧情或显式战斗命令驱动，"
                    + "不能在玩家选择前自行追击并创建正式战斗。 ");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FocusActor_LocksCameraOnlyUntilMovementCompletes()
        {
            Type inputType = RequireType(
                "CryingSnow.StackCraft.InputManager");
            Type cameraType = RequireType(
                "CryingSnow.StackCraft.CameraController");
            Type serviceType = RequireType(
                "CryingSnow.StackCraft.NarrativePresentationService");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type actorParametersType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorActionParameters");
            Type handleType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorHandle");
            Type cardType = RequireType(
                "CryingSnow.StackCraft.CardInstance");
            var inputHost = new GameObject("InputManager");
            var cameraHost = new GameObject("CameraController");
            var cameraPivot = new GameObject("CameraPivot");
            var actorHost = new GameObject("FocusActor");
            try
            {
                Component input = inputHost.AddComponent(inputType);
                Invoke(input, "Awake");
                Component camera = cameraHost.AddComponent(cameraType);
                cameraPivot.transform.SetParent(cameraHost.transform, false);
                cameraPivot.transform.localPosition = new Vector3(0f, 8f, -8f);
                cameraPivot.transform.localRotation = Quaternion.Euler(
                    45f, 0f, 0f);
                cameraPivot.AddComponent<Camera>();
                SetField(camera, "cameraTransform", cameraPivot.transform);
                Component card = actorHost.AddComponent(cardType);
                actorHost.transform.position = new Vector3(2f, 0f, 3f);
                object actor = Activator.CreateInstance(handleType,
                    "Merchant", "杂货商", null, card);
                object service = Activator.CreateInstance(serviceType,
                    null, camera);
                object command = CreateCommand(commandType, commandKindType,
                    "focus", "FocusActor");
                object actorParameters = Activator.CreateInstance(
                    actorParametersType);
                SetField(actorParameters, "actorRole", "Merchant");
                SetField(actorParameters, "duration", 2f);
                SetField(command, "actorActionParameters", actorParameters);

                object operation = Invoke(service, "Execute",
                    command, actor, false);
                Assert.That(GetProperty<bool>(input, "IsCameraInputEnabled"),
                    Is.False, "镜头移动过程中应避免玩家拖动与演出抢控制权。 ");
                Invoke(operation, "CompleteImmediately");
                Assert.That(GetProperty<bool>(input, "IsCameraInputEnabled"),
                    Is.True, "镜头移动完成后必须立即把控制权还给玩家。 ");
                Invoke(service, "Dispose");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actorHost);
                UnityEngine.Object.DestroyImmediate(cameraPivot);
                UnityEngine.Object.DestroyImmediate(cameraHost);
                UnityEngine.Object.DestroyImmediate(inputHost);
            }
        }

        [Test]
        public void DespawnActor_SucceedsWhenCombatAlreadyDestroyedTemporaryCard()
        {
            Type cardType = RequireType(
                "CryingSnow.StackCraft.CardInstance");
            Type handleType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorHandle");
            Type controlsType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorControlService");
            Type executorType = RequireType(
                "CryingSnow.StackCraft.NarrativeWorldActionExecutor");
            Type commandType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandDefinition");
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Type actorParametersType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorActionParameters");
            var threatHost = new GameObject("DefeatedTemporaryThreat");
            object executor = null;
            try
            {
                Component card = threatHost.AddComponent(cardType);
                object threat = Activator.CreateInstance(handleType,
                    "Threat", "哥布林", null, card);
                Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(
                    typeof(string), handleType);
                object actors = Activator.CreateInstance(dictionaryType);
                dictionaryType.GetMethod("Add")?.Invoke(actors,
                    new[] { "Threat", threat });
                object controls = Activator.CreateInstance(controlsType);
                executor = Activator.CreateInstance(executorType,
                    actors, controls, null);
                FieldInfo temporaryRolesField = executorType.GetField(
                    "temporaryActorRoles",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(temporaryRolesField, Is.Not.Null);
                object temporaryRoles = temporaryRolesField.GetValue(executor);
                temporaryRoles.GetType().GetMethod("Add")?.Invoke(
                    temporaryRoles, new object[] { "Threat" });

                UnityEngine.Object.DestroyImmediate(threatHost);

                object command = CreateCommand(commandType, commandKindType,
                    "despawn", "DespawnActor");
                object parameters = Activator.CreateInstance(
                    actorParametersType);
                SetField(parameters, "actorRole", "Threat");
                SetField(command, "actorActionParameters", parameters);
                object operation = Invoke(executor, "Execute", command, true);

                object result = GetProperty<object>(operation, "Result");
                Assert.That(GetProperty<bool>(result, "Success"), Is.True,
                    "战斗已销毁临时敌人时，剧情清理仍应幂等成功。 ");
            }
            finally
            {
                if (executor != null)
                    Invoke(executor, "Dispose");
                if (threatHost != null)
                    UnityEngine.Object.DestroyImmediate(threatHost);
            }
        }

        [Test]
        public void MerchantAmbush_CinematicDamageCannotKillMerchantBeforeCombat()
        {
            Type cardType = RequireType(
                "CryingSnow.StackCraft.CardInstance");
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type effectsType = RequireType(
                "CryingSnow.StackCraft.WorldEffectService");
            var merchantHost = new GameObject("WoundedMerchant");
            try
            {
                Component merchant = merchantHost.AddComponent(cardType);
                SetField(merchant, "_renderer",
                    merchantHost.GetComponent<MeshRenderer>());
                FieldInfo healthField = cardType.GetField(
                    "<CurrentHealth>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(healthField, Is.Not.Null);
                healthField.SetValue(merchant, 2);
                object effects = Activator.CreateInstance(effectsType,
                    Activator.CreateInstance(gameDataType));

                LogAssert.Expect(LogType.Error, new Regex(
                    "Instantiating material due to calling renderer.material"));
                Invoke(effects, "ApplyDamage",
                    "merchant-ambush", 1, "run-1", "begin",
                    "cinematic-hit", merchant, 3, true);

                Assert.That(GetProperty<int>(merchant, "CurrentHealth"),
                    Is.EqualTo(1),
                    "演出攻击不能在玩家选择帮忙前杀死杂货商。 ");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(merchantHost);
            }
        }

        [Test]
        public void CombatMerge_KeepsNeutralMerchantOnDeclaredPlayerSide()
        {
            Type managerType = RequireType(
                "CryingSnow.StackCraft.CombatManager");
            Type cardType = RequireType(
                "CryingSnow.StackCraft.CardInstance");
            Type taskType = RequireType(
                "CryingSnow.StackCraft.CombatTask");
            var playerHost = new GameObject("MergePlayer");
            var merchantHost = new GameObject("MergeNeutralMerchant");
            var threatHost = new GameObject("MergeThreat");
            try
            {
                Component player = playerHost.AddComponent(cardType);
                Component merchant = merchantHost.AddComponent(cardType);
                Component threat = threatHost.AddComponent(cardType);
                SetField(player, "<CurrentHealth>k__BackingField", 1);
                SetField(merchant, "<CurrentHealth>k__BackingField", 1);
                SetField(threat, "<CurrentHealth>k__BackingField", 1);
                Type cardListType = typeof(List<>).MakeGenericType(cardType);
                IList declaredPlayerSide = (IList)Activator.CreateInstance(
                    cardListType);
                declaredPlayerSide.Add(player);
                declaredPlayerSide.Add(merchant);
                IList declaredEnemySide = (IList)Activator.CreateInstance(
                    cardListType);
                declaredEnemySide.Add(threat);
                object tasks = Activator.CreateInstance(
                    typeof(List<>).MakeGenericType(taskType));
                MethodInfo collect = managerType.GetMethod(
                    "CollectMergedCombatants",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(collect, Is.Not.Null,
                    "合并战斗必须有按既有战斗侧归并参与者的逻辑。 ");
                object[] arguments =
                {
                    declaredPlayerSide,
                    declaredEnemySide,
                    true,
                    tasks,
                    null,
                    null
                };

                collect.Invoke(null, arguments);

                IEnumerable playerSide = (IEnumerable)arguments[4];
                Assert.That(playerSide.Cast<object>(), Does.Contain(merchant),
                    "Neutral 杂货商不能在战斗合并时从玩家一方消失。 ");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerHost);
                UnityEngine.Object.DestroyImmediate(merchantHost);
                UnityEngine.Object.DestroyImmediate(threatHost);
            }
        }

        [Test]
        public void MerchantAmbushAsset_BackgroundCombatHasOnlyCombatPayload()
        {
            const string assetPath =
                "Assets/StackCraft/Resources/Narratives/" +
                "Narrative_Riverbend_MerchantThreat.asset";
            ScriptableObject definition = AssetDatabase.LoadAssetAtPath<
                ScriptableObject>(assetPath);
            Assert.That(definition, Is.Not.Null);
            object[] commands = GetProperty<IList>(definition, "Nodes")
                .Cast<object>()
                .SelectMany(node => GetProperty<IList>(node, "Commands")
                    .Cast<object>())
                .ToArray();
            object combat = commands.Single(command =>
                GetProperty<string>(command, "CommandId") ==
                "merchant_goblin_fight");

            Assert.That(GetProperty<string>(GetProperty<object>(combat,
                    "EffectParameters"), "ResultId"),
                Is.Empty,
                "后台战斗命令不能残留世界效果结果标识。 ");
            Assert.That(GetProperty<IList>(GetProperty<object>(combat,
                    "DialogueParameters"), "Choices"),
                Is.Empty,
                "后台战斗命令不能残留玩家选择项。 ");
        }

        [Test]
        public void NarrativeCombatDefeatRules_SupportLethalPreservedAndLockedDefeat()
        {
            Type ruleType = RequireType(
                "CryingSnow.StackCraft.CombatDefeatRule");
            CollectionAssert.IsSubsetOf(
                new[] { "Lethal", "PreserveAtOne", "LockAtOne" },
                Enum.GetNames(ruleType));

            Type rulesType = RequireType(
                "CryingSnow.StackCraft.CombatDefeatRules");
            object lethal = Enum.Parse(ruleType, "Lethal");
            object preserved = Enum.Parse(ruleType, "PreserveAtOne");
            object locked = Enum.Parse(ruleType, "LockAtOne");

            Assert.That(InvokeStatic(rulesType, "ResolveDamage",
                    3, 5, lethal), Is.EqualTo(3));
            Assert.That(InvokeStatic(rulesType, "ResolveDamage",
                    3, 5, preserved), Is.EqualTo(2));
            Assert.That(InvokeStatic(rulesType, "ResolveDamage",
                    3, 5, locked), Is.EqualTo(2));
            Assert.That(InvokeStatic(rulesType, "ShouldResolveDefeat",
                    1, preserved), Is.EqualTo(true));
            Assert.That(InvokeStatic(rulesType, "ShouldResolveDefeat",
                    1, locked), Is.EqualTo(false));
        }

        [Test]
        public void MerchantAmbush_StartsRealBackgroundCombatBeforePlayerChoice()
        {
            Type commandKindType = RequireType(
                "CryingSnow.StackCraft.NarrativeCommandType");
            Assert.That(Enum.GetNames(commandKindType),
                Does.Contain("BeginBackgroundCombat"));

            const string assetPath =
                "Assets/StackCraft/Resources/Narratives/" +
                "Narrative_Riverbend_MerchantThreat.asset";
            ScriptableObject definition = AssetDatabase.LoadAssetAtPath<
                ScriptableObject>(assetPath);
            object begin = GetProperty<IList>(definition, "Nodes")
                .Cast<object>()
                .Single(node => GetProperty<string>(node, "Id") == "begin");
            object[] commands = GetProperty<IList>(begin, "Commands")
                .Cast<object>()
                .ToArray();
            string[] types = commands.Select(command =>
                GetProperty<object>(command, "Type").ToString()).ToArray();

            int combatIndex = Array.IndexOf(types, "BeginBackgroundCombat");
            int choiceIndex = Array.IndexOf(types, "ShowChoice");
            Assert.That(combatIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(choiceIndex, Is.GreaterThan(combatIndex),
                "杂货商和哥布林必须在玩家选择前就进入真实战斗。 ");
            Assert.That(types, Does.Not.Contain("PlayCinematicAttack"),
                "真实战斗不能再混用只打一下的假攻击演出。 ");
            Assert.That(types, Does.Not.Contain("ApplyDamage"));

            object parameters = GetProperty<object>(commands[combatIndex],
                "InteractionParameters");
            CollectionAssert.AreEquivalent(new[] { "Merchant" },
                GetProperty<IEnumerable>(parameters, "InitiatorRoles")
                    .Cast<string>());
            CollectionAssert.AreEquivalent(new[] { "Threat" },
                GetProperty<IEnumerable>(parameters, "TargetRoles")
                    .Cast<string>());
            object[] arguments = GetProperty<IEnumerable>(parameters,
                    "Arguments").Cast<object>().ToArray();
            Assert.That(arguments.Any(argument =>
                    GetProperty<string>(argument, "Key") ==
                    "friendlyDefeatRule" &&
                    GetProperty<string>(argument, "StringValue") ==
                    "PreserveAtOne"), Is.True,
                "该剧情中杂货商应真实战败但保留人物卡，以便结算被抢。 ");
        }

        [Test]
        public void InteractionToCombatTransition_HasReusableAnimatedHandoff()
        {
            Type interactionType = RequireType(
                "CryingSnow.StackCraft.NpcInteractionManager");
            Assert.That(interactionType.GetMethod(
                    "EndInteractionForCombatTransition"), Is.Not.Null,
                "对话/冲突切换到战斗必须使用可复用的平滑交接入口。 ");

            Type rectType = RequireType("CryingSnow.StackCraft.CombatRect");
            Assert.That(rectType.GetMethod("PlayTransitionIn"), Is.Not.Null,
                "战斗框需要淡入入口，避免边框和卡牌瞬间跳变。 ");
        }

        [Test]
        public void MerchantAmbush_PlayerChoiceTakesOverCombatImmediately()
        {
            const string assetPath =
                "Assets/StackCraft/Resources/Narratives/" +
                "Narrative_Riverbend_MerchantThreat.asset";
            ScriptableObject definition = AssetDatabase.LoadAssetAtPath<
                ScriptableObject>(assetPath);
            object confront = GetProperty<IList>(definition, "Nodes")
                .Cast<object>()
                .Single(node => GetProperty<string>(node, "Id") == "confront");
            object[] commands = GetProperty<IList>(confront, "Commands")
                .Cast<object>()
                .ToArray();
            string[] types = commands.Select(command =>
                GetProperty<object>(command, "Type").ToString()).ToArray();
            int combatIndex = Array.IndexOf(types, "ExecuteInteraction");
            int delayedPresentationIndex = Array.FindIndex(types, value =>
                value is "MoveToActor" or "ShowDialogue" or "Wait");

            Assert.That(combatIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(delayedPresentationIndex < 0 ||
                combatIndex < delayedPresentationIndex, Is.True,
                "玩家选择介入后必须立即接管后台战斗，不能在走位或对白期间继续竞态结算。 ");
        }

        [Test]
        public void NarrativeCombat_SaveDataKeepsRulesAndBackgroundBinding()
        {
            Type combatData = RequireType("CryingSnow.StackCraft.CombatData");
            Assert.That(combatData.GetField("DefeatRules"), Is.Not.Null);
            Assert.That(combatData.GetField("PreservedDefeated"), Is.Not.Null);

            Type runData = RequireType(
                "CryingSnow.StackCraft.NarrativeRunStateData");
            Assert.That(runData.GetField("BackgroundCombats"), Is.Not.Null,
                "后台战斗与剧情分支的绑定必须随剧情检查点保存。 ");
            Type backgroundData = RequireType(
                "CryingSnow.StackCraft.NarrativeBackgroundCombatData");
            Assert.That(backgroundData.GetField("Actors"), Is.Not.Null,
                "后台战斗恢复时还必须恢复演员身份、控制权和战前返回点。 ");
        }

        [Test]
        public void InteractionCombatHandoff_CanPreserveTrueWorldOrigin()
        {
            Type interactionType = RequireType(
                "CryingSnow.StackCraft.NpcInteractionManager");
            Assert.That(interactionType.GetMethod("TryGetReturnPosition"),
                Is.Not.Null);
            Type controlsType = RequireType(
                "CryingSnow.StackCraft.NarrativeActorControlService");
            Assert.That(controlsType.GetMethod("OverrideOrigin"), Is.Not.Null,
                "从交互框切进战斗前必须把人物真正的世界返回点交给剧情控制器。 ");
        }

        private static Type RequireType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Missing type {fullName}.");
            return type;
        }

        private static object Invoke(object target, string methodName,
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
                $"Missing {target.GetType().Name}.{methodName}.");
            return method.Invoke(target, arguments);
        }

        private static object InvokeStatic(Type type, string methodName,
            params object[] arguments)
        {
            MethodInfo method = type.GetMethods(
                    BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Where(candidate => candidate.Name == methodName)
                .SingleOrDefault(candidate => candidate.GetParameters()
                    .Length == arguments.Length);
            Assert.That(method, Is.Not.Null,
                $"Missing {type.Name}.{methodName}.");
            return method.Invoke(null, arguments);
        }

        private static object CreateCommand(Type commandType,
            Type commandKindType, string id, string kind)
        {
            object command = Activator.CreateInstance(commandType);
            SetField(command, "commandId", id);
            SetField(command, "type", Enum.Parse(commandKindType, kind));
            return command;
        }

        private static IList CreateList(Type itemType) =>
            (IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(itemType));

        private static T GetProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null,
                $"Missing {target.GetType().Name}.{propertyName}.");
            return (T)property.GetValue(target);
        }

        private static void SetField(object target, string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"Missing {target.GetType().Name}.{fieldName}.");
            field.SetValue(target, value);
        }
    }
}
