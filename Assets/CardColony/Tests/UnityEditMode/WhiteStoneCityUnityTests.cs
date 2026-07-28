using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardColony.Tests
{
    public sealed class WhiteStoneCityUnityTests
    {
        private const string DefinitionPath =
            "Assets/StackCraft/Resources/Locations/Location_WhiteStoneCity.asset";
        private const string BackgroundPath =
            "Assets/CardColony/Art/Backgrounds/WhiteStoneCityBackground.png";

        [Test]
        public void WhiteStoneCity_UsesLargeReusableLocationDefinition()
        {
            Object city = AssetDatabase.LoadAssetAtPath<Object>(DefinitionPath);
            Assert.That(city, Is.Not.Null, "白石城需要使用通用 LocationDefinition 资产");

            var serialized = new SerializedObject(city);
            Assert.That(serialized.FindProperty("id").stringValue,
                Is.EqualTo("white-stone-city"));
            Assert.That(serialized.FindProperty("displayName").stringValue,
                Is.EqualTo("白石城"));
            Assert.That(
                AssetDatabase.GetAssetPath(
                    serialized.FindProperty("backgroundTexture").objectReferenceValue),
                Is.EqualTo(BackgroundPath));

            Vector2 size = serialized.FindProperty("mapSize").vector2Value;
            Assert.That(size.x, Is.GreaterThanOrEqualTo(23f),
                "白石城应比河湾村更宽，给更多城市卡牌留出空间");
            Assert.That(size.y, Is.GreaterThanOrEqualTo(12.9f),
                "白石城应比河湾村更深，支持多个功能街区");
            Assert.That(serialized.FindProperty("cameraMaxDistance").floatValue,
                Is.GreaterThanOrEqualTo(28f),
                "大地图需要允许玩家缩放到城市全景");
        }

        [Test]
        public void WhiteStoneCity_ContainsFiveFacilitiesAndEightFixedNpcs()
        {
            Object city = AssetDatabase.LoadAssetAtPath<Object>(DefinitionPath);
            Assert.That(city, Is.Not.Null);

            SerializedProperty spawns = new SerializedObject(city)
                .FindProperty("initialCardSpawns");
            var facilities = new List<Object>();
            var npcs = new List<Object>();

            for (int index = 0; index < spawns.arraySize; index++)
            {
                Object definition = spawns.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("definition").objectReferenceValue;
                Assert.That(definition, Is.Not.Null);
                var card = new SerializedObject(definition);
                SerializedProperty category = card.FindProperty("category");
                string categoryName =
                    category.enumNames[category.enumValueIndex];
                if (categoryName == "Structure")
                    facilities.Add(definition);
                else if (categoryName == "Character")
                    npcs.Add(definition);
            }

            Assert.That(facilities, Has.Count.EqualTo(5),
                "第一版白石城应清楚划分五个主要城市设施");
            Assert.That(npcs, Has.Count.EqualTo(8),
                "白石城应比河湾村拥有更多 NPC");

            foreach (Object npc in npcs)
            {
                var card = new SerializedObject(npc);
                Assert.That(card.FindProperty("isLocationStatic").boolValue, Is.True);
                Assert.That(card.FindProperty("playerDraggable").boolValue, Is.False);
                Assert.That(card.FindProperty("dialogueEnabled").boolValue, Is.True);
                Assert.That(card.FindProperty("dialogueOpeningText").stringValue,
                    Is.Not.Empty);
            }
        }

        [Test]
        public void WhiteStoneCity_UsesReadableDistrictSpacing()
        {
            Object city = AssetDatabase.LoadAssetAtPath<Object>(DefinitionPath);
            Assert.That(city, Is.Not.Null);

            SerializedProperty spawns = new SerializedObject(city)
                .FindProperty("initialCardSpawns");
            var positions = new List<Vector3>();
            for (int index = 0; index < spawns.arraySize; index++)
            {
                positions.Add(spawns.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("position").vector3Value);
            }

            for (int first = 0; first < positions.Count; first++)
            {
                for (int second = first + 1; second < positions.Count; second++)
                {
                    Assert.That(
                        Vector2.Distance(
                            new Vector2(positions[first].x, positions[first].z),
                            new Vector2(positions[second].x, positions[second].z)),
                        Is.GreaterThanOrEqualTo(1.25f),
                        $"白石城初始卡牌 {first} 与 {second} 过于拥挤");
                }
            }
        }

        [Test]
        public void MainWorldMap_EnablesWhiteStoneCityLocalMap()
        {
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene mainScene = EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Main.unity",
                OpenSceneMode.Additive);
            try
            {
                MonoBehaviour bootstrap = mainScene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<MonoBehaviour>(true))
                    .First(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.WorldMapBootstrap");
                SerializedProperty details = new SerializedObject(bootstrap)
                    .FindProperty("locationDetails");

                SerializedProperty city = null;
                for (int index = 0; index < details.arraySize; index++)
                {
                    SerializedProperty candidate =
                        details.GetArrayElementAtIndex(index);
                    if (candidate.FindPropertyRelative("locationId").stringValue ==
                        "white-stone-city")
                    {
                        city = candidate;
                        break;
                    }
                }

                Assert.That(city, Is.Not.Null);
                Assert.That(
                    city.FindPropertyRelative("localMapImplemented").boolValue,
                    Is.True,
                    "完成白石城地图后，世界地图入口必须开放");
            }
            finally
            {
                EditorSceneManager.CloseScene(mainScene, removeScene: true);
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);
            }
        }
    }
}
