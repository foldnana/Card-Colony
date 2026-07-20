using System;
using System.IO;
using System.Text;
using UnityEngine;
using CardColony.Gameplay;

namespace CardColony.UnityIntegration.Save
{
    public static class RunSnapshotFileStore
    {
        public static void Save(string fileName, RunSnapshot snapshot)
        {
            if (!TrySave(fileName, snapshot, out string error))
                throw new IOException(error);
        }

        public static bool TrySave(string fileName, RunSnapshot snapshot, out string error)
        {
            string temporaryPath = null;
            try
            {
                string path = GetPath(fileName);
                temporaryPath = path + ".tmp";
                string backupPath = path + ".bak";
                string json = RunSnapshotJsonSerializer.Serialize(snapshot);

                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    File.Replace(temporaryPath, path, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                error = null;
                return true;
            }
            catch (Exception exception)
            {
                TryDelete(temporaryPath);
                error = exception.Message;
                return false;
            }
        }

        public static bool TryLoad(string fileName, out RunSnapshot snapshot)
        {
            return TryLoad(fileName, out snapshot, out _);
        }

        public static bool TryLoad(string fileName, out RunSnapshot snapshot, out string error)
        {
            try
            {
                string path = GetPath(fileName);
                if (!File.Exists(path))
                {
                    snapshot = null;
                    error = "Save file does not exist.";
                    return false;
                }

                try
                {
                    snapshot = RunSnapshotJsonSerializer.Deserialize(File.ReadAllText(path));
                }
                catch when (File.Exists(path + ".bak"))
                {
                    snapshot = RunSnapshotJsonSerializer.Deserialize(File.ReadAllText(path + ".bak"));
                }

                error = null;
                return true;
            }
            catch (Exception exception)
            {
                snapshot = null;
                error = exception.Message;
                return false;
            }
        }

        public static void Delete(string fileName)
        {
            string path = GetPath(fileName);
            if (File.Exists(path))
                File.Delete(path);

            string temporaryPath = path + ".tmp";
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            string backupPath = path + ".bak";
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // The known-good destination is never removed by this cleanup.
            }
        }

        private static string GetPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Save file name cannot be empty.", nameof(fileName));
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Save file name contains invalid characters.", nameof(fileName));

            return Path.Combine(Application.persistentDataPath, fileName + ".json");
        }
    }
}
