#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class RiverbendInnInstaller
    {
        private const string LocationScenePath = "Assets/StackCraft/Scenes/Location.unity";
        private const string RiverbendPath =
            "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset";
        private const string InnPath =
            "Assets/StackCraft/Resources/Locations/Location_RiverbendInn.asset";
        private const string InnBuildingPath =
            "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset";
        private const string VillagerPath =
            "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset";
        private const string CardsFolder =
            "Assets/StackCraft/Resources/Cards/Locations/Inn";
        private const string ArtFolder = "Assets/CardColony/Art/CardArts/Inn";
        private const string BackgroundPath =
            "Assets/CardColony/Art/Backgrounds/Inn/RiverbendInnInteriorBackground.png";
        private const string StructureBasePath =
            "Assets/CardColony/Art/CardBases/Riverbend/Riverbend_StructureBase.png";
        private const string CharacterBasePath =
            "Assets/CardColony/Art/CardBases/Riverbend/Riverbend_CharacterBase.png";

        private sealed class CardSpec
        {
            public string AssetName;
            public string Id;
            public string DisplayName;
            public string Description;
            public string ArtName;
            public CardCategory Category;
            public bool AmbientNpc;
            public string Opening;
            public string Reply;
            public string Response;
        }

        private readonly struct SpawnSpec
        {
            public readonly string CardId;
            public readonly Vector3 Position;

            public SpawnSpec(string cardId, float x, float z)
            {
                CardId = cardId;
                Position = new Vector3(x, 0f, z);
            }
        }

        private static readonly CardSpec[] Cards =
        {
            new()
            {
                AssetName = "Card_Inn_Reception",
                Id = "riverbend-inn-reception",
                DisplayName = "旅馆前台",
                Description = "登记住宿、寄存钥匙与询问消息的木制前台。",
                ArtName = "Reception.png",
                Category = CardCategory.Structure
            },
            new()
            {
                AssetName = "Card_Inn_Innkeeper",
                Id = "riverbend-innkeeper",
                DisplayName = "旅店老板",
                Description = "经营河湾旅馆，熟悉来往旅客与村里的消息。",
                ArtName = "Innkeeper.png",
                Category = CardCategory.Character,
                Opening = "欢迎来到河湾旅馆。想住店，还是打听些路上的消息？",
                Reply = "最近有什么新鲜事？",
                Response = "往森林去的人多了，但平安回来的却不算多。出发前最好先备好补给。"
            },
            new()
            {
                AssetName = "Card_Inn_Table",
                Id = "riverbend-inn-table",
                DisplayName = "餐桌",
                Description = "旅客吃饭、休息和交换消息的木桌。",
                ArtName = "Table.png",
                Category = CardCategory.Structure
            },
            new()
            {
                AssetName = "Card_Inn_Waiter",
                Id = "riverbend-inn-waiter",
                DisplayName = "小二",
                Description = "在大厅里忙着招呼客人与收拾餐桌。",
                ArtName = "Waiter.png",
                Category = CardCategory.Character,
                AmbientNpc = true,
                Opening = "客人先找张空桌坐吧，有需要尽管叫我。",
                Reply = "这里能买些什么？",
                Response = "热汤、面包和清水都有。等老板点头，我再给你准备房间。"
            },
            new()
            {
                AssetName = "Card_Inn_Bed",
                Id = "riverbend-inn-bed",
                DisplayName = "客房床铺",
                Description = "铺着干净被褥的单人床，可以让旅人休息。",
                ArtName = "Bed.png",
                Category = CardCategory.Structure
            }
        };

        private static readonly SpawnSpec[] Spawns =
        {
            new("riverbend-inn-reception", 3.15f, -2.45f),
            new("riverbend-innkeeper", 3.15f, -1.35f),
            new("riverbend-inn-table", -2.8f, -1.85f),
            new("riverbend-inn-table", -0.35f, -1.85f),
            new("riverbend-inn-table", -2.8f, 0.45f),
            new("riverbend-inn-waiter", -0.35f, 0.45f),
            new("riverbend-inn-bed", -6.15f, 1.8f),
            new("riverbend-inn-bed", -6.15f, -1.65f),
            new("riverbend-inn-bed", 6.15f, 1.8f),
            new("riverbend-inn-bed", 6.15f, -1.65f)
        };

        [MenuItem("Tools/Card Colony/Install Riverbend Inn Interior")]
        public static void Install()
        {
            EnsureFolder(CardsFolder);
            EnsureBackgroundImportSettings();
            Dictionary<string, CardDefinition> cards = Cards.ToDictionary(
                spec => spec.Id,
                CreateOrUpdateCard);
            LocationDefinition inn = CreateOrUpdateInn(cards);
            ConfigureRiverbendEntrance();
            RegisterDefinitionInLocationScene(inn);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static CardDefinition CreateOrUpdateCard(CardSpec spec)
        {
            string artPath = $"{ArtFolder}/{spec.ArtName}";
            EnsureArtMaskImportSettings(artPath);
            string assetPath = $"{CardsFolder}/{spec.AssetName}.asset";
            CardDefinition card = AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardDefinition>();
                AssetDatabase.CreateAsset(card, assetPath);
            }

            var serialized = new SerializedObject(card);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("displayName").stringValue = spec.DisplayName;
            serialized.FindProperty("description").stringValue = spec.Description;
            serialized.FindProperty("artTexture").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(artPath);
            serialized.FindProperty("baseTextureOverride").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    spec.Category == CardCategory.Character
                        ? CharacterBasePath
                        : StructureBasePath);
            serialized.FindProperty("category").enumValueIndex = (int)spec.Category;
            serialized.FindProperty("faction").enumValueIndex = (int)CardFaction.Neutral;
            serialized.FindProperty("isLocationStatic").boolValue = true;
            bool isNpc = spec.Category == CardCategory.Character;
            serialized.FindProperty("playerDraggable").boolValue = false;
            serialized.FindProperty("ambientNpcAiEnabled").boolValue = spec.AmbientNpc;
            serialized.FindProperty("ambientWanderRadius").floatValue = spec.AmbientNpc ? 1.4f : 0f;
            serialized.FindProperty("ambientMoveSpeed").floatValue = 0.45f;
            serialized.FindProperty("ambientIdleRange").vector2Value = new Vector2(2.5f, 5f);
            serialized.FindProperty("dialogueEnabled").boolValue = isNpc;
            serialized.FindProperty("dialogueOpeningText").stringValue = spec.Opening ?? string.Empty;
            serialized.FindProperty("dialogueReplyText").stringValue = spec.Reply ?? string.Empty;
            serialized.FindProperty("dialogueResponseText").stringValue = spec.Response ?? string.Empty;
            serialized.FindProperty("combatType").enumValueIndex = (int)CombatType.None;
            serialized.FindProperty("loot").ClearArray();
            serialized.FindProperty("isAggressive").boolValue = false;
            serialized.FindProperty("isSellable").boolValue = false;
            serialized.FindProperty("hasDurability").boolValue = false;
            serialized.FindProperty("uses").intValue = 1;
            serialized.FindProperty("nutrition").intValue = 0;
            serialized.FindProperty("maxHealth").intValue = 15;
            serialized.FindProperty("attack").intValue = 0;
            serialized.FindProperty("defense").intValue = 0;
            serialized.FindProperty("attackSpeed").intValue = 100;
            serialized.FindProperty("accuracy").intValue = 95;
            serialized.FindProperty("dodge").intValue = 5;
            serialized.FindProperty("criticalChance").intValue = 5;
            serialized.FindProperty("criticalMultiplier").intValue = 150;
            serialized.FindProperty("statModifiers").ClearArray();
            serialized.FindProperty("classChangeResult").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(card);
            return card;
        }

        private static LocationDefinition CreateOrUpdateInn(
            IReadOnlyDictionary<string, CardDefinition> cards)
        {
            var spawns = new List<LocationTemplateSpawn>();
            foreach (SpawnSpec spawn in Spawns)
            {
                spawns.Add(new LocationTemplateSpawn(
                    cards[spawn.CardId],
                    spawn.Position));
            }

            return LocationTemplateBuilder.CreateOrUpdate(
                InnPath,
                new LocationTemplate
                {
                    Id = "riverbend-inn",
                    DisplayName = "河湾旅馆",
                    BackgroundTexture =
                        AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath),
                    MapSize = new Vector2(18.4f, 10.35f),
                    CameraMinDistance = 3f,
                    CameraMaxDistance = 20f,
                    CameraInitialDistance = 7f,
                    CameraZoomSpeed = 3f,
                    ExpandedPartyMemberDefinition =
                        AssetDatabase.LoadAssetAtPath<CardDefinition>(VillagerPath),
                    PartySpawnPosition = new Vector3(0f, 0f, -3.7f),
                    PartyMemberSpacing = 0.9f,
                    InitialCardSpawns = spawns,
                    Entrances = System.Array.Empty<LocationTemplateEntrance>()
                });
        }

        private static void ConfigureRiverbendEntrance()
        {
            LocationDefinition riverbend = AssetDatabase.LoadAssetAtPath<LocationDefinition>(RiverbendPath);
            CardDefinition innBuilding = AssetDatabase.LoadAssetAtPath<CardDefinition>(InnBuildingPath);
            if (riverbend == null || innBuilding == null)
                throw new System.InvalidOperationException("Riverbend location or inn building is missing.");

            UpsertEntrance(riverbend, innBuilding, "riverbend-inn");
        }

        private static void RegisterDefinitionInLocationScene(LocationDefinition inn)
        {
            var scene = EditorSceneManager.OpenScene(LocationScenePath, OpenSceneMode.Single);
            LocationSceneController controller = Object.FindObjectOfType<LocationSceneController>(true);
            if (controller == null)
                throw new System.InvalidOperationException("LocationSceneController is missing.");

            LocationDefinition riverbend = AssetDatabase.LoadAssetAtPath<LocationDefinition>(RiverbendPath);
            UpsertDefinition(controller, riverbend);
            UpsertDefinition(controller, inn);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void UpsertEntrance(
            LocationDefinition location,
            CardDefinition sourceCard,
            string destinationLocationId)
        {
            LocationTemplateBuilder.UpsertEntrance(
                location,
                sourceCard,
                destinationLocationId);
        }

        private static void UpsertDefinition(
            LocationSceneController controller,
            LocationDefinition definition)
        {
            LocationTemplateBuilder.UpsertDefinition(controller, definition);
        }

        private static void EnsureArtMaskImportSettings(string artPath)
        {
            if (AssetImporter.GetAtPath(artPath) is not TextureImporter importer ||
                importer.alphaSource == TextureImporterAlphaSource.FromGrayScale)
            {
                return;
            }

            importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            importer.SaveAndReimport();
        }

        private static void EnsureBackgroundImportSettings()
        {
            if (AssetImporter.GetAtPath(BackgroundPath) is not TextureImporter importer ||
                importer.npotScale == TextureImporterNPOTScale.None)
            {
                return;
            }

            importer.npotScale = TextureImporterNPOTScale.None;
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
