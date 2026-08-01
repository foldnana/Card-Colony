using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CardColony.Tests
{
    public sealed class WorldQuestProgressionUnityTests
    {
        private const string QuestId =
            "story_riverbend_whispering_forest_01";
        private const string VillageChiefId =
            "riverbend-village-chief";
        private const string MarketId = "riverbend-market";
        private const string CommodityId = "food";
        private const string ForestId = "whispering-forest";
        private const string SlimeId =
            "366be2e0e40c4b4d93229a94ae7133ae";

        [Test]
        public void EnsureQuestState_CreatesAvailableStateAndDeduplicatesMigration()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type stateType = FindType(
                "CryingSnow.StackCraft.WorldQuestStateData");
            Type serviceType = FindType(
                "CryingSnow.StackCraft.WorldQuestProgressionService");
            object gameData = Activator.CreateInstance(gameDataType);
            gameDataType.GetField("ActiveLocationId")
                .SetValue(gameData, "riverbend");
            IList states = GetQuestStates(gameDataType, gameData);
            object duplicate = Activator.CreateInstance(stateType);
            stateType.GetField("QuestId").SetValue(duplicate, QuestId);
            states.Add(duplicate);
            object secondDuplicate = Activator.CreateInstance(stateType);
            stateType.GetField("QuestId").SetValue(
                secondDuplicate,
                QuestId);
            states.Add(secondDuplicate);

            object state = RequireMethod(
                    serviceType,
                    "EnsureQuestState",
                    gameDataType,
                    typeof(string))
                .Invoke(null, new[] { gameData, (object)QuestId });

            states = GetQuestStates(gameDataType, gameData);
            Assert.That(
                states.Cast<object>().Count(value =>
                    (string)stateType.GetField("QuestId").GetValue(value) ==
                    QuestId),
                Is.EqualTo(1));
            Assert.That(
                stateType.GetField("Status").GetValue(state).ToString(),
                Is.EqualTo("Available"));
            Assert.That(
                stateType.GetField("ObjectiveIndex").GetValue(state),
                Is.EqualTo(0));
            Assert.That(
                gameDataType.GetField("WorldQuestStateVersion")
                    .GetValue(gameData),
                Is.EqualTo(2));
        }

        [Test]
        public void QuestProgression_RequiresOrderedMatchingWorldActions()
        {
            QuestFixture fixture = CreateFixture();

            Assert.That(fixture.TryAccept(), Is.True);
            Assert.That(fixture.TryAccept(), Is.False);
            Assert.That(
                fixture.ReportPurchase(
                    "other-market",
                    CommodityId,
                    true,
                    1),
                Is.False);
            Assert.That(
                fixture.ReportPurchase(
                    MarketId,
                    "materials",
                    true,
                    1),
                Is.False);
            Assert.That(
                fixture.ReportPurchase(
                    MarketId,
                    CommodityId,
                    false,
                    1),
                Is.False);
            Assert.That(
                fixture.ReportLocation(ForestId),
                Is.False,
                "Entering the forest cannot skip the purchase objective.");
            Assert.That(
                fixture.ReportPurchase(
                    MarketId,
                    CommodityId,
                    true,
                    1),
                Is.True);
            Assert.That(fixture.ObjectiveIndex, Is.EqualTo(1));

            Assert.That(fixture.ReportLocation("riverbend"), Is.False);
            Assert.That(fixture.ReportLocation(ForestId), Is.True);
            Assert.That(fixture.ObjectiveIndex, Is.EqualTo(2));

            Assert.That(
                fixture.ReportDefeat("other-enemy", true),
                Is.False);
            Assert.That(
                fixture.ReportDefeat(SlimeId, false),
                Is.False,
                "NPC-only kills must not progress the main quest.");
            Assert.That(fixture.ReportDefeat(SlimeId, true), Is.True);
            Assert.That(fixture.Status, Is.EqualTo("Active"));
            Assert.That(fixture.ObjectiveIndex, Is.EqualTo(3));
            Assert.That(fixture.ReportTalk(VillageChiefId), Is.True);
            Assert.That(fixture.Status, Is.EqualTo("ReadyToTurnIn"));
            Assert.That(fixture.ObjectiveIndex, Is.EqualTo(3));
            Assert.That(
                fixture.ReportDefeat(SlimeId, true),
                Is.False,
                "Extra slime kills must not progress the quest twice.");
        }

        [Test]
        public void QuestProgression_IgnoresActionsBeforeAcceptance()
        {
            QuestFixture fixture = CreateFixture();

            Assert.That(
                fixture.ReportPurchase(
                    MarketId,
                    CommodityId,
                    true,
                    10),
                Is.False);
            Assert.That(fixture.ReportLocation(ForestId), Is.False);
            Assert.That(fixture.ReportDefeat(SlimeId, true), Is.False);
            Assert.That(fixture.Status, Is.EqualTo("Available"));
            Assert.That(fixture.ObjectiveIndex, Is.EqualTo(0));
        }

        [Test]
        public void QuestCompletion_RequiresVillageChiefAndIsIdempotent()
        {
            QuestFixture fixture = CreateFixture();
            fixture.TryAccept();
            fixture.ReportPurchase(MarketId, CommodityId, true, 1);
            fixture.ReportLocation(ForestId);
            fixture.ReportDefeat(SlimeId, true);
            fixture.ReportTalk(VillageChiefId);

            Assert.That(fixture.CanTurnIn("other-npc"), Is.False);
            Assert.That(fixture.TryComplete("other-npc"), Is.False);
            Assert.That(fixture.CanTurnIn(VillageChiefId), Is.True);
            Assert.That(fixture.TryComplete(VillageChiefId), Is.True);
            Assert.That(fixture.Status, Is.EqualTo("Completed"));
            Assert.That(fixture.CanTurnIn(VillageChiefId), Is.False);
            Assert.That(fixture.TryComplete(VillageChiefId), Is.False);
        }

        [Test]
        public void RewardService_GrantsAcceptanceBundleExactlyOnce()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type rewardServiceType = FindType(
                "CryingSnow.StackCraft.WorldQuestRewardService");
            object gameData = Activator.CreateInstance(gameDataType);
            gameDataType.GetField("ActiveLocationId")
                .SetValue(gameData, "riverbend");

            Assert.That(
                InvokeStaticBool(
                    rewardServiceType,
                    "TryAcceptAndGrant",
                    new[] { gameData, (object)QuestId },
                    gameDataType,
                    typeof(string)),
                Is.True);
            Assert.That(CountBackpackCards(gameDataType, gameData,
                "4bda315463bf4b73b63f1d232fb522e4"), Is.EqualTo(8));
            Assert.That(
                CountBackpackCards(
                    gameDataType,
                    gameData,
                    "medicine"),
                Is.EqualTo(1));
            Assert.That(
                InvokeStaticBool(
                    rewardServiceType,
                    "TryAcceptAndGrant",
                    new[] { gameData, (object)QuestId },
                    gameDataType,
                    typeof(string)),
                Is.False);
            Assert.That(CountBackpackCards(gameDataType, gameData,
                "4bda315463bf4b73b63f1d232fb522e4"), Is.EqualTo(8));
            Assert.That(
                StateField(gameDataType, gameData, "AcceptanceRewardClaimed"),
                Is.True);
        }

        [Test]
        public void RewardService_CompletesWithCoinsAndExperienceExactlyOnce()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            Type rewardServiceType = FindType(
                "CryingSnow.StackCraft.WorldQuestRewardService");
            object gameData = Activator.CreateInstance(gameDataType);
            object protagonist = Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(protagonist, "traveler");
            cardDataType.GetField("PersistentId")
                .SetValue(protagonist, "hero-id");
            cardDataType.GetField("Level").SetValue(protagonist, 1);
            cardDataType.GetField("Experience").SetValue(protagonist, 5);
            cardDataType.GetField("CurrentHealth").SetValue(protagonist, 15);
            cardDataType.GetField("MaximumHealth").SetValue(protagonist, 15);
            gameDataType.GetField("ProtagonistPersistentId")
                .SetValue(gameData, "hero-id");
            ((IList)gameDataType.GetField("PartyMembers").GetValue(gameData))
                .Add(protagonist);
            QuestFixture fixture = new(
                gameDataType,
                FindType(
                    "CryingSnow.StackCraft.WorldQuestProgressionService"),
                gameData);
            fixture.TryAccept();
            fixture.ReportPurchase(MarketId, CommodityId, true, 1);
            fixture.ReportLocation(ForestId);
            fixture.ReportDefeat(SlimeId, true);
            fixture.ReportTalk(VillageChiefId);

            Assert.That(
                InvokeStaticBool(
                    rewardServiceType,
                    "TryCompleteAndGrant",
                    new[] { gameData, (object)QuestId, VillageChiefId },
                    gameDataType,
                    typeof(string),
                    typeof(string)),
                Is.True);
            Assert.That(CountBackpackCards(gameDataType, gameData,
                "4bda315463bf4b73b63f1d232fb522e4"), Is.EqualTo(18));
            Assert.That(
                cardDataType.GetField("Experience").GetValue(protagonist),
                Is.EqualTo(0));
            Assert.That(
                cardDataType.GetField("Level").GetValue(protagonist),
                Is.EqualTo(2));
            Assert.That(
                cardDataType.GetField("MaximumHealth")
                    .GetValue(protagonist),
                Is.EqualTo(17));
            Assert.That(fixture.Status, Is.EqualTo("Completed"));
            Assert.That(
                InvokeStaticBool(
                    rewardServiceType,
                    "TryCompleteAndGrant",
                    new[] { gameData, (object)QuestId, VillageChiefId },
                    gameDataType,
                    typeof(string),
                    typeof(string)),
                Is.False);
            Assert.That(CountBackpackCards(gameDataType, gameData,
                "4bda315463bf4b73b63f1d232fb522e4"), Is.EqualTo(18));
            Assert.That(
                cardDataType.GetField("Experience").GetValue(protagonist),
                Is.EqualTo(0));
            Assert.That(
                StateField(gameDataType, gameData, "CompletionRewardClaimed"),
                Is.True);
        }

        [Test]
        public void WorldQuestDefinition_ContainsTheFixedMainQuestContract()
        {
            Type definitionType = FindType(
                "CryingSnow.StackCraft.WorldQuestDefinition");
            UnityEngine.Object[] definitions = Resources.LoadAll(
                "WorldQuests",
                definitionType);
            object definition = definitions.Single(value =>
                (string)definitionType.GetProperty("Id").GetValue(value) ==
                QuestId);

            Assert.That(
                definitionType.GetProperty("Title").GetValue(definition),
                Is.EqualTo("林间异响"));
            Assert.That(
                definitionType.GetProperty("GiverNpcId")
                    .GetValue(definition),
                Is.EqualTo(VillageChiefId));
            IList objectives = (IList)definitionType
                .GetProperty("Objectives")
                .GetValue(definition);
            Assert.That(objectives.Count, Is.EqualTo(4));
            Assert.That(
                objectiveText(objectives[0]),
                Is.EqualTo("在河湾市场购买1份粮食"));
            Assert.That(
                objectiveText(objectives[1]),
                Is.EqualTo("前往低语森林"));
            Assert.That(
                objectiveText(objectives[2]),
                Is.EqualTo("击败1只史莱姆"));
            Assert.That(
                objectiveText(objectives[3]),
                Is.EqualTo("返回河湾村向村长汇报"));

            string objectiveText(object objective)
            {
                return (string)objective.GetType()
                    .GetProperty("Text")
                    .GetValue(objective);
            }
        }

        [Test]
        public void WorldQuestState_SurvivesJsonRoundTrip()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type serviceType = FindType(
                "CryingSnow.StackCraft.WorldQuestProgressionService");
            object gameData = Activator.CreateInstance(gameDataType);
            gameDataType.GetField("ActiveLocationId")
                .SetValue(gameData, "riverbend");
            InvokeStaticBool(
                serviceType,
                "TryAccept",
                new[] { gameData, (object)QuestId },
                gameDataType,
                typeof(string));
            object state = GetQuestStates(gameDataType, gameData)[0];
            state.GetType().GetField("AcceptanceRewardClaimed")
                .SetValue(state, true);

            string json = JsonUtility.ToJson(gameData);
            object restored = JsonUtility.FromJson(json, gameDataType);

            Assert.That(
                gameDataType.GetField("WorldQuestStateVersion")
                    .GetValue(restored),
                Is.EqualTo(2));
            Assert.That(
                GetQuestStates(gameDataType, restored).Count,
                Is.EqualTo(1));
            Assert.That(
                StateField(
                    gameDataType,
                    restored,
                    "AcceptanceRewardClaimed"),
                Is.True);
            Assert.That(
                StateField(gameDataType, restored, "Status").ToString(),
                Is.EqualTo("Active"));
        }

        [Test]
        public void MainQuestDependencies_AreConfiguredForTheFirstDayFlow()
        {
            Type cardDefinitionType = FindType(
                "CryingSnow.StackCraft.CardDefinition");
            Type locationDefinitionType = FindType(
                "CryingSnow.StackCraft.LocationDefinition");
            UnityEngine.Object[] cards = Resources.LoadAll(
                "Cards",
                cardDefinitionType);
            UnityEngine.Object[] locations = Resources.LoadAll(
                "Locations",
                locationDefinitionType);

            Assert.That(
                cards.Any(card =>
                    PropertyString(card, "Id") == SlimeId),
                Is.True);
            Assert.That(
                cards.Any(card =>
                    PropertyString(card, "Id") ==
                    RiverbendForestQuestRulesCoinId),
                Is.True);
            Assert.That(
                cards.Any(card =>
                    PropertyString(card, "Id") == "medicine"),
                Is.True);

            object forest = locations.Single(location =>
                PropertyString(location, "Id") == ForestId);
            IList randomSpawns = (IList)forest.GetType()
                .GetProperty("RandomCardSpawns")
                .GetValue(forest);
            object slimeSpawn = randomSpawns.Cast<object>().Single(spawn =>
            {
                object definition = spawn.GetType()
                    .GetProperty("Definition")
                    .GetValue(spawn);
                return definition != null &&
                       PropertyString(definition, "Id") == SlimeId;
            });
            Assert.That(
                slimeSpawn.GetType().GetProperty("MinimumCount")
                    .GetValue(slimeSpawn),
                Is.EqualTo(1));

            object marketLocation = locations.Single(location =>
                PropertyString(location, "Id") == MarketId);
            object marketProfile = marketLocation.GetType()
                .GetProperty("PublicMarketProfile")
                .GetValue(marketLocation);
            Assert.That(marketProfile, Is.Not.Null);
            Assert.That(
                PropertyString(marketProfile, "Id"),
                Is.EqualTo(MarketId));
            object foodRule = RequireMethod(
                    marketProfile.GetType(),
                    "GetRule",
                    typeof(string))
                .Invoke(marketProfile, new object[] { CommodityId });
            Assert.That(foodRule, Is.Not.Null);
            Assert.That(
                foodRule.GetType().GetProperty("AllowsPlayerPurchase")
                    .GetValue(foodRule),
                Is.True);
            Assert.That(
                (int)foodRule.GetType().GetProperty("InitialStockMin")
                    .GetValue(foodRule),
                Is.GreaterThanOrEqualTo(1));
            object commodity = foodRule.GetType().GetProperty("Commodity")
                .GetValue(foodRule);
            Assert.That(
                (int)commodity.GetType().GetProperty("BasePrice")
                    .GetValue(commodity),
                Is.LessThanOrEqualTo(8));
        }

        private static QuestFixture CreateFixture()
        {
            return new QuestFixture(
                FindType("CryingSnow.StackCraft.GameData"),
                FindType(
                    "CryingSnow.StackCraft.WorldQuestProgressionService"));
        }

        private static IList GetQuestStates(
            Type gameDataType,
            object gameData)
        {
            return (IList)gameDataType.GetField("WorldQuests")
                .GetValue(gameData);
        }

        private const string RiverbendForestQuestRulesCoinId =
            "4bda315463bf4b73b63f1d232fb522e4";

        private static string PropertyString(
            object instance,
            string propertyName)
        {
            return (string)instance.GetType().GetProperty(propertyName)
                .GetValue(instance);
        }

        private static object StateField(
            Type gameDataType,
            object gameData,
            string fieldName)
        {
            Type stateType = FindType(
                "CryingSnow.StackCraft.WorldQuestStateData");
            object state = GetQuestStates(gameDataType, gameData)
                .Cast<object>()
                .Single(value =>
                    (string)stateType.GetField("QuestId").GetValue(value) ==
                    QuestId);
            return stateType.GetField(fieldName).GetValue(state);
        }

        private static int CountBackpackCards(
            Type gameDataType,
            object gameData,
            string definitionId)
        {
            object backpack = gameDataType.GetField("Backpack")
                .GetValue(gameData);
            Type backpackType = backpack.GetType();
            IList entries = (IList)backpackType.GetField("Entries")
                .GetValue(backpack);
            return entries.Cast<object>().Count(entry =>
            {
                object card = entry.GetType().GetField("Card").GetValue(entry);
                return card != null &&
                       (string)card.GetType().GetField("Id").GetValue(card) ==
                       definitionId;
            });
        }

        private static bool InvokeStaticBool(
            Type type,
            string methodName,
            object[] arguments,
            params Type[] parameterTypes)
        {
            return (bool)RequireMethod(type, methodName, parameterTypes)
                .Invoke(null, arguments);
        }

        private static MethodInfo RequireMethod(
            Type type,
            string name,
            params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance,
                null,
                parameterTypes,
                null);
            Assert.That(
                method,
                Is.Not.Null,
                $"Missing method {type.FullName}.{name}.");
            return method;
        }

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Missing type {fullName}");
            return type;
        }

        private sealed class QuestFixture
        {
            private readonly Type gameDataType;
            private readonly Type serviceType;
            private readonly Type stateType;
            private readonly object gameData;

            public QuestFixture(Type gameDataType, Type serviceType)
                : this(
                    gameDataType,
                    serviceType,
                    Activator.CreateInstance(gameDataType))
            {
            }

            public QuestFixture(
                Type gameDataType,
                Type serviceType,
                object gameData)
            {
                this.gameDataType = gameDataType;
                this.serviceType = serviceType;
                stateType = FindType(
                    "CryingSnow.StackCraft.WorldQuestStateData");
                this.gameData = gameData;
                gameDataType.GetField("ActiveLocationId")
                    .SetValue(gameData, "riverbend");
                EnsureTestProtagonist(gameDataType, gameData);
                RequireMethod(
                        serviceType,
                        "EnsureQuestState",
                        gameDataType,
                        typeof(string))
                    .Invoke(null, new[] { gameData, (object)QuestId });
            }

            private static void EnsureTestProtagonist(
                Type gameDataType,
                object gameData)
            {
                IList party = (IList)gameDataType.GetField("PartyMembers")
                    .GetValue(gameData);
                if (party.Count > 0)
                    return;
                Type cardDataType = FindType(
                    "CryingSnow.StackCraft.CardData");
                object protagonist = Activator.CreateInstance(cardDataType);
                cardDataType.GetField("Id").SetValue(
                    protagonist,
                    "traveler");
                cardDataType.GetField("PersistentId").SetValue(
                    protagonist,
                    "test-protagonist");
                cardDataType.GetField("Level").SetValue(protagonist, 1);
                cardDataType.GetField("CurrentHealth").SetValue(
                    protagonist,
                    15);
                cardDataType.GetField("MaximumHealth").SetValue(
                    protagonist,
                    15);
                gameDataType.GetField("ProtagonistPersistentId")
                    .SetValue(gameData, "test-protagonist");
                party.Add(protagonist);
            }

            public int ObjectiveIndex =>
                (int)stateType.GetField("ObjectiveIndex").GetValue(State);

            public string Status =>
                stateType.GetField("Status").GetValue(State).ToString();

            private object State => GetQuestStates(gameDataType, gameData)
                .Cast<object>()
                .Single(value =>
                    (string)stateType.GetField("QuestId").GetValue(value) ==
                    QuestId);

            public bool TryAccept()
            {
                return InvokeBool(
                    "TryAccept",
                    new[] { gameData, (object)QuestId },
                    gameDataType,
                    typeof(string));
            }

            public bool ReportPurchase(
                string marketId,
                string commodityId,
                bool playerBuys,
                int quantity)
            {
                return InvokeBool(
                    "ReportMarketPurchase",
                    new object[]
                    {
                        gameData,
                        QuestId,
                        marketId,
                        commodityId,
                        playerBuys,
                        quantity
                    },
                    gameDataType,
                    typeof(string),
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(int));
            }

            public bool ReportLocation(string locationId)
            {
                return InvokeBool(
                    "ReportLocationEntered",
                    new[] { gameData, (object)QuestId, locationId },
                    gameDataType,
                    typeof(string),
                    typeof(string));
            }

            public bool ReportDefeat(
                string cardDefinitionId,
                bool creditedToPlayerParty)
            {
                return InvokeBool(
                    "ReportEnemyDefeated",
                    new object[]
                    {
                        gameData,
                        QuestId,
                        cardDefinitionId,
                        creditedToPlayerParty
                    },
                    gameDataType,
                    typeof(string),
                    typeof(string),
                    typeof(bool));
            }

            public bool ReportTalk(string npcId)
            {
                return InvokeBool(
                    "ReportNpcTalked",
                    new[] { gameData, (object)QuestId, npcId },
                    gameDataType,
                    typeof(string),
                    typeof(string));
            }

            public bool CanTurnIn(string npcId)
            {
                return InvokeBool(
                    "CanTurnIn",
                    new[] { gameData, (object)QuestId, npcId },
                    gameDataType,
                    typeof(string),
                    typeof(string));
            }

            public bool TryComplete(string npcId)
            {
                return InvokeBool(
                    "TryComplete",
                    new[] { gameData, (object)QuestId, npcId },
                    gameDataType,
                    typeof(string),
                    typeof(string));
            }

            private bool InvokeBool(
                string name,
                object[] arguments,
                params Type[] parameterTypes)
            {
                return (bool)RequireMethod(
                        serviceType,
                        name,
                        parameterTypes)
                    .Invoke(null, arguments);
            }
        }
    }
}
