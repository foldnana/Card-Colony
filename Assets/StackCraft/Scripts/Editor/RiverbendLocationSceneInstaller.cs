#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class RiverbendLocationSceneInstaller
    {
        private const string SourceScenePath = "Assets/StackCraft/Scenes/Island.unity";
        private const string LocationScenePath = "Assets/StackCraft/Scenes/Location.unity";
        private const string DefinitionPath = "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset";
        private const string VillagerPath = "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset";
        private const string RiverbendCardsFolder =
            "Assets/StackCraft/Resources/Cards/Locations/Riverbend";
        private const string BackgroundPath =
            "Assets/CardColony/Art/Backgrounds/RiverbendVillageBackground_v3.png";
        private const string StructureBasePath =
            "Assets/CardColony/Art/CardBases/Riverbend/Riverbend_StructureBase.png";
        private const string CharacterBasePath =
            "Assets/CardColony/Art/CardBases/Riverbend/Riverbend_CharacterBase.png";

        private sealed class InitialCardSpec
        {
            public string AssetName;
            public string Id;
            public string DisplayName;
            public string Description;
            public string ArtPath;
            public CardCategory Category;
            public Vector3 Position;
        }

        private static readonly InitialCardSpec[] InitialCards =
        {
            new()
            {
                AssetName = "Card_Riverbend_Market",
                Id = "riverbend-market",
                DisplayName = "市场",
                Description = "河湾村交换食物与日用品的露天市场。",
                ArtPath = "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_Market.png",
                Category = CardCategory.Structure,
                Position = new Vector3(0f, 0f, 2.8f)
            },
            new()
            {
                AssetName = "Card_Riverbend_BlacksmithShop",
                Id = "riverbend-blacksmith-shop",
                DisplayName = "铁匠铺",
                Description = "传来锤击声与炉火热浪的铁匠铺。",
                ArtPath = "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_BlacksmithShop.png",
                Category = CardCategory.Structure,
                Position = new Vector3(-4.4f, 0f, 1f)
            },
            new()
            {
                AssetName = "Card_Riverbend_Inn",
                Id = "riverbend-inn",
                DisplayName = "旅馆",
                Description = "旅行者可以落脚休息的河畔旅馆。",
                ArtPath = "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_Inn.png",
                Category = CardCategory.Structure,
                Position = new Vector3(4.4f, 0f, 1f)
            },
            new()
            {
                AssetName = "Card_Riverbend_VillageChief",
                Id = "riverbend-village-chief",
                DisplayName = "村长",
                Description = "熟悉河湾村与周边道路的村长。",
                ArtPath = "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_VillageChief.png",
                Category = CardCategory.Character,
                Position = new Vector3(-1f, 0f, -0.6f)
            },
            new()
            {
                AssetName = "Card_Riverbend_Blacksmith",
                Id = "riverbend-blacksmith",
                DisplayName = "铁匠",
                Description = "负责打造和修理装备的铁匠。",
                ArtPath = "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_Blacksmith.png",
                Category = CardCategory.Character,
                Position = new Vector3(3.2f, 0f, -1.6f)
            },
            new()
            {
                AssetName = "Card_Riverbend_Grocer",
                Id = "riverbend-grocer",
                DisplayName = "杂货商",
                Description = "经营旅行补给与日常杂货的商人。",
                ArtPath = "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_Grocer.png",
                Category = CardCategory.Character,
                Position = new Vector3(-3.2f, 0f, -1.6f)
            },
            new()
            {
                AssetName = "Card_Riverbend_Apothecary",
                Id = "riverbend-apothecary",
                DisplayName = "药师",
                Description = "了解草药与药剂配制方法的药师。",
                ArtPath = "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_Apothecary.png",
                Category = CardCategory.Character,
                Position = new Vector3(3.2f, 0f, -3.6f)
            }
        };

        [MenuItem("Tools/Card Colony/Install Riverbend Location Scene")]
        public static void Install()
        {
            EnsureFolder("Assets/StackCraft/Resources/Locations");
            LocationDefinition riverbend = CreateOrUpdateDefinition();

            Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, LocationScenePath, saveAsCopy: true);
            scene = EditorSceneManager.GetActiveScene();

            CardManager cardManager = Object.FindObjectOfType<CardManager>(true);
            var cardManagerObject = new SerializedObject(cardManager);
            cardManagerObject.FindProperty("defaultSpawnCards").ClearArray();
            cardManagerObject.ApplyModifiedPropertiesWithoutUndo();

            LocationSceneController controller = cardManager.GetComponent<LocationSceneController>();
            if (controller == null)
                controller = cardManager.gameObject.AddComponent<LocationSceneController>();
            LocationTemplateBuilder.UpsertDefinition(controller, riverbend);

            Canvas canvas = Object.FindObjectsOfType<Canvas>(true)
                .OrderByDescending(item => item.transform.childCount)
                .First();
            Button returnButton = CreateReturnButton(canvas.transform);
            TMP_Text titleLabel = CreateLocationTitle(canvas.transform);

            var controllerObject = new SerializedObject(controller);
            controllerObject.FindProperty("returnButton").objectReferenceValue = returnButton;
            controllerObject.FindProperty("locationTitleLabel").objectReferenceValue = titleLabel;
            controllerObject.FindProperty("backgroundShader").objectReferenceValue = Shader.Find("Unlit/Texture");
            controllerObject.ApplyModifiedPropertiesWithoutUndo();

            DialogueUiInstaller.InstallIntoScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LocationScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Card Colony/Install Riverbend Initial Cards")]
        public static void InstallInitialCards()
        {
            EnsureFolder("Assets/StackCraft/Resources/Locations");
            CreateOrUpdateDefinition();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static LocationDefinition CreateOrUpdateDefinition()
        {
            EnsureFolder(RiverbendCardsFolder);
            var spawns = new List<LocationTemplateSpawn>();

            foreach (InitialCardSpec spec in InitialCards)
            {
                CardDefinition card = CreateOrUpdateCard(spec);
                spawns.Add(new LocationTemplateSpawn(card, spec.Position));
            }

            return LocationTemplateBuilder.CreateOrUpdate(
                DefinitionPath,
                new LocationTemplate
                {
                    Id = "riverbend",
                    DisplayName = "河湾村",
                    BackgroundTexture =
                        AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath),
                    MapSize = new Vector2(18.4f, 10.35f),
                    CameraMinDistance = 3f,
                    CameraMaxDistance = 24f,
                    CameraInitialDistance = 7f,
                    CameraZoomSpeed = 3f,
                    ExpandedPartyMemberDefinition =
                        AssetDatabase.LoadAssetAtPath<CardDefinition>(VillagerPath),
                    PartySpawnPosition = new Vector3(0f, 0f, -0.48f),
                    PartyMemberSpacing = 0.9f,
                    InitialCardSpawns = spawns
                });
        }

        private static CardDefinition CreateOrUpdateCard(InitialCardSpec spec)
        {
            EnsureArtMaskImportSettings(spec.ArtPath);
            string path = $"{RiverbendCardsFolder}/{spec.AssetName}.asset";
            CardDefinition card = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardDefinition>();
                AssetDatabase.CreateAsset(card, path);
            }

            var serialized = new SerializedObject(card);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("displayName").stringValue = spec.DisplayName;
            serialized.FindProperty("description").stringValue = spec.Description;
            serialized.FindProperty("artTexture").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(spec.ArtPath);
            serialized.FindProperty("baseTextureOverride").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    spec.Category == CardCategory.Structure ? StructureBasePath : CharacterBasePath);
            serialized.FindProperty("category").enumValueIndex = (int)spec.Category;
            serialized.FindProperty("faction").enumValueIndex = (int)CardFaction.Neutral;
            serialized.FindProperty("isLocationStatic").boolValue = true;
            bool isNpc = spec.Category == CardCategory.Character;
            serialized.FindProperty("playerDraggable").boolValue = false;
            serialized.FindProperty("ambientNpcAiEnabled").boolValue = isNpc;
            serialized.FindProperty("ambientWanderRadius").floatValue = 1f;
            serialized.FindProperty("ambientMoveSpeed").floatValue = 0.5f;
            serialized.FindProperty("ambientIdleRange").vector2Value = new Vector2(2f, 4f);
            ConfigureBasicDialogue(serialized, spec.Id, isNpc);
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

        private static void ConfigureBasicDialogue(
            SerializedObject serialized,
            string cardId,
            bool isNpc)
        {
            serialized.FindProperty("dialogueEnabled").boolValue = isNpc;
            if (!isNpc)
                return;

            (string opening, string reply, string response) dialogue = cardId switch
            {
                "riverbend-village-chief" => (
                    "年轻人，你们似乎不是本地人。先去杂货商那里看看吧。",
                    "我该去哪里？",
                    "沿着村里的小路往西走，杂货商会为旅途准备补给。"),
                "riverbend-blacksmith" => (
                    "要修装备就得有好材料，市场偶尔能淘到矿石。",
                    "哪里能找到矿石？",
                    "旧矿洞还能挖到，只是路上不太安全。"),
                "riverbend-grocer" => (
                    "出远门前先把口粮备足，空着肚子可走不远。",
                    "有什么推荐？",
                    "面包耐放，草药也该带上一些。"),
                _ => (
                    "河边和林地都能找到药草，别把它们当普通野草。",
                    "药草有什么用？",
                    "带两份药草来，我可以教你调制简单的治疗药水。")
            };

            serialized.FindProperty("dialogueOpeningText").stringValue = dialogue.opening;
            serialized.FindProperty("dialogueReplyText").stringValue = dialogue.reply;
            serialized.FindProperty("dialogueResponseText").stringValue = dialogue.response;
        }

        private static void EnsureArtMaskImportSettings(string artPath)
        {
            if (AssetImporter.GetAtPath(artPath) is not TextureImporter importer ||
                importer.alphaSource == TextureImporterAlphaSource.FromGrayScale)
                return;

            importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            importer.SaveAndReimport();
        }

        private static Button CreateReturnButton(Transform canvas)
        {
            Transform existing = FindDescendant(canvas, "ReturnToWorldMapButton");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            Button source = FindDescendant(canvas, "EnterLocationButton")?.GetComponent<Button>();
            Button button;
            if (source != null)
                button = Object.Instantiate(source, canvas);
            else
                button = CreateFallbackButton(canvas);

            button.name = "ReturnToWorldMapButton";
            button.gameObject.SetActive(true);
            button.interactable = true;
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 28f);
            rect.sizeDelta = new Vector2(260f, 66f);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = "返回世界地图";
            return button;
        }

        private static Button CreateFallbackButton(Transform parent)
        {
            var gameObject = new GameObject(
                "ReturnToWorldMapButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            gameObject.transform.SetParent(parent, false);
            gameObject.GetComponent<Image>().color = new Color(0.08f, 0.48f, 0.68f, 0.96f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(gameObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 28f;
            label.color = Color.white;
            return gameObject.GetComponent<Button>();
        }

        private static TMP_Text CreateLocationTitle(Transform canvas)
        {
            Transform existing = FindDescendant(canvas, "LocalLocationTitle");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            TMP_Text source = FindDescendant(canvas, "LocationTitle")?.GetComponent<TMP_Text>();
            TMP_Text title;
            if (source != null)
                title = Object.Instantiate(source, canvas);
            else
            {
                var titleObject = new GameObject("LocalLocationTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleObject.transform.SetParent(canvas, false);
                title = titleObject.GetComponent<TextMeshProUGUI>();
                title.alignment = TextAlignmentOptions.Center;
                title.fontSize = 34f;
                title.color = Color.white;
            }

            title.name = "LocalLocationTitle";
            title.text = "河湾村";
            title.gameObject.SetActive(true);
            RectTransform rect = title.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -22f);
            rect.sizeDelta = new Vector2(360f, 56f);
            return title;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == name);
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != LocationScenePath))
                scenes.Add(new EditorBuildSettingsScene(LocationScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
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
