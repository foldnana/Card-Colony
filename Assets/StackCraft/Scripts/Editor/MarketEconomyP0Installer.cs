#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class MarketEconomyP0Installer
    {
        private const string Root =
            "Assets/StackCraft/Resources/Trading";
        private const string CommodityRoot = Root + "/Commodities";
        private const string MarketRoot = Root + "/Markets";
        private const string NpcRoot = Root + "/Npcs";

        [MenuItem("Tools/StackCraft/Install Market Economy P0")]
        public static void Install()
        {
            EnsureFolder(
                "Assets/StackCraft/Resources",
                "Trading");
            EnsureFolder(Root, "Commodities");
            EnsureFolder(Root, "Markets");
            EnsureFolder(Root, "Npcs");

            CreateMissingTradeCards();
            Dictionary<string, CommodityDefinition> commodities =
                CreateCommodities();
            Dictionary<string, MarketProfile> markets =
                CreateMarkets(commodities);
            ConfigureRiverbendGrocer(markets["riverbend-market"]);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Market economy P0 assets installed: 8 commodities, " +
                "3 regional markets, Riverbend grocer profile.");
        }

        [MenuItem("Tools/StackCraft/Market Economy/Log P0 Quotes")]
        public static void LogP0Quotes()
        {
            CommodityDefinition[] commodities = Resources
                .LoadAll<CommodityDefinition>("Trading/Commodities");
            MarketProfile[] markets = Resources
                .LoadAll<MarketProfile>("Trading/Markets");
            var catalog = new InMemoryMarketCatalog(
                commodities,
                markets);
            var repository = new InMemoryMarketStateRepository();
            var service = new MarketService(
                catalog,
                new MutableWorldClock(0),
                repository,
                417);
            foreach (MarketProfile market in markets)
            {
                foreach (MarketCommodityRule rule in
                         market.CommodityRules)
                {
                    MarketQuote quote = service.GetQuote(
                        market.Id,
                        rule.Commodity.Id,
                        MerchantPriceModifiers.Default);
                    Debug.Log(
                        $"[行情] {market.Id}/{rule.Commodity.DisplayName}: " +
                        $"买 {quote.PlayerBuyUnitPrice}, " +
                        $"卖 {quote.PlayerSellUnitPrice}, " +
                        $"库存 {quote.AvailableStock}, " +
                        $"趋势 {quote.Trend}");
                }
            }
        }

        private static Dictionary<string, CommodityDefinition>
            CreateCommodities()
        {
            var result = new Dictionary<string, CommodityDefinition>();
            CreateCommodity(
                result, "food", "粮食", 4, 2f,
                CommodityCategory.Food, 0.55f,
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Potato.asset",
                "food", "basic");
            CreateCommodity(
                result, "wood", "木材", 5, 2f,
                CommodityCategory.Material, 0.4f,
                "Assets/StackCraft/Resources/Cards/Materials/Card_Wood.asset",
                "wood", "basic");
            CreateCommodity(
                result, "salt", "盐", 8, 0.3f,
                CommodityCategory.Material, 0.3f,
                "Assets/StackCraft/Resources/Cards/Materials/Card_Salt.asset",
                "salt", "processed");
            CreateCommodity(
                result, "cloth", "布料", 12, 0.4f,
                CommodityCategory.Manufactured, 0.35f,
                "Assets/StackCraft/Resources/Cards/Materials/Card_Fiber.asset",
                "cloth", "processed");
            CreateCommodity(
                result, "tools", "工具", 18, 1f,
                CommodityCategory.Manufactured, 0.4f,
                "Assets/StackCraft/Resources/Cards/Equipments/Card_WoodenClub.asset",
                "tools", "processed");
            CreateCommodity(
                result, "ore", "矿石", 14, 3f,
                CommodityCategory.Material, 0.25f,
                "Assets/StackCraft/Resources/Cards/Materials/Card_IronOre.asset",
                "ore", "mineral");
            CreateCommodity(
                result, "medicine", "药品", 24, 0.2f,
                CommodityCategory.Medicine, 0.6f,
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Medicine.asset",
                "medicine", "processed");
            CreateCommodity(
                result, "rope", "绳索", 9, 0.5f,
                CommodityCategory.Manufactured, 0.35f,
                "Assets/StackCraft/Resources/Cards/Materials/Card_Rope.asset",
                "rope", "basic");
            return result;
        }

        private static void CreateCommodity(
            IDictionary<string, CommodityDefinition> result,
            string id,
            string displayName,
            int basePrice,
            float weight,
            CommodityCategory category,
            float volatility,
            string cardPath,
            params string[] tags)
        {
            string path = $"{CommodityRoot}/Commodity_{id}.asset";
            CommodityDefinition asset =
                AssetDatabase.LoadAssetAtPath<CommodityDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject
                    .CreateInstance<CommodityDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var serialized = new SerializedObject(asset);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue =
                displayName;
            serialized.FindProperty("cardDefinition").objectReferenceValue =
                string.IsNullOrWhiteSpace(cardPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<CardDefinition>(
                        cardPath);
            serialized.FindProperty("basePrice").intValue = basePrice;
            serialized.FindProperty("unitWeight").floatValue = weight;
            serialized.FindProperty("stackLimit").intValue = 20;
            serialized.FindProperty("category").enumValueIndex =
                (int)category;
            serialized.FindProperty("volatility").floatValue = volatility;
            serialized.FindProperty("isTradeCommodity").boolValue = true;
            SetStringArray(serialized.FindProperty("tradeTags"), tags);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            result[id] = asset;
        }

        private static Dictionary<string, MarketProfile> CreateMarkets(
            IReadOnlyDictionary<string, CommodityDefinition> commodities)
        {
            CardDefinition currency =
                AssetDatabase.LoadAssetAtPath<CardDefinition>(
                    "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset");
            var result = new Dictionary<string, MarketProfile>();
            result["riverbend-market"] = CreateMarket(
                "riverbend-market",
                "riverbend-market",
                currency,
                commodities,
                new[]
                {
                    Rule("food", .78f, 5, 1),
                    Rule("wood", .82f, 5, 1),
                    Rule("salt", 1.18f, 0, 3),
                    Rule("cloth", 1.15f, 1, 2),
                    Rule("tools", 1.22f, 0, 3),
                    Rule("ore", 1.05f, 1, 2),
                    Rule("medicine", .95f, 2, 2),
                    Rule("rope", .82f, 4, 1)
                });
            result["whitestone-market"] = CreateMarket(
                "whitestone-market",
                "whitestone",
                currency,
                commodities,
                new[]
                {
                    Rule("food", 1.15f, 1, 4),
                    Rule("wood", 1.18f, 1, 4),
                    Rule("salt", .82f, 4, 1),
                    Rule("cloth", .80f, 5, 1),
                    Rule("tools", .82f, 5, 1),
                    Rule("ore", 1.30f, 1, 4),
                    Rule("medicine", .92f, 3, 2),
                    Rule("rope", 1.02f, 2, 2)
                });
            result["old-mine-market"] = CreateMarket(
                "old-mine-market",
                "old-mine",
                currency,
                commodities,
                new[]
                {
                    Rule("food", 1.80f, 0, 5),
                    Rule("wood", 1.08f, 1, 3),
                    Rule("salt", 1.18f, 0, 3),
                    Rule("cloth", 1.12f, 0, 3),
                    Rule("tools", 1.28f, 0, 4),
                    Rule("ore", .75f, 6, 1),
                    Rule("medicine", 1.35f, 0, 4),
                    Rule("rope", 1.25f, 0, 4)
                });
            return result;
        }

        private static void CreateMissingTradeCards()
        {
            CreateTradeCard(
                "Assets/StackCraft/Resources/Cards/Materials/Card_Salt.asset",
                "Assets/StackCraft/Resources/Cards/Materials/Card_Flint.asset",
                "salt",
                "Salt",
                "用于保存食物与调味的轻型贸易品。",
                CardCategory.Material,
                8);
            CreateTradeCard(
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Medicine.asset",
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Soup.asset",
                "medicine",
                "Medicine",
                "旅途中用于治疗伤病的常备药品。",
                CardCategory.Consumable,
                24);
        }

        private static void CreateTradeCard(
            string path,
            string templatePath,
            string id,
            string displayName,
            string description,
            CardCategory category,
            int sellPrice)
        {
            CardDefinition asset =
                AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
            if (asset == null)
            {
                CardDefinition template =
                    AssetDatabase.LoadAssetAtPath<CardDefinition>(
                        templatePath);
                asset = template != null
                    ? Object.Instantiate(template)
                    : ScriptableObject.CreateInstance<CardDefinition>();
                asset.name = System.IO.Path.GetFileNameWithoutExtension(
                    path);
                AssetDatabase.CreateAsset(asset, path);
            }

            var serialized = new SerializedObject(asset);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue =
                displayName;
            serialized.FindProperty("description").stringValue =
                description;
            serialized.FindProperty("category").enumValueIndex =
                (int)category;
            serialized.FindProperty("isSellable").boolValue = true;
            serialized.FindProperty("sellPrice").intValue = sellPrice;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static MarketProfile CreateMarket(
            string id,
            string locationId,
            CardDefinition currency,
            IReadOnlyDictionary<string, CommodityDefinition> commodities,
            IReadOnlyList<RuleData> rules)
        {
            string path = $"{MarketRoot}/Market_{id}.asset";
            MarketProfile asset =
                AssetDatabase.LoadAssetAtPath<MarketProfile>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MarketProfile>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var serialized = new SerializedObject(asset);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("locationId").stringValue = locationId;
            serialized.FindProperty("currency").objectReferenceValue =
                currency;
            serialized.FindProperty("baseSpread").floatValue = .18f;
            serialized.FindProperty("startingFunds").intValue = 300;
            serialized.FindProperty("maximumFunds").intValue = 600;
            serialized.FindProperty("fundRecovery").intValue = 60;
            serialized.FindProperty("refreshHoursMin").intValue = 48;
            serialized.FindProperty("refreshHoursMax").intValue = 96;
            serialized.FindProperty("transactionFee").floatValue = .02f;
            SerializedProperty serializedRules =
                serialized.FindProperty("commodityRules");
            serializedRules.arraySize = rules.Count;
            for (int index = 0; index < rules.Count; index++)
            {
                RuleData rule = rules[index];
                SerializedProperty element =
                    serializedRules.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("commodity")
                    .objectReferenceValue = commodities[rule.Id];
                element.FindPropertyRelative("regionalPriceFactor")
                    .floatValue = rule.Factor;
                element.FindPropertyRelative("initialStockMin")
                    .intValue = 18;
                element.FindPropertyRelative("initialStockMax")
                    .intValue = 22;
                element.FindPropertyRelative("targetStock")
                    .intValue = 20;
                element.FindPropertyRelative("productionPerRefresh")
                    .intValue = rule.Production;
                element.FindPropertyRelative("consumptionPerRefresh")
                    .intValue = rule.Consumption;
                element.FindPropertyRelative("minimumStock")
                    .intValue = 0;
                element.FindPropertyRelative("maximumStock")
                    .intValue = 60;
                element.FindPropertyRelative("allowsPlayerPurchase")
                    .boolValue = true;
                element.FindPropertyRelative("allowsPlayerSale")
                    .boolValue = true;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void ConfigureRiverbendGrocer(
            MarketProfile riverbendMarket)
        {
            const string locationPath =
                "Assets/StackCraft/Resources/Locations/Location_RiverbendMarket.asset";
            const string profilePath =
                NpcRoot + "/NpcTradeProfile_RiverbendGrocer.asset";
            LocationDefinition location =
                AssetDatabase.LoadAssetAtPath<LocationDefinition>(
                    locationPath);
            if (location == null || location.MarketOffers.Count == 0)
                return;

            NpcTradeProfile profile =
                AssetDatabase.LoadAssetAtPath<NpcTradeProfile>(profilePath);
            if (profile == null)
            {
                profile =
                    ScriptableObject.CreateInstance<NpcTradeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("npcDefinition").objectReferenceValue =
                location.MarketOffers[0].SourceCardDefinition;
            serialized.FindProperty("roleLabelOverride").stringValue =
                "杂货商";
            serialized.FindProperty("startingFundsOverride").intValue =
                300;
            SerializedProperty categories =
                serialized.FindProperty("buyCategoriesOverride");
            categories.arraySize = 4;
            categories.GetArrayElementAtIndex(0).enumValueIndex =
                (int)CardCategory.Consumable;
            categories.GetArrayElementAtIndex(1).enumValueIndex =
                (int)CardCategory.Material;
            categories.GetArrayElementAtIndex(2).enumValueIndex =
                (int)CardCategory.Valuable;
            categories.GetArrayElementAtIndex(3).enumValueIndex =
                (int)CardCategory.Equipment;
            serialized.FindProperty("marketProfile").objectReferenceValue =
                riverbendMarket;
            SetStringArray(
                serialized.FindProperty("sellCommodityTags"),
                new[]
                {
                    "food", "wood", "salt", "cloth",
                    "tools", "ore", "medicine", "rope"
                });
            SetStringArray(
                serialized.FindProperty("buyCommodityTags"),
                new[]
                {
                    "food", "wood", "salt", "cloth",
                    "tools", "ore", "medicine", "rope"
                });
            serialized.FindProperty("usesMarketFunds").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);

            var locationSerialized = new SerializedObject(location);
            SerializedProperty profiles =
                locationSerialized.FindProperty("npcTradeProfiles");
            profiles.arraySize = 1;
            profiles.GetArrayElementAtIndex(0).objectReferenceValue =
                profile;
            locationSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(location);
        }

        private static void SetStringArray(
            SerializedProperty property,
            IReadOnlyList<string> values)
        {
            property.arraySize = values?.Count ?? 0;
            for (int index = 0; index < property.arraySize; index++)
                property.GetArrayElementAtIndex(index).stringValue =
                    values[index];
        }

        private static RuleData Rule(
            string id,
            float factor,
            int production,
            int consumption)
        {
            return new RuleData(id, factor, production, consumption);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private readonly struct RuleData
        {
            public RuleData(
                string id,
                float factor,
                int production,
                int consumption)
            {
                Id = id;
                Factor = factor;
                Production = production;
                Consumption = consumption;
            }

            public string Id { get; }
            public float Factor { get; }
            public int Production { get; }
            public int Consumption { get; }
        }
    }
}
#endif
