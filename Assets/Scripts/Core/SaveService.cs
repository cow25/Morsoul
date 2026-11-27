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
        readonly int _slot;

        public SaveService(string filePath) : this(filePath, false, 0) { }

        SaveService(string filePath, bool isPersistent, int slot)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("filePath required", nameof(filePath));
            _filePath = filePath;
            _isPersistent = isPersistent;
            _slot = slot;
        }

        const string LastSlotPrefKeyPrefix = "Morsoul.LastSaveSlot";
        static int _activeSlot = -1;

        static string LastSlotPrefKey =>
            !Application.isEditor && GameLock.HasExplicitAccountOrDb
                ? $"{LastSlotPrefKeyPrefix}.{GameLock.AccountTag}"
                : LastSlotPrefKeyPrefix;

        /// <summary>Active save slot (1..3). Persists across sessions via PlayerPrefs.</summary>
        public static int ActiveSlot
        {
            get
            {
                if (_activeSlot < 1)
                    _activeSlot = Mathf.Clamp(PlayerPrefs.GetInt(LastSlotPrefKey, 1), 1, 3);
                return _activeSlot;
            }
            set
            {
                _activeSlot = Mathf.Clamp(value, 1, 3);
                PlayerPrefs.SetInt(LastSlotPrefKey, _activeSlot);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Service for a specific save slot (1..3). All slots sync with LauncherDb in builds.</summary>
        public static SaveService ForSlot(int slot)
        {
            int clamped = Mathf.Clamp(slot, 1, 3);
            string accountTag = !Application.isEditor && GameLock.HasExplicitAccountOrDb
                ? $"_{GameLock.AccountTag}"
                : "";
            string suffix = clamped == 1 ? "" : $"_slot{clamped}";
            return new SaveService(Path.Combine(Application.persistentDataPath,
#if UNITY_EDITOR
                $"morsoul_checkpoint{accountTag}{suffix}_editor.json"
#else
                $"morsoul_checkpoint{accountTag}{suffix}.json"
#endif
            ), isPersistent: true, slot: clamped);
        }

        /// <summary>Default service writing to persistentDataPath (isolated in Editor).</summary>
        public static SaveService Persistent => ForSlot(ActiveSlot);

        public bool HasSave => File.Exists(_filePath) || (_isPersistent && LauncherDb.HasDatabaseSave(_slot));

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
                LauncherDb.TrySave(state, json, _slot);
        }

        /// <summary>
        /// Loads and verifies the checkpoint. Returns false when no file
        /// exists, the file is malformed, or the signature fails.
        /// </summary>
        public bool TryLoad(out SaveState state, out string error)
        {
            state = null;
            error = null;
            if (_isPersistent && LauncherDb.TryLoad(_slot, out state))
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
            if (_isPersistent) LauncherDb.TryDeleteSlot(_slot);
        }
    }
}
