using System;
using System.IO;
using UnityEngine;

namespace Morsoul.Core
{
    /// <summary>
    /// Reads/writes the JSON checkpoint file. Path is injectable so EditMode
    /// tests use a temp file and runtime uses persistentDataPath.
    /// </summary>
    public class SaveService
    {
        readonly string _filePath;

        public SaveService(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("filePath required", nameof(filePath));
            _filePath = filePath;
        }

        /// <summary>Default service writing to persistentDataPath.</summary>
        public static SaveService Persistent =>
            new SaveService(Path.Combine(Application.persistentDataPath,
                "morsoul_checkpoint.json"));

        public bool HasSave => File.Exists(_filePath);

        /// <summary>Writes the checkpoint atomically (temp file + move).</summary>
        public void Save(SaveState state)
        {
            string json = SaveSerializer.Serialize(state);
            string dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(_filePath)) File.Delete(_filePath);
            File.Move(tmp, _filePath);
        }

        /// <summary>
        /// Loads and verifies the checkpoint. Returns false when no file
        /// exists, the file is malformed, or the signature fails.
        /// </summary>
        public bool TryLoad(out SaveState state, out string error)
        {
            state = null;
            error = null;
            if (!File.Exists(_filePath))
            {
                error = "no checkpoint file";
                return false;
            }
            return SaveSerializer.TryDeserialize(File.ReadAllText(_filePath),
                out state, out error);
        }

        /// <summary>Deletes the checkpoint if present.</summary>
        public void DeleteSave()
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
        }
    }
}
