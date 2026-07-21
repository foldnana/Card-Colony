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
        private const string BackgroundPath =
            "Assets/CardColony/Art/Backgrounds/RiverbendVillageBackground_v3.png";

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

            Canvas canvas = Object.FindObjectsOfType<Canvas>(true)
                .OrderByDescending(item => item.transform.childCount)
                .First();
            Button returnButton = CreateReturnButton(canvas.transform);
            TMP_Text titleLabel = CreateLocationTitle(canvas.transform);

            var controllerObject = new SerializedObject(controller);
            SerializedProperty definitions = controllerObject.FindProperty("locationDefinitions");
            definitions.ClearArray();
            definitions.InsertArrayElementAtIndex(0);
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = riverbend;
            controllerObject.FindProperty("returnButton").objectReferenceValue = returnButton;
            controllerObject.FindProperty("locationTitleLabel").objectReferenceValue = titleLabel;
            controllerObject.FindProperty("backgroundShader").objectReferenceValue = Shader.Find("Unlit/Texture");
            controllerObject.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LocationScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static LocationDefinition CreateOrUpdateDefinition()
        {
            LocationDefinition definition = AssetDatabase.LoadAssetAtPath<LocationDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<LocationDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = "riverbend";
            serialized.FindProperty("displayName").stringValue = "河湾村";
            serialized.FindProperty("backgroundTexture").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            serialized.FindProperty("mapSize").vector2Value = new Vector2(46f, 25.875f);
            serialized.FindProperty("cameraMinDistance").floatValue = 5f;
            serialized.FindProperty("cameraMaxDistance").floatValue = 52f;
            serialized.FindProperty("cameraInitialDistance").floatValue = 22f;
            serialized.FindProperty("cameraZoomSpeed").floatValue = 3f;
            serialized.FindProperty("expandedPartyMemberDefinition").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardDefinition>(VillagerPath);
            serialized.FindProperty("partySpawnPosition").vector3Value = new Vector3(0f, 0f, -1.2f);
            serialized.FindProperty("partyMemberSpacing").floatValue = 0.9f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
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
