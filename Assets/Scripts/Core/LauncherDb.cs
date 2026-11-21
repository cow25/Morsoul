using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Morsoul.Core
{
    /// <summary>
    /// Parameterized Win32 SQLCipher bridge for %LOCALAPPDATA%\Morsoul\game.db.
    /// Probes launcher-bundled SQLCipher DLLs at runtime; no-ops gracefully when
    /// running standalone or in Editor without the launcher.
    /// </summary>
    public static class LauncherDb
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        const int SQLITE_OK = 0;
        const int SQLITE_ROW = 100;
        const int SQLITE_OPEN_READWRITE = 0x00000002;
        const int SQLITE_OPEN_READONLY = 0x00000001;
        const int SQLITE_OPEN_CREATE = 0x00000004;
        static readonly IntPtr SQLITE_TRANSIENT = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int OpenV2Fn(byte[] filename, out IntPtr db, int flags, IntPtr zVfs);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int CloseV2Fn(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int ExecFn(IntPtr db, byte[] sql, IntPtr cb, IntPtr arg, out IntPtr err);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int PrepareV2Fn(IntPtr db, byte[] sql, int nByte, out IntPtr stmt, out IntPtr tail);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int StepFn(IntPtr stmt);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int FinalizeFn(IntPtr stmt);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int BindTextFn(IntPtr stmt, int idx, byte[] val, int nBytes, IntPtr destructor);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int BindIntFn(IntPtr stmt, int idx, int val);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int BindInt64Fn(IntPtr stmt, int idx, long val);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int BindDoubleFn(IntPtr stmt, int idx, double val);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr ColumnTextFn(IntPtr stmt, int col);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void FreeFn(IntPtr ptr);

        static bool _probed;
        static bool _loaded;
        static OpenV2Fn _openV2;
        static CloseV2Fn _closeV2;
        static ExecFn _exec;
        static PrepareV2Fn _prepareV2;
        static StepFn _step;
        static FinalizeFn _finalize;
        static BindTextFn _bindText;
        static BindIntFn _bindInt;
        static BindInt64Fn _bindInt64;
        static BindDoubleFn _bindDouble;
        static ColumnTextFn _columnText;
        static FreeFn _free;

        static bool EnsureNativeLoaded()
        {
            if (_probed) return _loaded;
            _probed = true;

            string appDir = Path.GetDirectoryName(Application.dataPath) ?? "";
            string localDir = Path.GetDirectoryName(GameLock.DatabasePath) ?? "";
            string[] names = { "e_sqlcipher.dll", "sqlcipher.dll", "sqlite3.dll" };
            string[] dirs = { "", appDir, Path.Combine(Application.dataPath, "Plugins", "x86_64"), localDir };

            IntPtr lib = IntPtr.Zero;
            foreach (string dir in dirs)
            {
                foreach (string name in names)
                {
                    string candidate = string.IsNullOrEmpty(dir) ? name : Path.Combine(dir, name);
                    if (!string.IsNullOrEmpty(dir) && !File.Exists(candidate)) continue;
                    lib = LoadLibrary(candidate);
                    if (lib != IntPtr.Zero) break;
                }
                if (lib != IntPtr.Zero) break;
            }
            if (lib == IntPtr.Zero) return false;

            _openV2 = GetFn<OpenV2Fn>(lib, "sqlite3_open_v2");
            _closeV2 = GetFn<CloseV2Fn>(lib, "sqlite3_close_v2") ?? GetFn<CloseV2Fn>(lib, "sqlite3_close");
            _exec = GetFn<ExecFn>(lib, "sqlite3_exec");
            _prepareV2 = GetFn<PrepareV2Fn>(lib, "sqlite3_prepare_v2");
            _step = GetFn<StepFn>(lib, "sqlite3_step");
            _finalize = GetFn<FinalizeFn>(lib, "sqlite3_finalize");
            _bindText = GetFn<BindTextFn>(lib, "sqlite3_bind_text");
            _bindInt = GetFn<BindIntFn>(lib, "sqlite3_bind_int");
            _bindInt64 = GetFn<BindInt64Fn>(lib, "sqlite3_bind_int64");
            _bindDouble = GetFn<BindDoubleFn>(lib, "sqlite3_bind_double");
            _columnText = GetFn<ColumnTextFn>(lib, "sqlite3_column_text");
            _free = GetFn<FreeFn>(lib, "sqlite3_free");

            _loaded = _openV2 != null && _closeV2 != null && _exec != null &&
                      _prepareV2 != null && _step != null && _finalize != null &&
                      _bindText != null && _bindInt != null && _bindInt64 != null &&
                      _bindDouble != null && _columnText != null;
            return _loaded;
        }

        static T GetFn<T>(IntPtr lib, string name) where T : Delegate
        {
            IntPtr p = GetProcAddress(lib, name);
            return p != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<T>(p) : null;
        }

        static bool TryOpen(bool readOnly, out IntPtr db)
        {
            db = IntPtr.Zero;
            if ((readOnly && !File.Exists(GameLock.DatabasePath)) || !EnsureNativeLoaded()) return false;
            string hexKey = KeyStoreReader.GetDatabaseHexKey();
            if (string.IsNullOrEmpty(hexKey)) return false;

            if (!readOnly)
            {
                string dir = Path.GetDirectoryName(GameLock.DatabasePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            int flags = readOnly ? SQLITE_OPEN_READONLY : (SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE);
            if (_openV2(Utf8(GameLock.DatabasePath), out db, flags, IntPtr.Zero) != SQLITE_OK || db == IntPtr.Zero)
            {
                if (db != IntPtr.Zero) _closeV2(db);
                db = IntPtr.Zero;
                return false;
            }

            if (!Exec(db, $"PRAGMA key = \"x'{hexKey}'\";") ||
                !Exec(db, "PRAGMA foreign_keys = ON;") ||
                !Exec(db, "PRAGMA journal_mode = WAL;") ||
                !Exec(db, "PRAGMA synchronous = FULL;") ||
                !Exec(db, "PRAGMA busy_timeout = 5000;") ||
                (!readOnly && !EnsureSchema(db)))
            {
                _closeV2(db);
                db = IntPtr.Zero;
                return false;
            }
            return true;
        }

        static bool EnsureSchema(IntPtr db)
        {
            const string ddl =
                "CREATE TABLE IF NOT EXISTS profiles (" +
                "profile_id TEXT PRIMARY KEY, display_name TEXT NOT NULL, created_at INTEGER NOT NULL);" +
                "CREATE TABLE IF NOT EXISTS profile_state (" +
                "profile_id TEXT PRIMARY KEY, revision INTEGER NOT NULL DEFAULT 1, updated_at INTEGER NOT NULL, " +
                "playtime REAL NOT NULL DEFAULT 0, reputation INTEGER NOT NULL DEFAULT 0, " +
                "current_case_id TEXT NOT NULL DEFAULT '', ending TEXT NOT NULL DEFAULT '', " +
                "focus_energy REAL NOT NULL DEFAULT 100.0, focus_as_of_playtime REAL NOT NULL DEFAULT 0, " +
                "dialogue_json TEXT NOT NULL DEFAULT '{}', board_json TEXT NOT NULL DEFAULT '{}', " +
                "puzzles_json TEXT NOT NULL DEFAULT '{}', notes_json TEXT NOT NULL DEFAULT '[]', " +
                "checkpoint_json TEXT NOT NULL DEFAULT '');" +
                "CREATE TABLE IF NOT EXISTS case_progress (" +
                "profile_id TEXT NOT NULL, case_id TEXT NOT NULL, status TEXT NOT NULL, " +
                "progress_version INTEGER NOT NULL DEFAULT 1, objective_id TEXT NOT NULL DEFAULT '', " +
                "warrant_granted INTEGER NOT NULL DEFAULT 0, failed_attempts INTEGER NOT NULL DEFAULT 0, " +
                "hint_tier INTEGER NOT NULL DEFAULT 0, updated_at INTEGER NOT NULL, " +
                "PRIMARY KEY (profile_id, case_id));" +
                "CREATE TABLE IF NOT EXISTS deduction_links (" +
                "profile_id TEXT NOT NULL, case_id TEXT NOT NULL, source_id TEXT NOT NULL, target_id TEXT NOT NULL, " +
                "PRIMARY KEY (profile_id, case_id, source_id, target_id));" +
                "CREATE TABLE IF NOT EXISTS clues_found (" +
                "profile_id TEXT NOT NULL, clue_id TEXT NOT NULL, case_id TEXT NOT NULL, " +
                "collected_at INTEGER NOT NULL, via TEXT NOT NULL DEFAULT 'examine', " +
                "PRIMARY KEY (profile_id, clue_id));" +
                "CREATE TABLE IF NOT EXISTS case_results (" +
                "profile_id TEXT NOT NULL, case_id TEXT NOT NULL, conclusion_id TEXT NOT NULL DEFAULT '', " +
                "correct INTEGER NOT NULL DEFAULT 1, flawed INTEGER NOT NULL DEFAULT 0, " +
                "strikes INTEGER NOT NULL DEFAULT 0, attempts INTEGER NOT NULL DEFAULT 1, completed_at INTEGER NOT NULL, " +
                "PRIMARY KEY (profile_id, case_id));";
            return Exec(db, ddl);
        }

        static bool Exec(IntPtr db, string sql)
        {
            int rc = _exec(db, Utf8(sql), IntPtr.Zero, IntPtr.Zero, out IntPtr err);
            if (err != IntPtr.Zero) _free?.Invoke(err);
            return rc == SQLITE_OK;
        }

        static byte[] Utf8(string s) => Encoding.UTF8.GetBytes((s ?? "") + "\0");
        static byte[] Utf8NoNull(string s) => Encoding.UTF8.GetBytes(s ?? "");

        static void BindStr(IntPtr stmt, int idx, string val)
        {
            var bytes = Utf8NoNull(val);
            _bindText(stmt, idx, bytes, bytes.Length, SQLITE_TRANSIENT);
        }

        static bool RunStmt(IntPtr db, string sql, Action<IntPtr> bind)
        {
            if (_prepareV2(db, Utf8(sql), -1, out IntPtr stmt, out _) != SQLITE_OK) return false;
            try
            {
                bind?.Invoke(stmt);
                int rc = _step(stmt);
                return rc == 101 /* SQLITE_DONE */ || rc == SQLITE_ROW;
            }
            finally
            {
                _finalize(stmt);
            }
        }
#endif

        public static bool HasDatabaseSave()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Application.isEditor) return false;
            return TryLoad(out _);
#else
            return false;
#endif
        }

        public static bool TryLoad(out SaveState state)
        {
            state = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Application.isEditor) return false;
            if (!TryOpen(true, out IntPtr db)) return false;
            try
            {
                const string sql = "SELECT checkpoint_json FROM profile_state WHERE checkpoint_json IS NOT NULL AND checkpoint_json != '' ORDER BY updated_at DESC LIMIT 1;";
                if (_prepareV2(db, Utf8(sql), -1, out IntPtr stmt, out _) != SQLITE_OK) return false;
                try
                {
                    if (_step(stmt) == SQLITE_ROW)
                    {
                        IntPtr ptr = _columnText(stmt, 0);
                        if (ptr != IntPtr.Zero)
                        {
                            string json = Marshal.PtrToStringUTF8(ptr);
                            return !string.IsNullOrEmpty(json) && SaveSerializer.TryDeserialize(json, out state, out _);
                        }
                    }
                }
                finally
                {
                    _finalize(stmt);
                }
            }
            finally
            {
                _closeV2(db);
            }
#endif
            return false;
        }

        public static bool TrySave(SaveState state, string sealedCheckpointJson)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Application.isEditor) return false;
            if (state == null || string.IsNullOrEmpty(sealedCheckpointJson)) return false;
            if (!TryOpen(false, out IntPtr db)) return false;
            try
            {
                if (!Exec(db, "BEGIN IMMEDIATE;")) return false;
                string profileId = string.IsNullOrEmpty(state.profileId) ? "local" : state.profileId;
                string displayName = string.IsNullOrEmpty(state.playerName) ? "Detective" : state.playerName;
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string caseIdStr = $"case_{state.currentCase:D2}";
                double focus = Math.Round(state.focusEnergy, 3);

                bool ok =
                    RunStmt(db,
                        "INSERT INTO profiles (profile_id, display_name, created_at) VALUES (?1, ?2, ?3) " +
                        "ON CONFLICT(profile_id) DO UPDATE SET display_name = excluded.display_name;",
                        s => { BindStr(s, 1, profileId); BindStr(s, 2, displayName); _bindInt64(s, 3, now); }) &&
                    RunStmt(db,
                        "INSERT INTO profile_state (profile_id, revision, updated_at, playtime, reputation, current_case_id, ending, focus_energy, focus_as_of_playtime, dialogue_json, board_json, puzzles_json, notes_json, checkpoint_json) " +
                        "VALUES (?1, 1, ?2, 0, 0, ?3, '', ?4, 0, '{}', '{}', '{}', '[]', ?5) " +
                        "ON CONFLICT(profile_id) DO UPDATE SET revision = revision + 1, updated_at = excluded.updated_at, current_case_id = excluded.current_case_id, focus_energy = excluded.focus_energy, checkpoint_json = excluded.checkpoint_json;",
                        s => { BindStr(s, 1, profileId); _bindInt64(s, 2, now); BindStr(s, 3, caseIdStr); _bindDouble(s, 4, focus); BindStr(s, 5, sealedCheckpointJson); });

                if (ok && state.boardStates != null)
                {
                    foreach (var b in state.boardStates)
                    {
                        if (b == null) continue;
                        string bCase = $"case_{b.caseId:D2}";
                        RunStmt(db,
                            "INSERT OR REPLACE INTO case_progress (profile_id, case_id, status, progress_version, objective_id, warrant_granted, failed_attempts, hint_tier, updated_at) " +
                            "VALUES (?1, ?2, ?3, 1, ?4, 0, ?5, 0, ?6);",
                            s =>
                            {
                                BindStr(s, 1, profileId);
                                BindStr(s, 2, bCase);
                                BindStr(s, 3, b.boardSolved ? "completed" : "in_progress");
                                BindStr(s, 4, state.currentSceneId ?? "");
                                _bindInt(s, 5, b.caseErrorCount);
                                _bindInt64(s, 6, now);
                            });
                        if (b.links != null)
                        {
                            RunStmt(db, "DELETE FROM deduction_links WHERE profile_id = ?1 AND case_id = ?2;",
                                s => { BindStr(s, 1, profileId); BindStr(s, 2, bCase); });
                            foreach (var edge in b.links)
                            {
                                if (edge == null) continue;
                                RunStmt(db,
                                    "INSERT OR IGNORE INTO deduction_links (profile_id, case_id, source_id, target_id) VALUES (?1, ?2, ?3, ?4);",
                                    s => { BindStr(s, 1, profileId); BindStr(s, 2, bCase); BindStr(s, 3, edge.sourceId); BindStr(s, 4, edge.targetId); });
                            }
                        }
                    }
                }

                if (ok && state.collectedClueIds != null)
                {
                    foreach (string cid in state.collectedClueIds)
                    {
                        if (string.IsNullOrEmpty(cid)) continue;
                        RunStmt(db,
                            "INSERT OR IGNORE INTO clues_found (profile_id, clue_id, case_id, collected_at, via) VALUES (?1, ?2, ?3, ?4, 'examine');",
                            s => { BindStr(s, 1, profileId); BindStr(s, 2, cid); BindStr(s, 3, caseIdStr); _bindInt64(s, 4, now); });
                    }
                }

                if (ok && state.caseGrades != null)
                {
                    for (int i = 0; i < state.caseGrades.Count; i++)
                    {
                        var grade = state.caseGrades[i];
                        if (grade == Morsoul.Data.Grade.Ungraded) continue;
                        int cId = i + 1;
                        RunStmt(db,
                            "INSERT OR REPLACE INTO case_results (profile_id, case_id, conclusion_id, correct, flawed, strikes, attempts, completed_at) " +
                            "VALUES (?1, ?2, '', 1, ?3, 0, 1, ?4);",
                            s => { BindStr(s, 1, profileId); BindStr(s, 2, $"case_{cId:D2}"); _bindInt(s, 3, grade == Morsoul.Data.Grade.Flawed ? 1 : 0); _bindInt64(s, 4, now); });
                    }
                }

                Exec(db, ok ? "COMMIT;" : "ROLLBACK;");
                return ok;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"LauncherDb.TrySave failed: {ex.Message}");
                Exec(db, "ROLLBACK;");
                return false;
            }
            finally
            {
                _closeV2(db);
            }
#else
            return false;
#endif
        }
    }
}

