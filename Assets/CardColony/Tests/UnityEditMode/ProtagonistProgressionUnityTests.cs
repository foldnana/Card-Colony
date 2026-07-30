using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CardColony.Tests
{
    public sealed class ProtagonistProgressionUnityTests
    {
        [Test]
        public void GameData_EnsureProtagonist_AssignsStableIdentityAndProgressionDefaults()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object gameData = Activator.CreateInstance(gameDataType);
            object member = Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(member, "traveler-definition");
            cardDataType.GetField("CurrentHealth").SetValue(member, 12);
            GetPartyMembers(gameDataType, gameData).Add(member);

            MethodInfo ensure = RequireMethod(
                gameDataType,
                "EnsureProtagonist",
                typeof(string),
                typeof(int),
                typeof(int));
            object first = ensure.Invoke(
                gameData,
                new object[] { "traveler-definition", 15, 4 });
            string firstId = (string)cardDataType
                .GetField("PersistentId")
                .GetValue(first);
            object second = ensure.Invoke(
                gameData,
                new object[] { "traveler-definition", 15, 4 });

            Assert.That(firstId, Is.Not.Null.And.Not.Empty);
            Assert.That(
                gameDataType.GetField("ProtagonistPersistentId").GetValue(gameData),
                Is.EqualTo(firstId));
            Assert.That(
                cardDataType.GetField("PersistentId").GetValue(second),
                Is.EqualTo(firstId));
            Assert.That(cardDataType.GetField("Level").GetValue(second), Is.EqualTo(1));
            Assert.That(cardDataType.GetField("Experience").GetValue(second), Is.EqualTo(0));
            Assert.That(cardDataType.GetField("CurrentEnergy").GetValue(second), Is.EqualTo(4));
            Assert.That(cardDataType.GetField("MaxEnergy").GetValue(second), Is.EqualTo(4));
            Assert.That(cardDataType.GetField("IsDowned").GetValue(second), Is.False);
        }

        [Test]
        public void GameData_EnsureProtagonist_PreservesZeroHealthAsDownedDuringMigration()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object gameData = Activator.CreateInstance(gameDataType);
            object member = Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(member, "traveler-definition");
            cardDataType.GetField("CurrentHealth").SetValue(member, 0);
            GetPartyMembers(gameDataType, gameData).Add(member);

            object migrated = RequireMethod(
                    gameDataType,
                    "EnsureProtagonist",
                    typeof(string),
                    typeof(int),
                    typeof(int))
                .Invoke(
                    gameData,
                    new object[] { "traveler-definition", 15, 4 });

            Assert.That(
                cardDataType.GetField("CurrentHealth").GetValue(migrated),
                Is.EqualTo(0));
            Assert.That(
                cardDataType.GetField("IsDowned").GetValue(migrated),
                Is.True);
        }

        [Test]
        public void GameData_GetProtagonist_UsesPersistentIdInsteadOfPartyOrder()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object gameData = Activator.CreateInstance(gameDataType);
            object companion = CreateCardData(cardDataType, "companion", "companion-id");
            object protagonist = CreateCardData(cardDataType, "traveler", "hero-id");
            IList members = GetPartyMembers(gameDataType, gameData);
            members.Add(companion);
            members.Add(protagonist);
            gameDataType.GetField("ProtagonistPersistentId")
                .SetValue(gameData, "hero-id");

            object result = RequireMethod(gameDataType, "GetProtagonistData")
                .Invoke(gameData, null);

            Assert.That(result, Is.SameAs(protagonist));
        }

        [Test]
        public void CharacterProgressionService_GrantExperience_HandlesMultipleLevels()
        {
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            Type serviceType = FindType(
                "CryingSnow.StackCraft.CharacterProgressionService");
            object protagonist = Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Level").SetValue(protagonist, 1);
            cardDataType.GetField("MaximumHealth").SetValue(protagonist, 15);

            Assert.That(
                RequireMethod(
                    serviceType,
                    "GetExperienceRequiredForNextLevel",
                    typeof(int)).Invoke(null, new object[] { 1 }),
                Is.EqualTo(15));

            object result = RequireMethod(
                    serviceType,
                    "GrantExperience",
                    cardDataType,
                    typeof(int))
                .Invoke(null, new[] { protagonist, (object)40 });

            Assert.That(cardDataType.GetField("Level").GetValue(protagonist), Is.EqualTo(3));
            Assert.That(cardDataType.GetField("Experience").GetValue(protagonist), Is.EqualTo(0));
            Assert.That(
                cardDataType.GetField("MaximumHealth").GetValue(protagonist),
                Is.EqualTo(19));
            Assert.That(
                result.GetType().GetProperty("LevelsGained").GetValue(result),
                Is.EqualTo(2));
            Assert.That(
                RequireMethod(serviceType, "GetMaxHealthBonus", typeof(int))
                    .Invoke(null, new object[] { 3 }),
                Is.EqualTo(4));
            Assert.That(
                RequireMethod(serviceType, "GetAttackBonus", typeof(int))
                    .Invoke(null, new object[] { 3 }),
                Is.EqualTo(1));
            Assert.That(
                RequireMethod(serviceType, "GetAttackBonus", typeof(int))
                    .Invoke(null, new object[] { 2 }),
                Is.EqualTo(1));
            Assert.That(
                RequireMethod(serviceType, "GetDefenseBonus", typeof(int))
                    .Invoke(null, new object[] { 3 }),
                Is.EqualTo(1));
        }

        [Test]
        public void LocationRefreshPolicy_RefreshesOnlyOnFirstEntryOrNewWorldDay()
        {
            Type policyType = FindType(
                "CryingSnow.StackCraft.LocationRandomRefreshPolicy");
            Type reasonType = FindType(
                "CryingSnow.StackCraft.LocationTransitionReason");
            MethodInfo shouldRefresh = RequireMethod(
                policyType,
                "ShouldRefresh",
                typeof(bool),
                typeof(bool),
                reasonType,
                typeof(int),
                typeof(int));
            object none = Enum.Parse(reasonType, "None");
            object worldMapEntry = Enum.Parse(reasonType, "WorldMapEntry");
            object childReturn = Enum.Parse(reasonType, "ReturnToParent");

            Assert.That(
                shouldRefresh.Invoke(
                    null,
                    new[] { (object)true, false, none, 1, 0 }),
                Is.True);
            Assert.That(
                shouldRefresh.Invoke(
                    null,
                    new[] { (object)true, true, worldMapEntry, 1, 1 }),
                Is.False);
            Assert.That(
                shouldRefresh.Invoke(
                    null,
                    new[] { (object)true, true, worldMapEntry, 2, 1 }),
                Is.True);
            Assert.That(
                shouldRefresh.Invoke(
                    null,
                    new[] { (object)true, true, childReturn, 2, 1 }),
                Is.False);
        }

        [Test]
        public void ProtagonistRules_IdentifyMemberWithoutDependingOnDefinition()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            Type rulesType = FindType("CryingSnow.StackCraft.ProtagonistRules");
            object gameData = Activator.CreateInstance(gameDataType);
            object protagonist = CreateCardData(
                cardDataType,
                "class-changed-definition",
                "hero-id");
            gameDataType.GetField("ProtagonistPersistentId")
                .SetValue(gameData, "hero-id");

            bool identified = (bool)RequireMethod(
                    rulesType,
                    "IsProtagonist",
                    gameDataType,
                    cardDataType)
                .Invoke(null, new[] { gameData, protagonist });

            Assert.That(identified, Is.True);
        }

        [Test]
        public void ProtagonistRecovery_RequiresLivingCompanionAndClearsDownedState()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            Type recoveryType = FindType(
                "CryingSnow.StackCraft.ProtagonistRecoveryService");
            object gameData = Activator.CreateInstance(gameDataType);
            object protagonist = CreateCardData(cardDataType, "traveler", "hero-id");
            object companion = CreateCardData(cardDataType, "guard", "guard-id");
            cardDataType.GetField("IsDowned").SetValue(protagonist, true);
            cardDataType.GetField("CurrentHealth").SetValue(protagonist, 0);
            cardDataType.GetField("CurrentHealth").SetValue(companion, 8);
            gameDataType.GetField("ProtagonistPersistentId")
                .SetValue(gameData, "hero-id");
            IList members = GetPartyMembers(gameDataType, gameData);
            members.Add(protagonist);
            members.Add(companion);

            Assert.That(
                RequireMethod(recoveryType, "CanBeRescued", gameDataType)
                    .Invoke(null, new[] { gameData }),
                Is.True);
            Assert.That(
                RequireMethod(
                        recoveryType,
                        "Revive",
                        cardDataType,
                        typeof(int),
                        typeof(int))
                    .Invoke(null, new[] { protagonist, (object)3, (object)1 }),
                Is.True);
            Assert.That(
                cardDataType.GetField("IsDowned").GetValue(protagonist),
                Is.False);
            Assert.That(
                cardDataType.GetField("CurrentHealth").GetValue(protagonist),
                Is.EqualTo(3));
            Assert.That(
                cardDataType.GetField("CurrentEnergy").GetValue(protagonist),
                Is.EqualTo(1));
        }

        [Test]
        public void ProtagonistRules_DownedHeroCannotUseNormalLocationReturn()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            Type rulesType = FindType("CryingSnow.StackCraft.ProtagonistRules");
            object gameData = Activator.CreateInstance(gameDataType);
            object protagonist = CreateCardData(
                cardDataType,
                "traveler",
                "hero-id");
            gameDataType.GetField("ProtagonistPersistentId")
                .SetValue(gameData, "hero-id");
            GetPartyMembers(gameDataType, gameData).Add(protagonist);
            MethodInfo canLeave = RequireMethod(
                rulesType,
                "CanLeaveLocationNormally",
                gameDataType);

            cardDataType.GetField("IsDowned").SetValue(protagonist, true);
            Assert.That(
                canLeave.Invoke(null, new[] { gameData }),
                Is.False);

            cardDataType.GetField("IsDowned").SetValue(protagonist, false);
            Assert.That(
                canLeave.Invoke(null, new[] { gameData }),
                Is.True);
        }

        [Test]
        public void ProtagonistRecovery_SelectsMedicineInsteadOfOrdinaryFood()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            Type backpackDataType = FindType(
                "CryingSnow.StackCraft.BackpackData");
            Type backpackEntryType = FindType(
                "CryingSnow.StackCraft.BackpackEntryData");
            Type recoveryType = FindType(
                "CryingSnow.StackCraft.ProtagonistRecoveryService");
            object gameData = Activator.CreateInstance(gameDataType);
            object backpack = gameDataType.GetField("Backpack").GetValue(gameData);
            IList entries = (IList)backpackDataType.GetField("Entries")
                .GetValue(backpack);
            object foodEntry = CreateBackpackEntry(
                backpackEntryType,
                cardDataType,
                "food-entry",
                "berry");
            object medicineEntry = CreateBackpackEntry(
                backpackEntryType,
                cardDataType,
                "medicine-entry",
                "medicine");
            entries.Add(foodEntry);
            entries.Add(medicineEntry);

            object result = RequireMethod(
                    recoveryType,
                    "FindRescueMedicineEntry",
                    gameDataType)
                .Invoke(null, new[] { gameData });

            Assert.That(result, Is.SameAs(medicineEntry));
        }

        [Test]
        public void ProtagonistRetreat_AppliesTimeAndCoinCostAndRevivesHero()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            Type backpackDataType = FindType(
                "CryingSnow.StackCraft.BackpackData");
            Type backpackEntryType = FindType(
                "CryingSnow.StackCraft.BackpackEntryData");
            Type retreatType = FindType(
                "CryingSnow.StackCraft.ProtagonistRetreatService");
            object gameData = Activator.CreateInstance(gameDataType);
            object protagonist = CreateCardData(
                cardDataType,
                "traveler",
                "hero-id");
            cardDataType.GetField("IsDowned").SetValue(protagonist, true);
            cardDataType.GetField("CurrentHealth").SetValue(protagonist, 0);
            cardDataType.GetField("MaximumHealth").SetValue(protagonist, 15);
            gameDataType.GetField("ProtagonistPersistentId")
                .SetValue(gameData, "hero-id");
            gameDataType.GetField("WorldElapsedHours")
                .SetValue(gameData, 47L);
            GetPartyMembers(gameDataType, gameData).Add(protagonist);

            object backpack = gameDataType.GetField("Backpack").GetValue(gameData);
            IList entries = (IList)backpackDataType.GetField("Entries")
                .GetValue(backpack);
            for (int index = 0; index < 5; index++)
            {
                entries.Add(CreateBackpackEntry(
                    backpackEntryType,
                    cardDataType,
                    $"coin-{index}",
                    "coin-definition"));
            }
            entries.Add(CreateBackpackEntry(
                backpackEntryType,
                cardDataType,
                "berry-entry",
                "berry"));

            object result = RequireMethod(
                    retreatType,
                    "Apply",
                    gameDataType,
                    typeof(string),
                    typeof(int),
                    typeof(int))
                .Invoke(
                    null,
                    new object[]
                    {
                        gameData,
                        "coin-definition",
                        6,
                        3
                    });

            Assert.That(
                result.GetType().GetProperty("Succeeded").GetValue(result),
                Is.True);
            Assert.That(
                result.GetType().GetProperty("HoursLost").GetValue(result),
                Is.EqualTo(6));
            Assert.That(
                result.GetType().GetProperty("CoinsLost").GetValue(result),
                Is.EqualTo(3));
            Assert.That(
                gameDataType.GetField("WorldElapsedHours").GetValue(gameData),
                Is.EqualTo(53L));
            Assert.That(
                gameDataType.GetField("WorldDay").GetValue(gameData),
                Is.EqualTo(3));
            Assert.That(
                cardDataType.GetField("IsDowned").GetValue(protagonist),
                Is.False);
            Assert.That(
                cardDataType.GetField("CurrentHealth").GetValue(protagonist),
                Is.EqualTo(1));
            Assert.That(entries.Count, Is.EqualTo(3));
        }

        [Test]
        public void ProtagonistChronicle_CapturesFinalRunSummary()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            Type chronicleServiceType = FindType(
                "CryingSnow.StackCraft.ProtagonistChronicleService");
            object gameData = Activator.CreateInstance(gameDataType);
            object protagonist = CreateCardData(
                cardDataType,
                "traveler",
                "hero-id");
            cardDataType.GetField("Level").SetValue(protagonist, 4);
            cardDataType.GetField("CurrentHealth").SetValue(protagonist, 0);
            cardDataType.GetField("IsDowned").SetValue(protagonist, true);
            gameDataType.GetField("SlotNumber").SetValue(gameData, 2);
            gameDataType.GetField("ActiveLocationId")
                .SetValue(gameData, "whispering-forest");
            gameDataType.GetField("WorldDay").SetValue(gameData, 7);
            gameDataType.GetField("ProtagonistPersistentId")
                .SetValue(gameData, "hero-id");
            GetPartyMembers(gameDataType, gameData).Add(protagonist);
            GetPartyMembers(gameDataType, gameData).Add(
                CreateCardData(cardDataType, "guard", "guard-id"));

            object chronicle = RequireMethod(
                    chronicleServiceType,
                    "Create",
                    gameDataType,
                    typeof(string))
                .Invoke(null, new[] { gameData, "战斗重伤" });
            Type chronicleType = chronicle.GetType();

            Assert.That(
                chronicleType.GetField("SlotNumber").GetValue(chronicle),
                Is.EqualTo(2));
            Assert.That(
                chronicleType.GetField("ProtagonistLevel").GetValue(chronicle),
                Is.EqualTo(4));
            Assert.That(
                chronicleType.GetField("WorldDay").GetValue(chronicle),
                Is.EqualTo(7));
            Assert.That(
                chronicleType.GetField("LocationId").GetValue(chronicle),
                Is.EqualTo("whispering-forest"));
            Assert.That(
                chronicleType.GetField("PartySize").GetValue(chronicle),
                Is.EqualTo(2));
            Assert.That(
                chronicleType.GetField("DeathCause").GetValue(chronicle),
                Is.EqualTo("战斗重伤"));
        }

        [Test]
        public void ProgressionFeedback_DescribesEveryLevelUpStatChange()
        {
            Type feedbackType = FindType(
                "CryingSnow.StackCraft.CharacterProgressionFeedback");
            string message = (string)RequireMethod(
                    feedbackType,
                    "BuildLevelUpMessage",
                    typeof(string),
                    typeof(int),
                    typeof(int))
                .Invoke(null, new object[] { "旅行者", 1, 3 });

            Assert.That(message, Does.Contain("旅行者升到 3 级"));
            Assert.That(message, Does.Contain("最大生命 +4"));
            Assert.That(message, Does.Contain("攻击 +1"));
            Assert.That(message, Does.Contain("防御 +1"));
        }

        [Test]
        public void ProgressionFeedback_BuildsReadableExperienceBar()
        {
            Type feedbackType = FindType(
                "CryingSnow.StackCraft.CharacterProgressionFeedback");
            string bar = (string)RequireMethod(
                    feedbackType,
                    "BuildExperienceBar",
                    typeof(int),
                    typeof(int),
                    typeof(int))
                .Invoke(null, new object[] { 1, 5, 10 });

            Assert.That(bar, Does.Contain("5/15"));
            Assert.That(bar.Count(character => character == '■'), Is.EqualTo(3));
            Assert.That(bar.Count(character => character == '□'), Is.EqualTo(7));
        }

        [Test]
        public void GameData_UpdatePartyMembers_PreservesHeroWhenActiveCaptureMissesIt()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object gameData = Activator.CreateInstance(gameDataType);
            gameDataType.GetField("ProtagonistPersistentId")
                .SetValue(gameData, "hero-id");
            GetPartyMembers(gameDataType, gameData).Add(
                CreateCardData(cardDataType, "traveler", "hero-id"));
            object companion = CreateCardData(
                cardDataType,
                "guard",
                "guard-id");
            Type enumerableType = typeof(System.Collections.Generic.IEnumerable<>)
                .MakeGenericType(cardDataType);
            Array party = Array.CreateInstance(cardDataType, 1);
            party.SetValue(companion, 0);

            RequireMethod(gameDataType, "UpdatePartyMembers", enumerableType)
                .Invoke(gameData, new object[] { party });

            Assert.That(
                gameDataType.GetField("ProtagonistPersistentId").GetValue(gameData),
                Is.EqualTo("hero-id"));
            Assert.That(
                RequireMethod(gameDataType, "GetProtagonistData")
                    .Invoke(gameData, null),
                Is.Not.Null);
        }

        [Test]
        public void GameData_UpdatePartyMembers_AlwaysKeepsHeroInsideFourMemberLimit()
        {
            Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object gameData = Activator.CreateInstance(gameDataType);
            gameDataType.GetField("ProtagonistPersistentId")
                .SetValue(gameData, "hero-id");
            Array party = Array.CreateInstance(cardDataType, 5);
            for (int index = 0; index < 4; index++)
            {
                party.SetValue(
                    CreateCardData(
                        cardDataType,
                        "companion",
                        $"companion-{index}"),
                    index);
            }
            object protagonist = CreateCardData(
                cardDataType,
                "traveler",
                "hero-id");
            party.SetValue(protagonist, 4);
            Type enumerableType = typeof(System.Collections.Generic.IEnumerable<>)
                .MakeGenericType(cardDataType);

            RequireMethod(gameDataType, "UpdatePartyMembers", enumerableType)
                .Invoke(gameData, new object[] { party });

            IList members = GetPartyMembers(gameDataType, gameData);
            Assert.That(members.Count, Is.EqualTo(4));
            Assert.That(
                members.Cast<object>().Any(member =>
                    (string)cardDataType.GetField("PersistentId")
                        .GetValue(member) == "hero-id"),
                Is.True);
        }

        private static object CreateCardData(
            Type cardDataType,
            string definitionId,
            string persistentId)
        {
            object data = Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(data, definitionId);
            cardDataType.GetField("PersistentId").SetValue(data, persistentId);
            return data;
        }

        private static object CreateBackpackEntry(
            Type backpackEntryType,
            Type cardDataType,
            string instanceId,
            string definitionId)
        {
            object entry = Activator.CreateInstance(backpackEntryType);
            backpackEntryType.GetField("InstanceId").SetValue(
                entry,
                instanceId);
            backpackEntryType.GetField("Card").SetValue(
                entry,
                CreateCardData(
                    cardDataType,
                    definitionId,
                    $"{instanceId}-card"));
            return entry;
        }

        private static IList GetPartyMembers(Type gameDataType, object gameData)
        {
            return (IList)gameDataType.GetField("PartyMembers").GetValue(gameData);
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
                $"Missing method {type.FullName}.{name}({string.Join(", ", parameterTypes.Select(value => value.Name))})");
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
    }
}
