#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class WhisperingForestLocationInstaller
    {
        private const string MainScenePath = "Assets/StackCraft/Scenes/Main.unity";
        private const string DefinitionPath =
            "Assets/StackCraft/Resources/Locations/Location_WhisperingForest.asset";
        private const string BackgroundPath =
            "Assets/CardColony/Art/Backgrounds/WhisperingForestBackground_v2.png";
        private const string VillagerPath =
            "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset";
        private const string RockPath =
            "Assets/StackCraft/Resources/Cards/Resources/Card_Rock.asset";
        private const string BerryBushPath =
            "Assets/StackCraft/Resources/Cards/Resources/Card_BerryBush.asset";
        private const string TreePath =
            "Assets/StackCraft/Resources/Cards/Resources/Card_Tree.asset";
        private const string SlimePath =
            "Assets/StackCraft/Resources/Cards/Mobs/Card_Slime.asset";
        private const string GoblinPath =
            "Assets/StackCraft/Resources/Cards/Mobs/Card_Goblin.asset";

        [MenuItem("Tools/Card Colony/Install Whispering Forest")]
        public static void Install()
        {
            ConfigureBackgroundImporter();
            LocationDefinition definition = CreateOrUpdateDefinition();
            EnableWorldMapEntry();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = definition;
            Debug.Log("Whispering Forest location installed.");
        }

        private static LocationDefinition CreateOrUpdateDefinition()
        {
            return LocationTemplateBuilder.CreateOrUpdate(
                DefinitionPath,
                new LocationTemplate
                {
                    Id = "whispering-forest",
                    DisplayName = "低语森林",
                    BackgroundTexture = LoadRequired<Texture2D>(BackgroundPath),
                    MapSize = new Vector2(18.4f, 10.35f),
                    CameraMinDistance = 3f,
                    CameraMaxDistance = 24f,
                    CameraInitialDistance = 7f,
                    CameraZoomSpeed = 3f,
                    ExpandedPartyMemberDefinition =
                        LoadRequired<CardDefinition>(VillagerPath),
                    PartySpawnPosition = new Vector3(-6.7f, 0f, -3.7f),
                    PartyMemberSpacing = 0.9f,
                    RandomizeCardsOnEntry = true,
                    RandomSpawnAreaCenter = new Vector2(-0.8f, -0.3f),
                    RandomSpawnAreaSize = new Vector2(13.2f, 5.4f),
                    RandomSpawnMinSpacing = 1.05f,
                    RandomSpawnPartyClearance = 2.1f,
                    RandomCardSpawns = new[]
                    {
                        new LocationTemplateRandomSpawn(
                            LoadRequired<CardDefinition>(RockPath),
                            2,
                            4),
                        new LocationTemplateRandomSpawn(
                            LoadRequired<CardDefinition>(BerryBushPath),
                            2,
                            4),
                        new LocationTemplateRandomSpawn(
                            LoadRequired<CardDefinition>(TreePath),
                            3,
                            6),
                        new LocationTemplateRandomSpawn(
                            LoadRequired<CardDefinition>(SlimePath),
                            1,
                            3),
                        new LocationTemplateRandomSpawn(
                            LoadRequired<CardDefinition>(GoblinPath),
                            0,
                            2)
                    }
                });
        }

        private static void ConfigureBackgroundImporter()
        {
            if (AssetImporter.GetAtPath(BackgroundPath) is not TextureImporter importer)
                throw new InvalidOperationException(
                    $"Whispering Forest background is missing: {BackgroundPath}");

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
            SerializedProperty forest = null;
            for (int index = 0; index < details.arraySize; index++)
            {
                SerializedProperty candidate = details.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("locationId").stringValue ==
                    "whispering-forest")
                {
                    forest = candidate;
                    break;
                }
            }

            if (forest == null)
            {
                throw new InvalidOperationException(
                    "Main scene does not define the whispering-forest world-map card.");
            }

            forest.FindPropertyRelative("localMapImplemented").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
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
