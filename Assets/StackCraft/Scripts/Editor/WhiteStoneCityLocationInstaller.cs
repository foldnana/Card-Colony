#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class WhiteStoneCityLocationInstaller
    {
        private const string MainScenePath = "Assets/StackCraft/Scenes/Main.unity";
        private const string DefinitionPath =
            "Assets/StackCraft/Resources/Locations/Location_WhiteStoneCity.asset";
        private const string CardsFolder =
            "Assets/StackCraft/Resources/Cards/Locations/WhiteStoneCity";
        private const string BackgroundPath =
            "Assets/CardColony/Art/Backgrounds/WhiteStoneCityBackground.png";
        private const string VillagerPath =
            "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset";
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
            public string ArtPath;
            public CardCategory Category;
            public Vector3 Position;
            public string Opening;
            public string Reply;
            public string Response;
        }

        private static readonly CardSpec[] InitialCards =
        {
            Facility(
                "Card_WhiteStone_CityHall",
                "white-stone-city-hall",
                "白石市政厅",
                "白石城处理政务、委托和通行事务的核心建筑。",
                "Assets/CardColony/Art/CardArts/Inn/Reception.png",
                new Vector3(0f, 0f, 4.4f)),
            Facility(
                "Card_WhiteStone_Market",
                "white-stone-market",
                "白石市场",
                "来自各条商路的货物在这里集中交易。",
                "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_Market.png",
                new Vector3(-7f, 0f, 1.8f)),
            Facility(
                "Card_WhiteStone_Smithy",
                "white-stone-smithy",
                "白石锻造坊",
                "以本地优质石料和矿石闻名的城市工坊。",
                "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_BlacksmithShop.png",
                new Vector3(7f, 0f, 1.8f)),
            Facility(
                "Card_WhiteStone_Inn",
                "white-stone-inn",
                "石冠旅馆",
                "靠近南门、接待商人与冒险者的大型旅馆。",
                "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_Inn.png",
                new Vector3(-7f, 0f, -2.4f)),
            Facility(
                "Card_WhiteStone_Guild",
                "white-stone-guild",
                "冒险者公会",
                "发布城市与周边地区委托的冒险者据点。",
                "Assets/CardColony/Art/CardArts/Inn/Table.png",
                new Vector3(7f, 0f, -2.4f)),

            Npc(
                "Card_WhiteStone_GuardCaptain",
                "white-stone-guard-captain",
                "卫队长",
                "负责白石城城防与城内秩序的卫队长。",
                "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_VillageChief.png",
                new Vector3(0f, 0f, 2.35f),
                "白石城欢迎守规矩的旅人。第一次来，就先熟悉中央广场吧。",
                "城里有哪些重要地点？",
                "西边是市场和旅馆，东边是工坊和公会，北边则是市政厅。"),
            Npc(
                "Card_WhiteStone_Merchant",
                "white-stone-merchant",
                "行会商人",
                "熟悉白石城货价和商路消息的行会商人。",
                "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_Grocer.png",
                new Vector3(-5.15f, 0f, 0.25f),
                "河湾村来的货物最近很抢手，你若有余货，可以来市场问价。",
                "这里最缺什么货？",
                "食物总有人要，真正值钱的则是矿石、药材和远方的稀罕物。"),
            Npc(
                "Card_WhiteStone_Blacksmith",
                "white-stone-blacksmith",
                "锻造师",
                "能够打造和修理进阶装备的城市锻造师。",
                "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_Blacksmith.png",
                new Vector3(5.15f, 0f, 0.25f),
                "白石城的炉火从不熄灭，不过好装备要配得上好材料。",
                "我该去哪里找材料？",
                "旧矿洞里还有矿石，但最好先在公会找几个可靠的同伴。"),
            Npc(
                "Card_WhiteStone_Innkeeper",
                "white-stone-innkeeper",
                "旅馆老板",
                "经营石冠旅馆、掌握来往旅人消息的老板。",
                "Assets/CardColony/Art/CardArts/Inn/Innkeeper.png",
                new Vector3(-5.45f, 0f, -3.55f),
                "房间、热汤和消息，这三样在石冠旅馆都能找到。",
                "最近有什么消息？",
                "北边来的商队少了，据说旧矿洞附近又出现了怪物。"),
            Npc(
                "Card_WhiteStone_GuildClerk",
                "white-stone-guild-clerk",
                "公会接待员",
                "登记冒险者、整理委托与发放报酬的接待员。",
                "Assets/CardColony/Art/CardArts/Inn/Waiter.png",
                new Vector3(5.45f, 0f, -3.55f),
                "新面孔？先完成登记，之后就能查看适合你们的委托。",
                "现在有什么简单委托？",
                "可以从护送、采集和城外巡查开始，别急着接讨伐任务。"),
            Npc(
                "Card_WhiteStone_Apothecary",
                "white-stone-apothecary",
                "药剂师",
                "收购药材并调制旅行药剂的城市药剂师。",
                "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_Apothecary.png",
                new Vector3(-2.8f, 0f, 0.8f),
                "森林里的药草品质不错，只是采集时要小心那些会动的东西。",
                "你会收购药材吗？",
                "当然。保存完整的药草价格更好，受潮的只能拿来做普通药膏。"),
            Npc(
                "Card_WhiteStone_QuestOfficer",
                "white-stone-quest-officer",
                "委托官",
                "负责整理城内公共委托和外来报告的办事员。",
                "Assets/CardColony/Art/CardArts/Riverbend/Riverbend_VillageChief.png",
                new Vector3(2.8f, 0f, 0.8f),
                "城市不会缺少工作，只会缺少愿意把事情办完的人。",
                "我能接哪些委托？",
                "先去公会登记。积累信誉后，市政厅会开放更重要的委托。"),
            Npc(
                "Card_WhiteStone_GateGuard",
                "white-stone-gate-guard",
                "南门守卫",
                "检查出入人员与货物的白石城守卫。",
                "Assets/CardColony/Art/CardArts/Inn/Waiter.png",
                new Vector3(0f, 0f, -3.55f),
                "进城后沿主路直走就是广场，别把危险物品带进居民区。",
                "离开城市要注意什么？",
                "城外夜间并不安全，准备食物和照明，最好结伴同行。")
        };

        [MenuItem("Tools/Card Colony/Install White Stone City")]
        public static void Install()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            SceneSetup[] previousSceneSetup =
                EditorSceneManager.GetSceneManagerSetup();
            try
            {
                ConfigureBackgroundImporter();
                EnsureFolder(CardsFolder);

                var spawns = new List<LocationTemplateSpawn>();
                foreach (CardSpec spec in InitialCards)
                {
                    spawns.Add(new LocationTemplateSpawn(
                        CreateOrUpdateCard(spec),
                        spec.Position));
                }

                LocationDefinition definition =
                    LocationTemplateBuilder.CreateOrUpdate(
                        DefinitionPath,
                        new LocationTemplate
                        {
                            Id = "white-stone-city",
                            DisplayName = "白石城",
                            BackgroundTexture =
                                LoadRequired<Texture2D>(BackgroundPath),
                            MapSize = new Vector2(23f, 12.94f),
                            CameraMinDistance = 3f,
                            CameraMaxDistance = 30f,
                            CameraInitialDistance = 9f,
                            CameraZoomSpeed = 3.8f,
                            ExpandedPartyMemberDefinition =
                                LoadRequired<CardDefinition>(VillagerPath),
                            PartySpawnPosition =
                                new Vector3(0f, 0f, -5.15f),
                            PartyMemberSpacing = 0.9f,
                            InitialCardSpawns = spawns
                        });

                EnableWorldMapEntry();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = definition;
                Debug.Log("White Stone City location installed.");
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
            }
        }

        private static CardDefinition CreateOrUpdateCard(CardSpec spec)
        {
            string path = $"{CardsFolder}/{spec.AssetName}.asset";
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
                LoadRequired<Texture2D>(spec.ArtPath);
            serialized.FindProperty("baseTextureOverride").objectReferenceValue =
                LoadRequired<Texture2D>(
                    spec.Category == CardCategory.Structure
                        ? StructureBasePath
                        : CharacterBasePath);
            serialized.FindProperty("category").enumValueIndex = (int)spec.Category;
            serialized.FindProperty("faction").enumValueIndex = (int)CardFaction.Neutral;
            serialized.FindProperty("isLocationStatic").boolValue = true;
            serialized.FindProperty("playerDraggable").boolValue = false;

            bool isNpc = spec.Category == CardCategory.Character;
            serialized.FindProperty("ambientNpcAiEnabled").boolValue = isNpc;
            serialized.FindProperty("ambientWanderRadius").floatValue = 0.85f;
            serialized.FindProperty("ambientMoveSpeed").floatValue = 0.45f;
            serialized.FindProperty("ambientIdleRange").vector2Value =
                new Vector2(2.5f, 5f);
            serialized.FindProperty("dialogueEnabled").boolValue = isNpc;
            serialized.FindProperty("dialogueOpeningText").stringValue =
                isNpc ? spec.Opening : string.Empty;
            serialized.FindProperty("dialogueReplyText").stringValue =
                isNpc ? spec.Reply : string.Empty;
            serialized.FindProperty("dialogueResponseText").stringValue =
                isNpc ? spec.Response : string.Empty;

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

        private static void ConfigureBackgroundImporter()
        {
            if (AssetImporter.GetAtPath(BackgroundPath) is not TextureImporter importer)
                throw new InvalidOperationException(
                    $"White Stone City background is missing: {BackgroundPath}");

            bool changed = false;
            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }
            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }
            if (importer.maxTextureSize < 2048)
            {
                importer.maxTextureSize = 2048;
                changed = true;
            }
            if (changed)
                importer.SaveAndReimport();
        }

        private static void EnableWorldMapEntry()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            WorldMapBootstrap bootstrap =
                UnityEngine.Object.FindObjectOfType<WorldMapBootstrap>(true);
            if (bootstrap == null)
                throw new InvalidOperationException("Main scene is missing WorldMapBootstrap.");

            var serialized = new SerializedObject(bootstrap);
            SerializedProperty details = serialized.FindProperty("locationDetails");
            SerializedProperty city = null;
            for (int index = 0; index < details.arraySize; index++)
            {
                SerializedProperty candidate = details.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("locationId").stringValue ==
                    "white-stone-city")
                {
                    city = candidate;
                    break;
                }
            }

            if (city == null)
                throw new InvalidOperationException(
                    "Main scene does not define the white-stone-city world-map card.");

            city.FindPropertyRelative("localMapImplemented").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static CardSpec Facility(
            string assetName,
            string id,
            string displayName,
            string description,
            string artPath,
            Vector3 position)
        {
            return new CardSpec
            {
                AssetName = assetName,
                Id = id,
                DisplayName = displayName,
                Description = description,
                ArtPath = artPath,
                Category = CardCategory.Structure,
                Position = position
            };
        }

        private static CardSpec Npc(
            string assetName,
            string id,
            string displayName,
            string description,
            string artPath,
            Vector3 position,
            string opening,
            string reply,
            string response)
        {
            return new CardSpec
            {
                AssetName = assetName,
                Id = id,
                DisplayName = displayName,
                Description = description,
                ArtPath = artPath,
                Category = CardCategory.Character,
                Position = position,
                Opening = opening,
                Reply = reply,
                Response = response
            };
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

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Required asset is missing: {path}");
        }
    }
}
#endif
