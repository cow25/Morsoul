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
        readonly bool _isPersistent;

        public SaveService(string filePath) : this(filePath, false) { }

        SaveService(string filePath, bool isPersistent)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("filePath required", nameof(filePath));
            _filePath = filePath;
            _isPersistent = isPersistent;
        }

        static int _activeSlot = 1;

        /// <summary>Active save slot (1..3). Defaults to 1.</summary>
        public static int ActiveSlot
        {
            get => _activeSlot;
            set => _activeSlot = Mathf.Clamp(value, 1, 3);
        }

        /// <summary>Service for a specific save slot (1..3). Slot 1 includes LauncherDb.</summary>
        public static SaveService ForSlot(int slot)
        {
            int clamped = Mathf.Clamp(slot, 1, 3);
            string suffix = clamped == 1 ? "" : $"_slot{clamped}";
            return new SaveService(Path.Combine(Application.persistentDataPath,
#if UNITY_EDITOR
                $"morsoul_checkpoint{suffix}_editor.json"
#else
                $"morsoul_checkpoint{suffix}.json"
#endif
            ), isPersistent: clamped == 1);
        }

        /// <summary>Default service writing to persistentDataPath (isolated in Editor).</summary>
        public static SaveService Persistent => ForSlot(_activeSlot);

        public bool HasSave => File.Exists(_filePath) || (_isPersistent && LauncherDb.HasDatabaseSave());

        /// <summary>
        /// Writes the checkpoint via a temp file, then swaps it in. When a
        /// previous save exists the swap is an atomic <see cref="File.Replace"/>
        /// keeping a .bak backup, so a crash mid-save cannot lose the old
        /// checkpoint. Falls back to delete+move if Replace is unsupported.
        /// </summary>
        public void Save(SaveState state)
        {
            string json = SaveSerializer.Serialize(state);
            string dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);

            if (File.Exists(_filePath))
            {
                string bak = _filePath + ".bak";
                try
                {
                    File.Replace(tmp, _filePath, bak);
                    if (File.Exists(bak)) File.Delete(bak);
                }
                catch (PlatformNotSupportedException)
                {
                    // Filesystem lacks atomic replace - last-resort swap.
                    File.Copy(_filePath, bak, true);
                    File.Move(tmp, _filePath);
                }
            }
            else
            {
                File.Move(tmp, _filePath);
            }

            if (_isPersistent)
                LauncherDb.TrySave(state, json);
        }

        /// <summary>
        /// Loads and verifies the checkpoint. Returns false when no file
        /// exists, the file is malformed, or the signature fails.
        /// </summary>
        public bool TryLoad(out SaveState state, out string error)
        {
            state = null;
            error = null;
            if (_isPersistent && LauncherDb.TryLoad(out state))
                return true;
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
