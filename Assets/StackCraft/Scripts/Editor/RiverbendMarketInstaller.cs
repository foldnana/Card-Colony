#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class RiverbendMarketInstaller
    {
        private const string LocationScenePath =
            "Assets/StackCraft/Scenes/Location.unity";
        private const string RiverbendPath =
            "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset";
        private const string MarketPath =
            "Assets/StackCraft/Resources/Locations/Location_RiverbendMarket.asset";
        private const string MarketBuildingPath =
            "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Market.asset";
        private const string GrocerPath =
            "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Grocer.asset";
        private const string VillagerPath =
            "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset";
        private const string CoinPath =
            "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset";
        private const string PickupArtPath =
            "Assets/StackCraft/Resources/Cards/Specials/Chest/Card_Chest_WoodenChest.asset";
        private const string CardsFolder =
            "Assets/StackCraft/Resources/Cards/Locations/Market";
        private const string BackgroundPath =
            "Assets/CardColony/Art/Backgrounds/Market/RiverbendMarketInteriorBackground.png";
        private const string StructureBasePath =
            "Assets/CardColony/Art/CardBases/Riverbend/Riverbend_StructureBase.png";

        private readonly struct OfferSpec
        {
            public OfferSpec(
                string assetName,
                string id,
                string displayName,
                string description,
                string productPath,
                int buyPrice,
                int minimumDailyStock,
                int maximumDailyStock,
                Vector3 position)
            {
                AssetName = assetName;
                Id = id;
                DisplayName = displayName;
                Description = description;
                ProductPath = productPath;
                BuyPrice = buyPrice;
                MinimumDailyStock = minimumDailyStock;
                MaximumDailyStock = maximumDailyStock;
                Position = position;
            }

            public string AssetName { get; }
            public string Id { get; }
            public string DisplayName { get; }
            public string Description { get; }
            public string ProductPath { get; }
            public int BuyPrice { get; }
            public int MinimumDailyStock { get; }
            public int MaximumDailyStock { get; }
            public Vector3 Position { get; }
        }

        private static readonly OfferSpec[] Offers =
        {
            new(
                "Card_Market_AppleStall",
                "riverbend-market-apple-stall",
                "苹果摊",
                "出售新鲜苹果的小摊。点击查看今日价格和库存。",
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset",
                3,
                2,
                4,
                new Vector3(-5.4f, 0f, 1f)),
            new(
                "Card_Market_BerryStall",
                "riverbend-market-berry-stall",
                "浆果摊",
                "出售当日采摘浆果的小摊。点击查看今日价格和库存。",
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Berry.asset",
                2,
                3,
                5,
                new Vector3(-3.7f, 0f, 1f)),
            new(
                "Card_Market_PotatoStall",
                "riverbend-market-potato-stall",
                "土豆摊",
                "出售耐储存土豆的小摊。点击查看今日价格和库存。",
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Potato.asset",
                3,
                2,
                4,
                new Vector3(-5.4f, 0f, -0.8f)),
            new(
                "Card_Market_RawMeatStall",
                "riverbend-market-raw-meat-stall",
                "生肉摊",
                "出售当日生肉的小摊。点击查看今日价格和库存。",
                "Assets/StackCraft/Resources/Cards/Consumables/Card_RawMeat.asset",
                5,
                1,
                3,
                new Vector3(-3.7f, 0f, -0.8f)),
            new(
                "Card_Market_WoodStall",
                "riverbend-market-wood-stall",
                "木材摊",
                "出售常用木材的小摊。点击查看今日价格和库存。",
                "Assets/StackCraft/Resources/Cards/Materials/Card_Wood.asset",
                2,
                2,
                4,
                new Vector3(3.8f, 0f, 1f)),
            new(
                "Card_Market_StoneStall",
                "riverbend-market-stone-stall",
                "石料摊",
                "出售常用石料的小摊。点击查看今日价格和库存。",
                "Assets/StackCraft/Resources/Cards/Materials/Card_Stone.asset",
                2,
                2,
                4,
                new Vector3(5.4f, 0f, 1f)),
            new(
                "Card_Market_RopeStall",
                "riverbend-market-rope-stall",
                "绳子摊",
                "出售旅行常用绳索的小摊。点击查看今日价格和库存。",
                "Assets/StackCraft/Resources/Cards/Materials/Card_Rope.asset",
                5,
                1,
                3,
                new Vector3(4.6f, 0f, -0.8f))
        };

        [MenuItem("Tools/Card Colony/Install Riverbend Market")]
        public static void Install()
        {
            EnsureFolder(CardsFolder);
            EnsureBackgroundImportSettings();

            CardDefinition marketBuilding =
                RequireAsset<CardDefinition>(MarketBuildingPath);
            CardDefinition grocer = RequireAsset<CardDefinition>(GrocerPath);
            CardDefinition pickupArt =
                RequireAsset<CardDefinition>(PickupArtPath);
            CardDefinition pickup = CreateOrUpdateServiceCard(
                "Card_Market_Pickup",
                "riverbend-market-pickup",
                "取货台",
                "购买的商品会出现在取货台旁边。",
                pickupArt.ArtTexture);

            var products = new Dictionary<string, CardDefinition>();
            foreach (OfferSpec offer in Offers)
            {
                CardDefinition product =
                    RequireAsset<CardDefinition>(offer.ProductPath);
                products[offer.Id] = product;
            }

            LocationDefinition market = CreateOrUpdateMarket(
                grocer,
                pickup,
                products);
            LocationDefinition riverbend =
                RequireAsset<LocationDefinition>(RiverbendPath);
            LocationTemplateBuilder.UpsertEntrance(
                riverbend,
                marketBuilding,
                "riverbend-market");
            RegisterDefinitionInLocationScene(market);

            EditorUtility.SetDirty(riverbend);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static LocationDefinition CreateOrUpdateMarket(
            CardDefinition grocer,
            CardDefinition pickup,
            IReadOnlyDictionary<string, CardDefinition> products)
        {
            var spawns = new List<LocationTemplateSpawn>
            {
                new(grocer, new Vector3(0f, 0f, 1.25f)),
                new(pickup, new Vector3(4.6f, 0f, -2.65f))
            };
            var marketOffers = new List<LocationTemplateMarketOffer>();
            foreach (OfferSpec offer in Offers)
            {
                marketOffers.Add(new LocationTemplateMarketOffer(
                    grocer,
                    products[offer.Id],
                    offer.BuyPrice,
                    offer.MinimumDailyStock,
                    offer.MaximumDailyStock,
                    offer.Id));
            }

            return LocationTemplateBuilder.CreateOrUpdate(
                MarketPath,
                new LocationTemplate
                {
                    Id = "riverbend-market",
                    DisplayName = "河湾市场",
                    BackgroundTexture =
                        RequireAsset<Texture2D>(BackgroundPath),
                    MapSize = new Vector2(18.4f, 10.35f),
                    CameraMinDistance = 3f,
                    CameraMaxDistance = 20f,
                    CameraInitialDistance = 7f,
                    CameraZoomSpeed = 3f,
                    ExpandedPartyMemberDefinition =
                        RequireAsset<CardDefinition>(VillagerPath),
                    PartySpawnPosition = new Vector3(0f, 0f, -3.65f),
                    PartyMemberSpacing = 0.9f,
                    InitialCardSpawns = spawns,
                    Entrances = Array.Empty<LocationTemplateEntrance>(),
                    MarketCurrencyCardDefinition =
                        RequireAsset<CardDefinition>(CoinPath),
                    MarketBuyerCardDefinition = null,
                    MarketPickupCardDefinition = pickup,
                    MarketOffers = marketOffers
                });
        }

        private static CardDefinition CreateOrUpdateServiceCard(
            string assetName,
            string id,
            string displayName,
            string description,
            Texture2D artTexture)
        {
            string assetPath = $"{CardsFolder}/{assetName}.asset";
            CardDefinition definition =
                AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CardDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("artTexture").objectReferenceValue =
                artTexture;
            serialized.FindProperty("baseTextureOverride").objectReferenceValue =
                RequireAsset<Texture2D>(StructureBasePath);
            serialized.FindProperty("category").enumValueIndex =
                (int)CardCategory.Structure;
            serialized.FindProperty("faction").enumValueIndex =
                (int)CardFaction.Neutral;
            serialized.FindProperty("isLocationStatic").boolValue = true;
            serialized.FindProperty("playerDraggable").boolValue = false;
            serialized.FindProperty("ambientNpcAiEnabled").boolValue = false;
            serialized.FindProperty("dialogueEnabled").boolValue = false;
            serialized.FindProperty("combatType").enumValueIndex =
                (int)CombatType.None;
            serialized.FindProperty("loot").ClearArray();
            serialized.FindProperty("isAggressive").boolValue = false;
            serialized.FindProperty("isSellable").boolValue = false;
            serialized.FindProperty("sellPrice").intValue = 0;
            serialized.FindProperty("hasDurability").boolValue = false;
            serialized.FindProperty("uses").intValue = 1;
            serialized.FindProperty("nutrition").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void RegisterDefinitionInLocationScene(
            LocationDefinition market)
        {
            var scene = EditorSceneManager.OpenScene(
                LocationScenePath,
                OpenSceneMode.Single);
            LocationSceneController controller =
                UnityEngine.Object.FindObjectOfType<LocationSceneController>(true);
            if (controller == null)
                throw new InvalidOperationException(
                    "LocationSceneController is missing.");

            LocationTemplateBuilder.UpsertDefinition(
                controller,
                RequireAsset<LocationDefinition>(RiverbendPath));
            LocationTemplateBuilder.UpsertDefinition(controller, market);

            TradeManager tradeManager =
                UnityEngine.Object.FindObjectOfType<TradeManager>(true);
            if (tradeManager == null)
                throw new InvalidOperationException(
                    "TradeManager is missing.");

            var tradeManagerSerialized = new SerializedObject(tradeManager);
            tradeManagerSerialized.FindProperty("spawnLegacyZones")
                .boolValue = true;
            SerializedProperty excludedLocations =
                tradeManagerSerialized.FindProperty(
                    "legacyZoneExcludedLocationIds");
            excludedLocations.arraySize = 1;
            excludedLocations.GetArrayElementAtIndex(0).stringValue =
                "riverbend-market";
            tradeManagerSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tradeManager);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException(
                    $"Required market asset is missing: {path}");
            return asset;
        }

        private static void EnsureBackgroundImportSettings()
        {
            if (AssetImporter.GetAtPath(BackgroundPath) is not
                TextureImporter importer)
            {
                throw new InvalidOperationException(
                    $"Market background is missing: {BackgroundPath}");
            }

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
#endif
