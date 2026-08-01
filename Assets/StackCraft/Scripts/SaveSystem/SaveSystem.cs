using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace CryingSnow.StackCraft
{
    public static class SaveSystem
    {
        /// <summary>
        /// Saves data to a file as JSON.
        /// </summary>
        public static void SaveData<T>(T data, string fileName)
        {
            string filePath = Path.Combine(Application.persistentDataPath, fileName + ".json");
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
                Directory.CreateDirectory(directoryPath);

            // Convert the data object to a JSON string
            // Formatting.Indented makes the file readable (good for debugging). 
            // Change to Formatting.None for a smaller file size in release.
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);

            string tempPath = filePath + ".tmp";
            string backupPath = filePath + ".bak";
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None))
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            if (!File.Exists(filePath))
            {
                File.Move(tempPath, filePath);
                return;
            }

            try
            {
                File.Replace(tempPath, filePath, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(filePath, backupPath, true);
                File.Delete(filePath);
                File.Move(tempPath, filePath);
            }
        }

        /// <summary>
        /// Loads data from a JSON file.
        /// </summary>
        public static T LoadData<T>(string fileName)
        {
            string filePath = Path.Combine(Application.persistentDataPath, fileName + ".json");

            string backupPath = filePath + ".bak";
            if (File.Exists(filePath))
            {
                try
                {
                    return DeserializeAndValidateFile<T>(filePath);
                }
                catch (SaveVersionTooNewException)
                {
                    throw;
                }
                catch (System.Exception exception)
                {
                    if (!File.Exists(backupPath))
                        throw;
                    Debug.LogWarning(
                        $"Primary save '{fileName}' is invalid; " +
                        $"restoring its backup. {exception.Message}");
                    return LoadBackupAndRepair<T>(filePath, backupPath);
                }
            }
            if (File.Exists(backupPath))
            {
                return LoadBackupAndRepair<T>(filePath, backupPath);
            }
            return default(T);
        }

        /// <summary>
        /// Loads all JSON files in the directory and returns a Dictionary of valid data.
        /// Key = File Name (without extension), Value = The Data Object.
        /// </summary>
        public static Dictionary<string, T> LoadAllValidData<T>()
        {
            Dictionary<string, T> validDataDict = new Dictionary<string, T>();
            string directoryPath = Application.persistentDataPath;

            // 1. Ensure the directory exists
            if (!Directory.Exists(directoryPath))
            {
                return validDataDict;
            }

            // Include backups so a missing or corrupt primary save still
            // appears in the title-screen save list.
            IEnumerable<string> filePaths = Directory
                .GetFiles(directoryPath, "*.json")
                .Concat(Directory.GetFiles(directoryPath, "*.json.bak"))
                .Select(path => path.EndsWith(
                        ".bak",
                        System.StringComparison.OrdinalIgnoreCase)
                    ? path.Substring(0, path.Length - 4)
                    : path)
                .Distinct(System.StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in filePaths)
            {
                try
                {
                    T data;
                    try
                    {
                        data = File.Exists(filePath)
                            ? DeserializeAndValidateFile<T>(filePath)
                            : throw new FileNotFoundException(
                                "Primary save is missing.",
                                filePath);
                    }
                    catch (SaveVersionTooNewException)
                    {
                        throw;
                    }
                    catch (System.Exception)
                    {
                        string backupPath = filePath + ".bak";
                        if (!File.Exists(backupPath))
                            throw;
                        data = LoadBackupAndRepair<T>(filePath, backupPath);
                    }

                    if (data != null)
                    {
                        // 4. Get the file name to use as the Key (e.g., "SaveSlot001")
                        string fileName = Path.GetFileNameWithoutExtension(filePath);

                        // Add to dictionary
                        validDataDict.Add(fileName, data);
                    }
                }
                catch (SaveVersionTooNewException exception)
                {
                    Debug.LogWarning(
                        $"Skipped incompatible save file at: {filePath}. " +
                        exception.Message);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Skipped invalid save file at: {filePath}. Error: {ex.Message}");
                }
            }

            return validDataDict;
        }

        /// <summary>
        /// Helper to delete a save file.
        /// </summary>
        public static void DeleteSave(string fileName)
        {
            string filePath = Path.Combine(Application.persistentDataPath, fileName + ".json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            string backupPath = filePath + ".bak";
            if (File.Exists(backupPath))
                File.Delete(backupPath);
            string tempPath = filePath + ".tmp";
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }

        private static T DeserializeFile<T>(string path)
        {
            string json = File.ReadAllText(path);
            T data = DeserializeVersioned<T>(json);
            if (data == null)
            {
                throw new JsonSerializationException(
                    $"Save file '{path}' contains no data.");
            }
            return data;
        }

        private static T DeserializeAndValidateFile<T>(string path)
        {
            T data = DeserializeFile<T>(path);
            if (data is GameData gameData)
            {
                WorldQuestDefinition[] definitions =
                    Resources.LoadAll<WorldQuestDefinition>("WorldQuests");
                WorldQuestValidationReport report =
                    WorldQuestDefinitionValidator.Validate(
                        (IEnumerable<WorldQuestDefinition>)definitions);
                if (!report.IsValid)
                {
                    throw new InvalidDataException(
                        "World Quest definitions are invalid: " +
                        string.Join(" | ", report.Errors.Cast<string>()));
                }
                new WorldQuestEngine(
                    gameData,
                    (IEnumerable<WorldQuestDefinition>)definitions)
                    .Initialize();
            }
            return data;
        }

        private static T LoadBackupAndRepair<T>(
            string primaryPath,
            string backupPath)
        {
            T data = DeserializeAndValidateFile<T>(backupPath);
            try
            {
                RepairPrimaryFromBackup(primaryPath, backupPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Backup save was loaded, but primary repair failed: " +
                    exception.Message);
            }
            return data;
        }

        private static void RepairPrimaryFromBackup(
            string primaryPath,
            string backupPath)
        {
            string repairPath = primaryPath + ".repair.tmp";
            try
            {
                using (var input = new FileStream(
                           backupPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read))
                using (var output = new FileStream(
                           repairPath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.None))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                }

                if (!File.Exists(primaryPath))
                {
                    File.Move(repairPath, primaryPath);
                }
                else
                {
                    try
                    {
                        File.Replace(repairPath, primaryPath, null, true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(repairPath, primaryPath, true);
                        File.Delete(repairPath);
                    }
                }
            }
            finally
            {
                if (File.Exists(repairPath))
                    File.Delete(repairPath);
            }
        }

        private static T DeserializeVersioned<T>(string json)
        {
            if (typeof(T) != typeof(GameData))
                return JsonConvert.DeserializeObject<T>(json);

            JObject root = JObject.Parse(json);
            int version = root.Value<int?>(
                nameof(GameData.WorldQuestStateVersion)) ?? 0;
            if (version > GameData.CurrentWorldQuestStateVersion)
            {
                throw new SaveVersionTooNewException(
                    "存档由更新版本创建，当前客户端不能降级读取。");
            }
            if (version >= 2)
                return root.ToObject<T>();

            // V1 and V2 use different numeric enum layouts. Remove the old
            // state array before deserializing GameData, then explicitly map
            // its raw numeric status through this bridge DTO.
            JToken legacyToken = root[nameof(GameData.WorldQuests)];
            if (legacyToken != null &&
                legacyToken.Type is not JTokenType.Null and
                not JTokenType.Array)
            {
                throw new JsonSerializationException(
                    "V1 存档的 WorldQuests 必须是数组。");
            }
            JArray legacyStates = legacyToken as JArray ?? new JArray();
            root[nameof(GameData.WorldQuests)] = new JArray();
            GameData gameData = root.ToObject<GameData>();
            gameData.WorldQuestStateVersion = version;
            gameData.WorldQuests = legacyStates
                .Select(token => token.ToObject<WorldQuestV1StateDto>())
                .Where(value => value != null)
                .Select(value => value.ToRuntimeState())
                .ToList();
            return (T)(object)gameData;
        }

        private sealed class SaveVersionTooNewException :
            JsonSerializationException
        {
            public SaveVersionTooNewException(string message)
                : base(message)
            {
            }
        }

        [Serializable]
        private sealed class WorldQuestV1StateDto
        {
            public string QuestId;
            public int Status;
            public int ObjectiveIndex;
            public int CurrentAmount;
            public bool AcceptanceRewardClaimed;
            public bool CompletionRewardClaimed;

            public WorldQuestStateData ToRuntimeState()
            {
#pragma warning disable CS0612
                return new WorldQuestStateData
                {
                    QuestId = QuestId,
                    Status = (WorldQuestStatus)Status,
                    ObjectiveIndex = ObjectiveIndex,
                    CurrentAmount = CurrentAmount,
                    AcceptanceRewardClaimed = AcceptanceRewardClaimed,
                    CompletionRewardClaimed = CompletionRewardClaimed
                };
#pragma warning restore CS0612
            }
        }
    }
}
