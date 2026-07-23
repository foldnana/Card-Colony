#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CryingSnow.StackCraft.EditorTools
{
    public readonly struct LocationTemplateSpawn
    {
        public LocationTemplateSpawn(CardDefinition definition, Vector3 position)
        {
            Definition = definition;
            Position = position;
        }

        public CardDefinition Definition { get; }
        public Vector3 Position { get; }
    }

    public readonly struct LocationTemplateEntrance
    {
        public LocationTemplateEntrance(
            CardDefinition sourceCardDefinition,
            string destinationLocationId)
        {
            SourceCardDefinition = sourceCardDefinition;
            DestinationLocationId = destinationLocationId;
        }

        public CardDefinition SourceCardDefinition { get; }
        public string DestinationLocationId { get; }
    }

    public readonly struct LocationTemplateRandomSpawn
    {
        public LocationTemplateRandomSpawn(
            CardDefinition definition,
            int minimumCount,
            int maximumCount)
        {
            Definition = definition;
            MinimumCount = minimumCount;
            MaximumCount = maximumCount;
        }

        public CardDefinition Definition { get; }
        public int MinimumCount { get; }
        public int MaximumCount { get; }
    }

    [Serializable]
    public sealed class LocationTemplate
    {
        public string Id = "new-location";
        public string DisplayName = "新地点";
        public Texture2D BackgroundTexture;
        public Vector2 MapSize = new(18.4f, 10.35f);
        public float CameraMinDistance = 3f;
        public float CameraMaxDistance = 20f;
        public float CameraInitialDistance = 7f;
        public float CameraZoomSpeed = 3f;
        public CardDefinition ExpandedPartyMemberDefinition;
        public Vector3 PartySpawnPosition = new(0f, 0f, -1f);
        public float PartyMemberSpacing = 0.9f;
        public IReadOnlyList<LocationTemplateSpawn> InitialCardSpawns =
            Array.Empty<LocationTemplateSpawn>();
        public IReadOnlyList<LocationTemplateEntrance> Entrances =
            Array.Empty<LocationTemplateEntrance>();
        public bool RandomizeCardsOnEntry;
        public Vector2 RandomSpawnAreaCenter;
        public Vector2 RandomSpawnAreaSize = new(14f, 7f);
        public float RandomSpawnMinSpacing = 0.8f;
        public float RandomSpawnPartyClearance = 2f;
        public IReadOnlyList<LocationTemplateRandomSpawn> RandomCardSpawns =
            Array.Empty<LocationTemplateRandomSpawn>();
    }

    public static class LocationTemplateBuilder
    {
        private const string DefaultLocationFolder =
            "Assets/StackCraft/Resources/Locations";

        public static LocationDefinition CreateOrUpdate(
            string assetPath,
            LocationTemplate template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Location assets must use an Assets/.../*.asset path.",
                    nameof(assetPath));
            }
            if (string.IsNullOrWhiteSpace(template.Id))
                throw new ArgumentException("Location id cannot be empty.", nameof(template));

            EnsureFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            LocationDefinition definition =
                AssetDatabase.LoadAssetAtPath<LocationDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<LocationDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = template.Id.Trim();
            serialized.FindProperty("displayName").stringValue =
                string.IsNullOrWhiteSpace(template.DisplayName)
                    ? template.Id.Trim()
                    : template.DisplayName.Trim();
            serialized.FindProperty("backgroundTexture").objectReferenceValue =
                template.BackgroundTexture;
            serialized.FindProperty("mapSize").vector2Value = template.MapSize;
            serialized.FindProperty("cameraMinDistance").floatValue =
                template.CameraMinDistance;
            serialized.FindProperty("cameraMaxDistance").floatValue =
                template.CameraMaxDistance;
            serialized.FindProperty("cameraInitialDistance").floatValue =
                template.CameraInitialDistance;
            serialized.FindProperty("cameraZoomSpeed").floatValue =
                template.CameraZoomSpeed;
            serialized.FindProperty("expandedPartyMemberDefinition").objectReferenceValue =
                template.ExpandedPartyMemberDefinition;
            serialized.FindProperty("partySpawnPosition").vector3Value =
                template.PartySpawnPosition;
            serialized.FindProperty("partyMemberSpacing").floatValue =
                template.PartyMemberSpacing;

            ReplaceSpawns(
                serialized.FindProperty("initialCardSpawns"),
                template.InitialCardSpawns);
            UpsertEntrances(
                serialized.FindProperty("entrances"),
                template.Entrances);
            serialized.FindProperty("randomizeCardsOnEntry").boolValue =
                template.RandomizeCardsOnEntry;
            serialized.FindProperty("randomSpawnAreaCenter").vector2Value =
                template.RandomSpawnAreaCenter;
            serialized.FindProperty("randomSpawnAreaSize").vector2Value =
                template.RandomSpawnAreaSize;
            serialized.FindProperty("randomSpawnMinSpacing").floatValue =
                template.RandomSpawnMinSpacing;
            serialized.FindProperty("randomSpawnPartyClearance").floatValue =
                template.RandomSpawnPartyClearance;
            ReplaceRandomSpawns(
                serialized.FindProperty("randomCardSpawns"),
                template.RandomCardSpawns);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        public static void UpsertDefinition(
            LocationSceneController controller,
            LocationDefinition definition)
        {
            if (controller == null || definition == null)
                return;

            var serialized = new SerializedObject(controller);
            SerializedProperty definitions =
                serialized.FindProperty("locationDefinitions");
            for (int index = 0; index < definitions.arraySize; index++)
            {
                LocationDefinition existing =
                    definitions.GetArrayElementAtIndex(index).objectReferenceValue
                        as LocationDefinition;
                if (existing == definition)
                {
                    return;
                }
                if (existing != null && existing.Id == definition.Id)
                    return;
            }

            int newIndex = definitions.arraySize;
            definitions.InsertArrayElementAtIndex(newIndex);
            definitions.GetArrayElementAtIndex(newIndex).objectReferenceValue =
                definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        public static void UpsertEntrance(
            LocationDefinition location,
            CardDefinition sourceCard,
            string destinationLocationId)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));
            if (sourceCard == null)
                throw new ArgumentNullException(nameof(sourceCard));
            if (string.IsNullOrWhiteSpace(destinationLocationId))
            {
                throw new ArgumentException(
                    "Destination location id cannot be empty.",
                    nameof(destinationLocationId));
            }

            var serialized = new SerializedObject(location);
            SerializedProperty entrances = serialized.FindProperty("entrances");
            SerializedProperty target = null;
            for (int index = 0; index < entrances.arraySize; index++)
            {
                SerializedProperty candidate = entrances.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("sourceCardDefinition")
                        .objectReferenceValue == sourceCard)
                {
                    target = candidate;
                    break;
                }
            }

            if (target == null)
            {
                int index = entrances.arraySize;
                entrances.InsertArrayElementAtIndex(index);
                target = entrances.GetArrayElementAtIndex(index);
            }

            target.FindPropertyRelative("sourceCardDefinition").objectReferenceValue =
                sourceCard;
            target.FindPropertyRelative("destinationLocationId").stringValue =
                destinationLocationId.Trim();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(location);
        }

        public static string CreateDefaultLocationId(string assetPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            const string prefix = "Location_";
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                fileName = fileName.Substring(prefix.Length);

            var characters = new List<char>(fileName.Length);
            bool previousWasSeparator = false;
            foreach (char character in fileName)
            {
                if (char.IsLetterOrDigit(character))
                {
                    characters.Add(char.ToLowerInvariant(character));
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator && characters.Count > 0)
                {
                    characters.Add('-');
                    previousWasSeparator = true;
                }
            }

            string id = new string(characters.ToArray()).Trim('-');
            return string.IsNullOrWhiteSpace(id) ? "new-location" : id;
        }

        [MenuItem("Tools/Card Colony/Create Reusable Location")]
        private static void CreateReusableLocation()
        {
            EnsureFolder(DefaultLocationFolder);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{DefaultLocationFolder}/Location_New.asset");
            LocationDefinition definition = CreateOrUpdate(
                assetPath,
                new LocationTemplate
                {
                    Id = CreateDefaultLocationId(assetPath)
                });
            AssetDatabase.SaveAssets();
            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
        }

        private static void ReplaceSpawns(
            SerializedProperty target,
            IReadOnlyList<LocationTemplateSpawn> spawns)
        {
            target.ClearArray();
            if (spawns == null)
                return;

            for (int index = 0; index < spawns.Count; index++)
            {
                LocationTemplateSpawn spawn = spawns[index];
                if (spawn.Definition == null)
                    continue;

                int targetIndex = target.arraySize;
                target.InsertArrayElementAtIndex(targetIndex);
                SerializedProperty element = target.GetArrayElementAtIndex(targetIndex);
                element.FindPropertyRelative("definition").objectReferenceValue =
                    spawn.Definition;
                element.FindPropertyRelative("position").vector3Value =
                    spawn.Position;
            }
        }

        private static void UpsertEntrances(
            SerializedProperty target,
            IReadOnlyList<LocationTemplateEntrance> entrances)
        {
            if (entrances == null)
                return;

            for (int index = 0; index < entrances.Count; index++)
            {
                LocationTemplateEntrance entrance = entrances[index];
                if (entrance.SourceCardDefinition == null ||
                    string.IsNullOrWhiteSpace(entrance.DestinationLocationId))
                {
                    continue;
                }

                SerializedProperty element = null;
                for (int existingIndex = 0;
                     existingIndex < target.arraySize;
                     existingIndex++)
                {
                    SerializedProperty existing =
                        target.GetArrayElementAtIndex(existingIndex);
                    if (existing.FindPropertyRelative("sourceCardDefinition")
                            .objectReferenceValue == entrance.SourceCardDefinition)
                    {
                        element = existing;
                        break;
                    }
                }

                if (element == null)
                {
                    int targetIndex = target.arraySize;
                    target.InsertArrayElementAtIndex(targetIndex);
                    element = target.GetArrayElementAtIndex(targetIndex);
                }

                element.FindPropertyRelative("sourceCardDefinition")
                    .objectReferenceValue = entrance.SourceCardDefinition;
                element.FindPropertyRelative("destinationLocationId").stringValue =
                    entrance.DestinationLocationId.Trim();
            }
        }

        private static void ReplaceRandomSpawns(
            SerializedProperty target,
            IReadOnlyList<LocationTemplateRandomSpawn> spawns)
        {
            target.ClearArray();
            if (spawns == null)
                return;

            for (int index = 0; index < spawns.Count; index++)
            {
                LocationTemplateRandomSpawn spawn = spawns[index];
                if (spawn.Definition == null)
                    continue;

                int targetIndex = target.arraySize;
                target.InsertArrayElementAtIndex(targetIndex);
                SerializedProperty element = target.GetArrayElementAtIndex(targetIndex);
                element.FindPropertyRelative("definition").objectReferenceValue =
                    spawn.Definition;
                element.FindPropertyRelative("minimumCount").intValue =
                    Mathf.Max(0, spawn.MinimumCount);
                element.FindPropertyRelative("maximumCount").intValue =
                    Mathf.Max(spawn.MinimumCount, spawn.MaximumCount);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

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
