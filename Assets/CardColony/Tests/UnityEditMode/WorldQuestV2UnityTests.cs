using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CardColony.Tests
{
    public sealed class WorldQuestV2UnityTests
    {
        private const string QuestId =
            "story_riverbend_whispering_forest_01";

        [Test]
        public void V2State_ContainsTheVersionedBranchingContract()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type stateType = RequireType(
                "CryingSnow.StackCraft.WorldQuestStateData");
            Type statusType = RequireType(
                "CryingSnow.StackCraft.WorldQuestStatus");

            Assert.That(
                (int)gameDataType.GetField(
                    "CurrentWorldQuestStateVersion",
                    BindingFlags.Public | BindingFlags.Static)
                    .GetRawConstantValue(),
                Is.EqualTo(2));
            CollectionAssert.AreEqual(
                new[]
                {
                    "Locked", "Available", "Active", "Suspended",
                    "ReadyToTurnIn", "Completed", "Failed", "Cancelled"
                },
                Enum.GetNames(statusType));

            string[] requiredFields =
            {
                "QuestId", "Status", "ActiveStageId",
                "ObjectiveProgress", "SelectedChoiceId", "OutcomeId",
                "StatusReasonId", "SuspendedFromStatus", "RunNumber",
                "CompletionCount", "AvailableSinceWorldHour",
                "AcceptedWorldHour", "LastUpdatedWorldHour",
                "LastCompletedWorldHour", "NextAvailableWorldHour",
                "Revision", "AppliedOperationIds"
            };
            foreach (string fieldName in requiredFields)
            {
                Assert.That(
                    stateType.GetField(fieldName),
                    Is.Not.Null,
                    $"Missing V2 state field {fieldName}.");
            }
        }

        [Test]
        public void MainQuestAsset_UsesStagesAndAStableOutcome()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.WorldQuestDefinition");
            UnityEngine.Object definition = Resources.LoadAll(
                    "WorldQuests",
                    definitionType)
                .Single(candidate =>
                    GetProperty<string>(candidate, "Id") == QuestId);

            Assert.That(
                GetProperty<int>(definition, "SchemaVersion"),
                Is.EqualTo(2));
            IList stages = GetProperty<IList>(definition, "Stages");
            Assert.That(stages.Count, Is.EqualTo(4));
            CollectionAssert.AreEqual(
                new[]
                {
                    "prepare_supplies", "travel_to_forest",
                    "investigate_forest", "report_to_chief"
                },
                stages.Cast<object>()
                    .Select(stage => GetProperty<string>(stage, "StageId"))
                    .ToArray());
            IList outcomes = GetProperty<IList>(definition, "Outcomes");
            Assert.That(outcomes.Count, Is.EqualTo(1));
            Assert.That(
                GetProperty<string>(outcomes[0], "OutcomeId"),
                Is.EqualTo("threat_reported"));
        }

        [Test]
        public void GenericEngine_ProgressesTheMainQuestFromTypedEvents()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.WorldQuestDefinition");
            Type engineType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEngine");
            Type eventType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEvent");
            Type eventKindType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEventType");
            Type directionType = RequireType(
                "CryingSnow.StackCraft.WorldQuestTradeDirection");

            object gameData = Activator.CreateInstance(gameDataType);
            gameDataType.GetField("ActiveLocationId")
                .SetValue(gameData, "riverbend");
            Array definitions = Resources.LoadAll(
                "WorldQuests",
                definitionType);
            object engine = Activator.CreateInstance(
                engineType,
                gameData,
                definitions);
            Invoke(engine, "Initialize");

            AssertSuccess(Invoke(engine, "TryAccept", QuestId));
            AssertStage(gameDataType, gameData, "prepare_supplies", "Active");

            object purchase = CreateEvent(
                eventType,
                eventKindType,
                directionType,
                "purchase-1",
                "MarketTradeCommitted",
                "riverbend",
                "food",
                "riverbend-market",
                1,
                "PlayerBuys",
                true,
                true);
            AssertSuccess(Invoke(engine, "ReportEvent", purchase));
            AssertStage(gameDataType, gameData, "travel_to_forest", "Active");

            object entered = CreateEvent(
                eventType,
                eventKindType,
                directionType,
                "enter-1",
                "LocationEntered",
                "whispering-forest",
                "whispering-forest",
                string.Empty,
                1,
                "None",
                true,
                true);
            AssertSuccess(Invoke(engine, "ReportEvent", entered));
            AssertStage(gameDataType, gameData, "investigate_forest", "Active");

            object defeated = CreateEvent(
                eventType,
                eventKindType,
                directionType,
                "defeat-1",
                "CardDefeated",
                "whispering-forest",
                "366be2e0e40c4b4d93229a94ae7133ae",
                string.Empty,
                1,
                "None",
                true,
                true);
            AssertSuccess(Invoke(engine, "ReportEvent", defeated));
            AssertStage(gameDataType, gameData, "report_to_chief", "Active");

            object talked = CreateEvent(
                eventType,
                eventKindType,
                directionType,
                "talk-1",
                "NpcTalked",
                "riverbend",
                "riverbend-village-chief",
                string.Empty,
                1,
                "None",
                true,
                true);
            AssertSuccess(Invoke(engine, "ReportEvent", talked));
            AssertStage(
                gameDataType,
                gameData,
                "report_to_chief",
                "ReadyToTurnIn");
        }

        [Test]
        public void DuplicateEvent_IsProcessedExactlyOnce()
        {
            EngineFixture fixture = CreateMainQuestEngine();
            AssertSuccess(Invoke(fixture.Engine, "TryAccept", QuestId));
            object purchase = CreateEvent(
                fixture.EventType,
                fixture.EventKindType,
                fixture.DirectionType,
                "same-event",
                "MarketTradeCommitted",
                "riverbend",
                "food",
                "riverbend-market",
                1,
                "PlayerBuys",
                true,
                true);

            AssertSuccess(Invoke(fixture.Engine, "ReportEvent", purchase));
            AssertSuccess(Invoke(fixture.Engine, "ReportEvent", purchase));
            AssertStage(
                fixture.GameDataType,
                fixture.GameData,
                "travel_to_forest",
                "Active");
            IList ids = (IList)fixture.GameDataType
                .GetField("ProcessedWorldQuestEventIds")
                .GetValue(fixture.GameData);
            Assert.That(ids.Count, Is.EqualTo(1));
        }

        [Test]
        public void V1State_MigratesByLegacyNumericStatus()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type stateType = RequireType(
                "CryingSnow.StackCraft.WorldQuestStateData");
            Type statusType = RequireType(
                "CryingSnow.StackCraft.WorldQuestStatus");
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.WorldQuestDefinition");
            Type engineType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEngine");
            object gameData = Activator.CreateInstance(gameDataType);
            gameDataType.GetField("WorldQuestStateVersion")
                .SetValue(gameData, 1);
            object legacyState = Activator.CreateInstance(stateType);
            stateType.GetField("QuestId").SetValue(legacyState, QuestId);
            stateType.GetField("Status").SetValue(
                legacyState,
                Enum.ToObject(statusType, 1));
            stateType.GetField("ObjectiveIndex").SetValue(legacyState, 2);
            stateType.GetField("CurrentAmount").SetValue(legacyState, 0);
            stateType.GetField("AcceptanceRewardClaimed")
                .SetValue(legacyState, true);
            ((IList)gameDataType.GetField("WorldQuests")
                    .GetValue(gameData))
                .Add(legacyState);

            UnityEngine.Object[] definitions = Resources.LoadAll(
                "WorldQuests",
                definitionType);
            object engine = Activator.CreateInstance(
                engineType,
                gameData,
                definitions);
            Invoke(engine, "Initialize");

            Assert.That(
                gameDataType.GetField("WorldQuestStateVersion")
                    .GetValue(gameData),
                Is.EqualTo(2));
            AssertStage(
                gameDataType,
                gameData,
                "investigate_forest",
                "Active");
            IList operationIds = (IList)stateType
                .GetField("AppliedOperationIds")
                .GetValue(legacyState);
            Assert.That(operationIds.Count, Is.EqualTo(2));
        }

        [Test]
        public void FailedCompletionReward_RollsBackTheWholeTransaction()
        {
            EngineFixture fixture = CreateMainQuestEngine();
            AdvanceMainQuestToTurnIn(fixture);
            int before = BackpackCount(
                fixture.GameDataType,
                fixture.GameData);

            object result = Invoke(
                fixture.Engine,
                "TryTurnIn",
                QuestId,
                "threat_reported",
                "riverbend-village-chief");

            Assert.That(GetProperty<bool>(result, "Success"), Is.False);
            AssertStage(
                fixture.GameDataType,
                fixture.GameData,
                "report_to_chief",
                "ReadyToTurnIn");
            Assert.That(
                BackpackCount(fixture.GameDataType, fixture.GameData),
                Is.EqualTo(before));
        }

        [Test]
        public void QuestEffects_SuspendAndResumeAnotherQuestWithoutLosingStage()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.WorldQuestDefinition");
            Type effectType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEffectDefinition");
            Type effectKindType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEffectType");
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type engineType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEngine");
            UnityEngine.Object template = Resources.LoadAll(
                    "WorldQuests",
                    definitionType)
                .Single(candidate =>
                    GetProperty<string>(candidate, "Id") == QuestId);
            object sharedStages = GetField(template, "stages");

            UnityEngine.Object target = CreateQuestDefinition(
                definitionType,
                "side_test_target_01",
                sharedStages,
                null);
            object suspend = CreateEffect(
                effectType,
                effectKindType,
                "suspend_target",
                "SuspendQuest",
                "side_test_target_01",
                "story_conflict");
            UnityEngine.Object interrupter = CreateQuestDefinition(
                definitionType,
                "side_test_interrupter_01",
                sharedStages,
                suspend);
            object resume = CreateEffect(
                effectType,
                effectKindType,
                "resume_target",
                "ResumeQuest",
                "side_test_target_01",
                string.Empty);
            UnityEngine.Object resolver = CreateQuestDefinition(
                definitionType,
                "side_test_resolver_01",
                sharedStages,
                resume);

            object gameData = Activator.CreateInstance(gameDataType);
            var definitions = new[] { target, interrupter, resolver };
            object engine = Activator.CreateInstance(
                engineType,
                gameData,
                definitions);
            Invoke(engine, "Initialize");
            AssertSuccess(Invoke(engine, "TryAccept", "side_test_target_01"));
            AssertSuccess(Invoke(
                engine,
                "TryAccept",
                "side_test_interrupter_01"));
            AssertQuestState(
                gameDataType,
                gameData,
                "side_test_target_01",
                "prepare_supplies",
                "Suspended");
            AssertSuccess(Invoke(
                engine,
                "TryAccept",
                "side_test_resolver_01"));
            AssertQuestState(
                gameDataType,
                gameData,
                "side_test_target_01",
                "prepare_supplies",
                "Active");

            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(interrupter);
            UnityEngine.Object.DestroyImmediate(resolver);
        }

        [Test]
        public void RuntimeContract_IsGenericAndContainsNoQuestSpecificRuleType()
        {
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.WorldQuestRuntime");
            Type eventType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEvent");

            Assert.That(
                runtimeType.GetMethod(
                    "ReportEvent",
                    new[] { eventType }),
                Is.Not.Null);
            Assert.That(
                runtimeType.GetMethod(
                    "TryTurnIn",
                    new[]
                    {
                        typeof(string), typeof(string), typeof(string)
                    }),
                Is.Not.Null);
            Assert.That(
                runtimeType.GetMethod("GetQuestList", Type.EmptyTypes),
                Is.Not.Null);
            Assert.That(
                FindTypeOrNull(
                    "CryingSnow.StackCraft.RiverbendForestQuestRules"),
                Is.Null);
        }

        [Test]
        public void RuntimeAutosave_WaitsUntilNpcInteractionEnds()
        {
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.WorldQuestRuntime");
            MethodInfo canFlush = runtimeType.GetMethod(
                "CanFlushPendingAutosave",
                BindingFlags.Static |
                BindingFlags.NonPublic |
                BindingFlags.Public,
                null,
                new[]
                {
                    typeof(bool), typeof(bool), typeof(bool),
                    typeof(float), typeof(float), typeof(float),
                    typeof(float)
                },
                null);

            Assert.That(
                canFlush,
                Is.Not.Null,
                "任务自动存档需要显式避让正在进行的人物互动，防止交谈按钮刚打开对话就结束互动。");
            Assert.That(
                canFlush.Invoke(
                    null,
                    new object[]
                    {
                        true, true, true,
                        20f, 0f, 0f, 0f
                    }),
                Is.False,
                "人物互动期间必须保留待存档状态，不能立即触发会清理互动框的存档流程。");
            Assert.That(
                canFlush.Invoke(
                    null,
                    new object[]
                    {
                        true, false, true,
                        20f, 0f, 0f, 0f
                    }),
                Is.True,
                "关键任务变化在人物互动结束后必须立即补存档。");
            Assert.That(
                canFlush.Invoke(
                    null,
                    new object[]
                    {
                        false, false, true,
                        20f, 0f, 0f, 0f
                    }),
                Is.False,
                "没有待处理任务变更时不应触发额外存档。");
        }

        [Test]
        public void RuntimeAutosave_CoalescesOrdinaryProgressChanges()
        {
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.WorldQuestRuntime");
            MethodInfo canFlush = runtimeType.GetMethod(
                "CanFlushPendingAutosave",
                BindingFlags.Static |
                BindingFlags.NonPublic |
                BindingFlags.Public,
                null,
                new[]
                {
                    typeof(bool), typeof(bool), typeof(bool),
                    typeof(float), typeof(float), typeof(float),
                    typeof(float)
                },
                null);

            Assert.That(canFlush, Is.Not.Null);
            Assert.That(
                canFlush.Invoke(
                    null,
                    new object[]
                    {
                        true, false, false,
                        12f, 0f, 11f, 0f
                    }),
                Is.False,
                "最近仍有任务进度变化时，应继续等待静默窗口以合并连续事件。");
            Assert.That(
                canFlush.Invoke(
                    null,
                    new object[]
                    {
                        true, false, false,
                        9f, 0f, 0f, 0f
                    }),
                Is.False,
                "普通任务自动存档之间必须保留最小间隔。");
            Assert.That(
                canFlush.Invoke(
                    null,
                    new object[]
                    {
                        true, false, false,
                        11f, 0f, 0f, 0f
                    }),
                Is.True,
                "静默窗口和最小间隔都满足后应写入一次合并存档。");
        }

        [Test]
        public void RuntimeAutosave_MaximumDelayEventuallyFlushesDirtyProgress()
        {
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.WorldQuestRuntime");
            MethodInfo canFlush = runtimeType.GetMethod(
                "CanFlushPendingAutosave",
                BindingFlags.Static |
                BindingFlags.NonPublic |
                BindingFlags.Public,
                null,
                new[]
                {
                    typeof(bool), typeof(bool), typeof(bool),
                    typeof(float), typeof(float), typeof(float),
                    typeof(float)
                },
                null);

            Assert.That(canFlush, Is.Not.Null);
            Assert.That(
                canFlush.Invoke(
                    null,
                    new object[]
                    {
                        true, false, false,
                        31f, 0f, 30.5f, 29f
                    }),
                Is.True,
                "持续产生任务事件时也必须在最长等待时间后落盘，不能无限延期。");
        }

        [Test]
        public void RuntimeAutosave_IgnoresQuestEventsThatChangeNothing()
        {
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.WorldQuestRuntime");
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Assert.That(
                runtimeType.GetProperty("Instance").GetValue(null),
                Is.Null,
                "测试开始前不应残留任务运行时实例。");

            var host = new GameObject("World Quest Autosave Test");
            Component runtime = host.AddComponent(runtimeType);
            try
            {
                object gameData = Activator.CreateInstance(gameDataType);
                gameDataType.GetField("ActiveLocationId")
                    .SetValue(gameData, "riverbend");
                Invoke(runtime, "Initialize", gameData);

                FieldInfo savePending = runtimeType.GetField(
                    "savePending",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(savePending, Is.Not.Null);
                savePending.SetValue(runtime, false);

                object result = Invoke(
                    runtime,
                    "ReportNpcTalked",
                    "npc-without-any-quest",
                    "riverbend");
                Assert.That(
                    GetProperty<object>(result, "Code").ToString(),
                    Is.EqualTo("NoChange"));
                Assert.That(
                    savePending.GetValue(runtime),
                    Is.False,
                    "不影响任务状态的普通事件不能安排磁盘存档。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Tracking_IdempotentOperationsReportNoChange()
        {
            EngineFixture fixture = CreateMainQuestEngine();
            AssertSuccess(Invoke(fixture.Engine, "TryAccept", QuestId));

            object repeatedTrack = Invoke(
                fixture.Engine,
                "SetTrackedQuest",
                QuestId);
            Assert.That(
                GetProperty<object>(repeatedTrack, "Code").ToString(),
                Is.EqualTo("NoChange"),
                "Tracking the already tracked quest must not schedule a save.");

            AssertSuccess(Invoke(fixture.Engine, "ClearTrackedQuest"));
            object repeatedClear = Invoke(
                fixture.Engine,
                "ClearTrackedQuest");
            Assert.That(
                GetProperty<object>(repeatedClear, "Code").ToString(),
                Is.EqualTo("NoChange"),
                "Clearing an empty tracker must not schedule a save.");
        }

        [Test]
        public void RuntimeAutosave_ClearsPendingOnlyAfterSaveCompletes()
        {
            Type gameDirectorType = RequireType(
                "CryingSnow.StackCraft.GameDirector");
            Type runtimeType = RequireType(
                "CryingSnow.StackCraft.WorldQuestRuntime");

            Assert.That(
                gameDirectorType.GetEvent("OnAfterSave"),
                Is.Not.Null,
                "The quest runtime needs a successful-save lifecycle event.");
            Assert.That(
                runtimeType.GetMethod(
                    "HandleAfterSave",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(
                runtimeType.GetMethod(
                    "HandleBeforeSave",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null,
                "Clearing before disk I/O loses the retry when saving fails.");
        }

        [Test]
        public void SaveSystem_RecoversFromBackupWhenPrimaryJsonIsCorrupt()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type saveSystemType = RequireType(
                "CryingSnow.StackCraft.SaveSystem");
            string fileName = "WorldQuestV2AtomicSaveTest_" +
                Guid.NewGuid().ToString("N");
            string primary = Path.Combine(
                Application.persistentDataPath,
                fileName + ".json");
            string backup = primary + ".bak";
            string temp = primary + ".tmp";
            try
            {
                MethodInfo save = saveSystemType.GetMethod("SaveData")
                    .MakeGenericMethod(gameDataType);
                MethodInfo load = saveSystemType.GetMethod("LoadData")
                    .MakeGenericMethod(gameDataType);
                object first = Activator.CreateInstance(gameDataType);
                gameDataType.GetField("SlotNumber").SetValue(first, 41);
                save.Invoke(null, new[] { first, (object)fileName });
                object second = Activator.CreateInstance(gameDataType);
                gameDataType.GetField("SlotNumber").SetValue(second, 42);
                save.Invoke(null, new[] { second, (object)fileName });

                Assert.That(File.Exists(backup), Is.True);
                File.WriteAllText(primary, "{not-valid-json");
                object restored = load.Invoke(
                    null,
                    new object[] { fileName });
                Assert.That(restored, Is.Not.Null);
                Assert.That(
                    gameDataType.GetField("SlotNumber").GetValue(restored),
                    Is.EqualTo(41));

                MethodInfo loadAll = saveSystemType
                    .GetMethod("LoadAllValidData")
                    .MakeGenericMethod(gameDataType);
                object all = loadAll.Invoke(null, null);
                PropertyInfo item = all.GetType().GetProperty("Item");
                object startupRestored = item.GetValue(
                    all,
                    new object[] { fileName });
                Assert.That(
                    gameDataType.GetField("SlotNumber")
                        .GetValue(startupRestored),
                    Is.EqualTo(41));
            }
            finally
            {
                if (File.Exists(primary))
                    File.Delete(primary);
                if (File.Exists(backup))
                    File.Delete(backup);
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        [Test]
        public void DefinitionValidator_AcceptsMainQuestAndRejectsDuplicateIds()
        {
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.WorldQuestDefinition");
            Type validatorType = RequireType(
                "CryingSnow.StackCraft.WorldQuestDefinitionValidator");
            UnityEngine.Object[] definitions = Resources.LoadAll(
                "WorldQuests",
                definitionType);
            MethodInfo validate = validatorType.GetMethod(
                "Validate",
                new[] { typeof(UnityEngine.Object[]) });
            Assert.That(validate, Is.Not.Null);

            object validReport = validate.Invoke(
                null,
                new object[] { definitions });
            Assert.That(
                GetProperty<IList>(validReport, "Errors").Count,
                Is.EqualTo(0));

            var duplicate = new[] { definitions[0], definitions[0] };
            object invalidReport = validate.Invoke(
                null,
                new object[] { duplicate });
            Assert.That(
                GetProperty<IList>(invalidReport, "Errors").Count,
                Is.GreaterThan(0));
        }

        [Test]
        public void SaveSystem_NewerPrimaryDoesNotFallBackToOlderBackup()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type saveSystemType = RequireType(
                "CryingSnow.StackCraft.SaveSystem");
            string fileName = "WorldQuestV2NewerSaveTest_" +
                Guid.NewGuid().ToString("N");
            string primary = Path.Combine(
                Application.persistentDataPath,
                fileName + ".json");
            string backup = primary + ".bak";
            string temp = primary + ".tmp";
            try
            {
                MethodInfo save = saveSystemType.GetMethod("SaveData")
                    .MakeGenericMethod(gameDataType);
                MethodInfo load = saveSystemType.GetMethod("LoadData")
                    .MakeGenericMethod(gameDataType);
                object oldData = Activator.CreateInstance(gameDataType);
                gameDataType.GetField("SlotNumber").SetValue(oldData, 71);
                save.Invoke(null, new[] { oldData, (object)fileName });
                File.Copy(primary, backup, true);
                File.WriteAllText(
                    primary,
                    "{\"WorldQuestStateVersion\":999,\"SlotNumber\":72}");

                TargetInvocationException exception = Assert.Throws<
                    TargetInvocationException>(() =>
                        load.Invoke(null, new object[] { fileName }));
                Assert.That(exception.InnerException, Is.Not.Null);
                Assert.That(
                    exception.InnerException.GetType().Name,
                    Is.EqualTo("SaveVersionTooNewException"));
            }
            finally
            {
                if (File.Exists(primary))
                    File.Delete(primary);
                if (File.Exists(backup))
                    File.Delete(backup);
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        [Test]
        public void V1Migration_RejectsInvalidObjectiveIndexAndRollsBack()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type stateType = RequireType(
                "CryingSnow.StackCraft.WorldQuestStateData");
            Type statusType = RequireType(
                "CryingSnow.StackCraft.WorldQuestStatus");
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.WorldQuestDefinition");
            Type engineType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEngine");
            object gameData = Activator.CreateInstance(gameDataType);
            gameDataType.GetField("WorldQuestStateVersion")
                .SetValue(gameData, 1);
            object legacyState = Activator.CreateInstance(stateType);
            stateType.GetField("QuestId").SetValue(legacyState, QuestId);
            stateType.GetField("Status").SetValue(
                legacyState,
                Enum.ToObject(statusType, 1));
            stateType.GetField("ObjectiveIndex").SetValue(legacyState, 99);
            ((IList)gameDataType.GetField("WorldQuests")
                    .GetValue(gameData))
                .Add(legacyState);
            object engine = Activator.CreateInstance(
                engineType,
                gameData,
                Resources.LoadAll("WorldQuests", definitionType));

            TargetInvocationException exception = Assert.Throws<
                TargetInvocationException>(() => Invoke(engine, "Initialize"));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidDataException>());
            Assert.That(
                gameDataType.GetField("WorldQuestStateVersion")
                    .GetValue(gameData),
                Is.EqualTo(1));
            Assert.That(
                stateType.GetField("ObjectiveIndex").GetValue(legacyState),
                Is.EqualTo(99));
        }

        [Test]
        public void V2Initialization_RejectsDuplicateQuestStates()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type stateType = RequireType(
                "CryingSnow.StackCraft.WorldQuestStateData");
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.WorldQuestDefinition");
            Type engineType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEngine");
            object gameData = Activator.CreateInstance(gameDataType);
            gameDataType.GetField("WorldQuestStateVersion")
                .SetValue(gameData, 2);
            IList states = (IList)gameDataType.GetField("WorldQuests")
                .GetValue(gameData);
            for (int index = 0; index < 2; index++)
            {
                object state = Activator.CreateInstance(stateType);
                stateType.GetField("QuestId").SetValue(state, QuestId);
                states.Add(state);
            }
            object engine = Activator.CreateInstance(
                engineType,
                gameData,
                Resources.LoadAll("WorldQuests", definitionType));

            TargetInvocationException exception = Assert.Throws<
                TargetInvocationException>(() => Invoke(engine, "Initialize"));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidDataException>());
            Assert.That(states.Count, Is.EqualTo(2));
        }

        [Test]
        public void LoadAllValidData_SkipsFutureVersionWithoutUsingItsBackup()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type saveSystemType = RequireType(
                "CryingSnow.StackCraft.SaveSystem");
            string prefix = "WorldQuestV2FutureListTest_" +
                Guid.NewGuid().ToString("N");
            string compatibleName = prefix + "_compatible";
            string futureName = prefix + "_future";
            string compatiblePath = Path.Combine(
                Application.persistentDataPath,
                compatibleName + ".json");
            string futurePath = Path.Combine(
                Application.persistentDataPath,
                futureName + ".json");
            try
            {
                MethodInfo save = saveSystemType.GetMethod("SaveData")
                    .MakeGenericMethod(gameDataType);
                object compatible = Activator.CreateInstance(gameDataType);
                gameDataType.GetField("SlotNumber").SetValue(compatible, 81);
                save.Invoke(null, new[] { compatible, (object)compatibleName });
                object oldFuture = Activator.CreateInstance(gameDataType);
                gameDataType.GetField("SlotNumber").SetValue(oldFuture, 82);
                save.Invoke(null, new[] { oldFuture, (object)futureName });
                File.Copy(futurePath, futurePath + ".bak", true);
                File.WriteAllText(
                    futurePath,
                    "{\"WorldQuestStateVersion\":999,\"SlotNumber\":83}");

                MethodInfo loadAll = saveSystemType
                    .GetMethod("LoadAllValidData")
                    .MakeGenericMethod(gameDataType);
                object all = loadAll.Invoke(null, null);
                MethodInfo containsKey = all.GetType().GetMethod(
                    "ContainsKey",
                    new[] { typeof(string) });
                Assert.That(
                    containsKey.Invoke(all, new object[] { compatibleName }),
                    Is.True);
                Assert.That(
                    containsKey.Invoke(all, new object[] { futureName }),
                    Is.False);
            }
            finally
            {
                DeleteSaveFiles(compatiblePath);
                DeleteSaveFiles(futurePath);
            }
        }

        [Test]
        public void V1DuplicateCompletedStates_GrantPendingRewardOnlyOnce()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type stateType = RequireType(
                "CryingSnow.StackCraft.WorldQuestStateData");
            Type statusType = RequireType(
                "CryingSnow.StackCraft.WorldQuestStatus");
            Type cardDataType = RequireType(
                "CryingSnow.StackCraft.CardData");
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.WorldQuestDefinition");
            Type engineType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEngine");
            object gameData = Activator.CreateInstance(gameDataType);
            gameDataType.GetField("WorldQuestStateVersion")
                .SetValue(gameData, 1);
            object protagonist = Activator.CreateInstance(cardDataType);
            cardDataType.GetField("PersistentId")
                .SetValue(protagonist, "test-protagonist");
            cardDataType.GetField("Level").SetValue(protagonist, 1);
            ((IList)gameDataType.GetField("PartyMembers")
                    .GetValue(gameData))
                .Add(protagonist);
            gameDataType.GetField("ProtagonistPersistentId")
                .SetValue(gameData, "test-protagonist");
            IList states = (IList)gameDataType.GetField("WorldQuests")
                .GetValue(gameData);
            for (int index = 0; index < 2; index++)
            {
                object state = Activator.CreateInstance(stateType);
                stateType.GetField("QuestId").SetValue(state, QuestId);
                stateType.GetField("Status").SetValue(
                    state,
                    Enum.ToObject(statusType, 3));
                stateType.GetField("ObjectiveIndex").SetValue(state, 3);
                stateType.GetField("CurrentAmount").SetValue(state, 1);
                stateType.GetField("AcceptanceRewardClaimed")
                    .SetValue(state, true);
                stateType.GetField("CompletionRewardClaimed")
                    .SetValue(state, false);
                states.Add(state);
            }
            object engine = Activator.CreateInstance(
                engineType,
                gameData,
                Resources.LoadAll("WorldQuests", definitionType));

            Invoke(engine, "Initialize");

            Assert.That(
                ((IList)gameDataType.GetField("WorldQuests")
                    .GetValue(gameData)).Count,
                Is.EqualTo(1));
            Assert.That(
                BackpackCount(gameDataType, gameData),
                Is.EqualTo(10));
            Assert.That(
                cardDataType.GetField("Experience").GetValue(protagonist),
                Is.EqualTo(10));
        }

        [Test]
        public void SaveSystem_UsesBackupWhenV2PrimaryViolatesQuestInvariants()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type saveSystemType = RequireType(
                "CryingSnow.StackCraft.SaveSystem");
            string fileName = "WorldQuestV2InvariantBackupTest_" +
                Guid.NewGuid().ToString("N");
            string primary = Path.Combine(
                Application.persistentDataPath,
                fileName + ".json");
            try
            {
                MethodInfo save = saveSystemType.GetMethod("SaveData")
                    .MakeGenericMethod(gameDataType);
                MethodInfo load = saveSystemType.GetMethod("LoadData")
                    .MakeGenericMethod(gameDataType);
                object backupData = Activator.CreateInstance(gameDataType);
                gameDataType.GetField("SlotNumber").SetValue(backupData, 91);
                save.Invoke(null, new[] { backupData, (object)fileName });
                File.Copy(primary, primary + ".bak", true);
                File.WriteAllText(
                    primary,
                    "{\"WorldQuestStateVersion\":2," +
                    "\"SlotNumber\":92,\"WorldQuests\":[" +
                    "{\"QuestId\":\"" + QuestId + "\",\"Status\":0}," +
                    "{\"QuestId\":\"" + QuestId + "\",\"Status\":0}]}");

                object restored = load.Invoke(null, new object[] { fileName });
                Assert.That(
                    gameDataType.GetField("SlotNumber").GetValue(restored),
                    Is.EqualTo(91));

                File.Delete(primary + ".bak");
                object repairedPrimary = load.Invoke(
                    null,
                    new object[] { fileName });
                Assert.That(
                    gameDataType.GetField("SlotNumber")
                        .GetValue(repairedPrimary),
                    Is.EqualTo(91));
            }
            finally
            {
                DeleteSaveFiles(primary);
            }
        }

        private static void DeleteSaveFiles(string primaryPath)
        {
            foreach (string suffix in new[] { string.Empty, ".bak", ".tmp" })
            {
                string path = primaryPath + suffix;
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static EngineFixture CreateMainQuestEngine()
        {
            Type gameDataType = RequireType(
                "CryingSnow.StackCraft.GameData");
            Type definitionType = RequireType(
                "CryingSnow.StackCraft.WorldQuestDefinition");
            Type engineType = RequireType(
                "CryingSnow.StackCraft.WorldQuestEngine");
            object gameData = Activator.CreateInstance(gameDataType);
            gameDataType.GetField("ActiveLocationId")
                .SetValue(gameData, "riverbend");
            UnityEngine.Object[] definitions = Resources.LoadAll(
                "WorldQuests",
                definitionType);
            object engine = Activator.CreateInstance(
                engineType,
                gameData,
                definitions);
            Invoke(engine, "Initialize");
            return new EngineFixture(
                gameDataType,
                gameData,
                engine,
                RequireType("CryingSnow.StackCraft.WorldQuestEvent"),
                RequireType("CryingSnow.StackCraft.WorldQuestEventType"),
                RequireType(
                    "CryingSnow.StackCraft.WorldQuestTradeDirection"));
        }

        private static void AdvanceMainQuestToTurnIn(EngineFixture fixture)
        {
            AssertSuccess(Invoke(fixture.Engine, "TryAccept", QuestId));
            object[] events =
            {
                CreateEvent(
                    fixture.EventType,
                    fixture.EventKindType,
                    fixture.DirectionType,
                    "purchase-rollback",
                    "MarketTradeCommitted",
                    "riverbend",
                    "food",
                    "riverbend-market",
                    1,
                    "PlayerBuys",
                    true,
                    true),
                CreateEvent(
                    fixture.EventType,
                    fixture.EventKindType,
                    fixture.DirectionType,
                    "enter-rollback",
                    "LocationEntered",
                    "whispering-forest",
                    "whispering-forest",
                    string.Empty,
                    1,
                    "None",
                    true,
                    true),
                CreateEvent(
                    fixture.EventType,
                    fixture.EventKindType,
                    fixture.DirectionType,
                    "defeat-rollback",
                    "CardDefeated",
                    "whispering-forest",
                    "366be2e0e40c4b4d93229a94ae7133ae",
                    string.Empty,
                    1,
                    "None",
                    true,
                    true),
                CreateEvent(
                    fixture.EventType,
                    fixture.EventKindType,
                    fixture.DirectionType,
                    "talk-rollback",
                    "NpcTalked",
                    "riverbend",
                    "riverbend-village-chief",
                    string.Empty,
                    1,
                    "None",
                    true,
                    true)
            };
            foreach (object questEvent in events)
                AssertSuccess(Invoke(fixture.Engine, "ReportEvent", questEvent));
        }

        private static UnityEngine.Object CreateQuestDefinition(
            Type definitionType,
            string id,
            object stages,
            object acceptEffect)
        {
            UnityEngine.Object definition = ScriptableObject.CreateInstance(
                definitionType);
            SetField(definition, "id", id);
            SetField(definition, "title", id);
            SetField(definition, "entryStageId", "prepare_supplies");
            SetField(definition, "acceptLocationId", string.Empty);
            SetField(definition, "stages", stages);
            if (acceptEffect != null)
            {
                FieldInfo effectsField = definitionType.GetField(
                    "onAcceptEffects",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                IList effects = (IList)Activator.CreateInstance(
                    effectsField.FieldType);
                effects.Add(acceptEffect);
                effectsField.SetValue(definition, effects);
            }
            return definition;
        }

        private static object CreateEffect(
            Type effectType,
            Type effectKindType,
            string effectId,
            string kind,
            string targetId,
            string reasonId)
        {
            object effect = Activator.CreateInstance(effectType);
            SetField(effect, "effectId", effectId);
            SetField(effect, "type", Enum.Parse(effectKindType, kind));
            SetField(effect, "targetId", targetId);
            SetField(effect, "reasonId", reasonId);
            return effect;
        }

        private static int BackpackCount(Type gameDataType, object gameData)
        {
            object backpack = gameDataType.GetField("Backpack")
                .GetValue(gameData);
            return ((IList)backpack.GetType().GetField("Entries")
                .GetValue(backpack)).Count;
        }

        private static void AssertQuestState(
            Type gameDataType,
            object gameData,
            string questId,
            string expectedStage,
            string expectedStatus)
        {
            IList states = (IList)gameDataType.GetField("WorldQuests")
                .GetValue(gameData);
            object state = states.Cast<object>().Single(candidate =>
                (string)candidate.GetType().GetField("QuestId")
                    .GetValue(candidate) == questId);
            Assert.That(
                state.GetType().GetField("ActiveStageId").GetValue(state),
                Is.EqualTo(expectedStage));
            Assert.That(
                state.GetType().GetField("Status").GetValue(state).ToString(),
                Is.EqualTo(expectedStatus));
        }

        private static object GetField(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(instance);
        }

        private static void SetField(
            object instance,
            string fieldName,
            object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            field.SetValue(instance, value);
        }

        private static object CreateEvent(
            Type eventType,
            Type eventKindType,
            Type directionType,
            string eventId,
            string eventKind,
            string locationId,
            string primaryTargetId,
            string contextId,
            int amount,
            string direction,
            bool credited,
            bool protagonist)
        {
            return Activator.CreateInstance(
                eventType,
                eventId,
                Enum.Parse(eventKindType, eventKind),
                0L,
                locationId,
                "hero-id",
                credited,
                protagonist,
                primaryTargetId,
                string.Empty,
                contextId,
                amount,
                Enum.Parse(directionType, direction));
        }

        private static void AssertStage(
            Type gameDataType,
            object gameData,
            string expectedStage,
            string expectedStatus)
        {
            IList states = (IList)gameDataType.GetField("WorldQuests")
                .GetValue(gameData);
            object state = states.Cast<object>().Single(candidate =>
                (string)candidate.GetType().GetField("QuestId")
                    .GetValue(candidate) == QuestId);
            Assert.That(
                state.GetType().GetField("ActiveStageId").GetValue(state),
                Is.EqualTo(expectedStage));
            Assert.That(
                state.GetType().GetField("Status").GetValue(state).ToString(),
                Is.EqualTo(expectedStatus));
        }

        private static void AssertSuccess(object result)
        {
            Assert.That(
                GetProperty<bool>(result, "Success"),
                Is.True,
                GetProperty<string>(result, "Message"));
        }

        private static object Invoke(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate =>
                    candidate.Name == methodName &&
                    candidate.GetParameters().Length == arguments.Length);
            return method.Invoke(instance, arguments);
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(
                property,
                Is.Not.Null,
                $"Missing property {instance.GetType().FullName}.{propertyName}");
            return (T)property.GetValue(instance);
        }

        private static Type RequireType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Missing type {fullName}.");
            return type;
        }

        private static Type FindTypeOrNull(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
        }

        private sealed class EngineFixture
        {
            public Type GameDataType { get; }
            public object GameData { get; }
            public object Engine { get; }
            public Type EventType { get; }
            public Type EventKindType { get; }
            public Type DirectionType { get; }

            public EngineFixture(
                Type gameDataType,
                object gameData,
                object engine,
                Type eventType,
                Type eventKindType,
                Type directionType)
            {
                GameDataType = gameDataType;
                GameData = gameData;
                Engine = engine;
                EventType = eventType;
                EventKindType = eventKindType;
                DirectionType = directionType;
            }
        }
    }
}
