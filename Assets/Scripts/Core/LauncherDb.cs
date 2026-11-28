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
        delegate int ColumnIntFn(IntPtr stmt, int col);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate long ColumnInt64Fn(IntPtr stmt, int col);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate double ColumnDoubleFn(IntPtr stmt, int col);
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
        static ColumnIntFn _columnInt;
        static ColumnInt64Fn _columnInt64;
        static ColumnDoubleFn _columnDouble;
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
            _columnInt = GetFn<ColumnIntFn>(lib, "sqlite3_column_int");
            _columnInt64 = GetFn<ColumnInt64Fn>(lib, "sqlite3_column_int64");
            _columnDouble = GetFn<ColumnDoubleFn>(lib, "sqlite3_column_double");
            _free = GetFn<FreeFn>(lib, "sqlite3_free");

            _loaded = _openV2 != null && _closeV2 != null && _exec != null &&
                      _prepareV2 != null && _step != null && _finalize != null &&
                      _bindText != null && _bindInt != null && _bindInt64 != null &&
                      _bindDouble != null && _columnText != null &&
                      _columnInt != null && _columnInt64 != null && _columnDouble != null;
            if (!_loaded) Debug.LogWarning("LauncherDb: SQLCipher DLL missing expected sqlite3 exports.");
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
            if (string.IsNullOrEmpty(hexKey))
            {
                Debug.LogWarning("LauncherDb: database key unavailable in credential store.");
                return false;
            }

            if (!readOnly)
            {
                string dir = Path.GetDirectoryName(GameLock.DatabasePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            int flags = readOnly ? SQLITE_OPEN_READONLY : (SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE);
            if (_openV2(Utf8(GameLock.DatabasePath), out db, flags, IntPtr.Zero) != SQLITE_OK || db == IntPtr.Zero)
            {
                Debug.LogWarning($"LauncherDb: failed to open '{GameLock.DatabasePath}' (readOnly={readOnly}).");
                if (db != IntPtr.Zero) _closeV2(db);
                db = IntPtr.Zero;
                return false;
            }

            bool pragmasOk =
                Exec(db, $"PRAGMA key = \"x'{hexKey}'\";") &&
                RunStmt(db, "SELECT count(*) FROM sqlite_master;", null) &&
                Exec(db, "PRAGMA foreign_keys = ON;") &&
                (readOnly || (Exec(db, "PRAGMA journal_mode = WAL;") && Exec(db, "PRAGMA synchronous = FULL;"))) &&
                Exec(db, "PRAGMA busy_timeout = 5000;") &&
                (readOnly || EnsureSchema(db));

            if (!pragmasOk)
            {
                Debug.LogWarning($"LauncherDb: failed to unlock or initialize '{GameLock.DatabasePath}' (readOnly={readOnly}).");
                _closeV2(db);
                db = IntPtr.Zero;
                return false;
            }
            return true;
        }

        static bool EnsureSchema(IntPtr db)
        {
            const string ddl =
                "CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY NOT NULL, value TEXT NOT NULL);" +
                "CREATE TABLE IF NOT EXISTS profiles (" +
                "profile_id TEXT PRIMARY KEY, display_name TEXT NOT NULL, age INTEGER, created_at INTEGER NOT NULL DEFAULT 0);" +
                "CREATE TABLE IF NOT EXISTS profile_state (" +
                "profile_id TEXT PRIMARY KEY, revision INTEGER NOT NULL DEFAULT 1, updated_at INTEGER NOT NULL, " +
                "playtime REAL NOT NULL DEFAULT 0, reputation INTEGER NOT NULL DEFAULT 0, " +
                "current_case_id TEXT NOT NULL DEFAULT '', ending TEXT NOT NULL DEFAULT '', " +
                "focus_energy REAL NOT NULL DEFAULT 100.0, focus_as_of_playtime REAL NOT NULL DEFAULT 0, " +
                "dialogue_json TEXT NOT NULL DEFAULT '{}', board_json TEXT NOT NULL DEFAULT '{}', " +
                "puzzles_json TEXT NOT NULL DEFAULT '{}', notes_json TEXT NOT NULL DEFAULT '[]', " +
                "checkpoint_json TEXT);" +
                "CREATE TABLE IF NOT EXISTS case_progress (" +
                "profile_id TEXT NOT NULL, case_id TEXT NOT NULL, status TEXT NOT NULL, " +
                "progress_version INTEGER NOT NULL DEFAULT 1, objective_id TEXT, " +
                "warrant_granted INTEGER NOT NULL DEFAULT 0, failed_attempts INTEGER NOT NULL DEFAULT 0, " +
                "hint_tier INTEGER NOT NULL DEFAULT 0, updated_at INTEGER, " +
                "PRIMARY KEY (profile_id, case_id));" +
                "CREATE TABLE IF NOT EXISTS deduction_links (" +
                "profile_id TEXT NOT NULL, case_id TEXT NOT NULL, source_id TEXT NOT NULL, target_id TEXT NOT NULL, " +
                "PRIMARY KEY (profile_id, case_id, source_id, target_id));" +
                "CREATE TABLE IF NOT EXISTS clues_found (" +
                "profile_id TEXT NOT NULL, clue_id TEXT NOT NULL, case_id TEXT NOT NULL, " +
                "collected_at INTEGER NOT NULL, via TEXT NOT NULL DEFAULT 'examine', " +
                "PRIMARY KEY (profile_id, case_id, clue_id));" +
                "CREATE TABLE IF NOT EXISTS case_results (" +
                "profile_id TEXT NOT NULL, case_id TEXT NOT NULL, conclusion_id TEXT NOT NULL DEFAULT '', " +
                "correct INTEGER NOT NULL DEFAULT 1, flawed INTEGER NOT NULL DEFAULT 0, " +
                "strikes INTEGER NOT NULL DEFAULT 0, attempts INTEGER NOT NULL DEFAULT 1, completed_at INTEGER NOT NULL, " +
                "PRIMARY KEY (profile_id, case_id));" +
                "INSERT OR IGNORE INTO meta(key, value) VALUES ('schemaVersion', '1'), ('contentVersion', '1');" +
                "PRAGMA user_version = 1;";
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

        static string ColStr(IntPtr stmt, int col)
        {
            IntPtr ptr = _columnText(stmt, col);
            return ptr != IntPtr.Zero ? Marshal.PtrToStringUTF8(ptr) ?? "" : "";
        }

        static bool TryGetProfileIdForSlot(IntPtr db, int slot, out string profileId)
        {
            profileId = null;
            int offset = Mathf.Clamp(slot, 1, 3) - 1;
            const string sql =
                "SELECT p.profile_id FROM profiles p " +
                "LEFT JOIN profile_state s ON s.profile_id = p.profile_id " +
                "ORDER BY p.created_at ASC, p.profile_id ASC LIMIT 1 OFFSET ?1;";
            if (_prepareV2(db, Utf8(sql), -1, out IntPtr stmt, out _) != SQLITE_OK) return false;
            try
            {
                _bindInt(stmt, 1, offset);
                if (_step(stmt) == SQLITE_ROW)
                {
                    profileId = ColStr(stmt, 0);
                    return !string.IsNullOrEmpty(profileId);
                }
            }
            finally
            {
                _finalize(stmt);
            }
            return false;
        }

        static int ParseCaseNumber(string caseIdStr)
        {
            if (string.IsNullOrWhiteSpace(caseIdStr)) return 0;
            string s = caseIdStr.Trim();
            if (s.StartsWith("case_", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(5);
            return int.TryParse(s, out int n) && n >= 0 ? n : 0;
        }

        static string SanitizeDisplayName(string raw)
        {
            string s = string.IsNullOrWhiteSpace(raw) ? "Detective" : raw.Trim();
            var sb = new StringBuilder(Mathf.Min(20, s.Length));
            foreach (char c in s)
            {
                if (sb.Length >= 20) break;
                bool ok = char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' ';
                sb.Append(ok ? c : '_');
            }
            string cleaned = sb.ToString().Trim();
            return string.IsNullOrEmpty(cleaned) ? "Detective" : cleaned;
        }

        static CaseBoardSave GetOrCreateBoard(SaveState state, int caseId)
        {
            foreach (var b in state.boardStates)
                if (b != null && b.caseId == caseId) return b;
            var created = new CaseBoardSave { caseId = caseId };
            state.boardStates.Add(created);
            return created;
        }

        static bool TryReconstructFromRelational(IntPtr db, string profileId, out SaveState state)
        {
            state = null;
            string displayName = "Detective";
            if (_prepareV2(db, Utf8("SELECT display_name FROM profiles WHERE profile_id = ?1;"), -1, out IntPtr pStmt, out _) != SQLITE_OK)
                return false;
            try
            {
                BindStr(pStmt, 1, profileId);
                if (_step(pStmt) != SQLITE_ROW) return false;
                displayName = ColStr(pStmt, 0);
            }
            finally
            {
                _finalize(pStmt);
            }

            string currentCaseId = "";
            float focusEnergy = FocusTuning.FocusMax;
            bool hasStateRow = false;
            if (_prepareV2(db, Utf8("SELECT current_case_id, focus_energy FROM profile_state WHERE profile_id = ?1;"), -1, out IntPtr sStmt, out _) == SQLITE_OK)
            {
                try
                {
                    BindStr(sStmt, 1, profileId);
                    if (_step(sStmt) == SQLITE_ROW)
                    {
                        hasStateRow = true;
                        currentCaseId = ColStr(sStmt, 0);
                        focusEnergy = Mathf.Clamp((float)_columnDouble(sStmt, 1), 0f, FocusTuning.FocusMax);
                    }
                }
                finally
                {
                    _finalize(sStmt);
                }
            }
            if (!hasStateRow) return false;

            int currentCase = ParseCaseNumber(currentCaseId);
            state = new SaveState
            {
                saveVersion = SaveState.CurrentVersion,
                profileId = profileId,
                playerName = string.IsNullOrWhiteSpace(displayName) ? "Detective" : displayName,
                currentCase = currentCase,
                focusEnergy = focusEnergy > 0f ? focusEnergy : FocusTuning.FocusMax,
                focusMax = FocusTuning.FocusMax,
                prologueCompleted = currentCase > 0,
            };
            state.toolsUnlocked.Add(Morsoul.Data.Tool.GolemEye);

            if (_prepareV2(db, Utf8("SELECT case_id, status, failed_attempts FROM case_progress WHERE profile_id = ?1;"), -1, out IntPtr cpStmt, out _) == SQLITE_OK)
            {
                try
                {
                    BindStr(cpStmt, 1, profileId);
                    while (_step(cpStmt) == SQLITE_ROW)
                    {
                        int cId = ParseCaseNumber(ColStr(cpStmt, 0));
                        string status = ColStr(cpStmt, 1);
                        int failed = _columnInt(cpStmt, 2);
                        var board = GetOrCreateBoard(state, cId);
                        board.boardSolved = string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);
                        board.caseErrorCount = Mathf.Max(0, failed);
                        if (cId == 0 && board.boardSolved)
                            state.prologueCompleted = true;
                    }
                }
                finally
                {
                    _finalize(cpStmt);
                }
            }

            if (_prepareV2(db, Utf8("SELECT clue_id FROM clues_found WHERE profile_id = ?1;"), -1, out IntPtr clStmt, out _) == SQLITE_OK)
            {
                try
                {
                    BindStr(clStmt, 1, profileId);
                    while (_step(clStmt) == SQLITE_ROW)
                    {
                        string cid = ColStr(clStmt, 0);
                        if (!string.IsNullOrEmpty(cid) && !state.collectedClueIds.Contains(cid))
                            state.collectedClueIds.Add(cid);
                    }
                }
                finally
                {
                    _finalize(clStmt);
                }
            }

            if (_prepareV2(db, Utf8("SELECT case_id, source_id, target_id FROM deduction_links WHERE profile_id = ?1;"), -1, out IntPtr dlStmt, out _) == SQLITE_OK)
            {
                try
                {
                    BindStr(dlStmt, 1, profileId);
                    while (_step(dlStmt) == SQLITE_ROW)
                    {
                        int cId = ParseCaseNumber(ColStr(dlStmt, 0));
                        string src = ColStr(dlStmt, 1);
                        string dst = ColStr(dlStmt, 2);
                        if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(dst))
                            GetOrCreateBoard(state, cId).links.Add(new BoardLinkSave(src, dst));
                    }
                }
                finally
                {
                    _finalize(dlStmt);
                }
            }

            while (state.caseGrades.Count < 4)
                state.caseGrades.Add(Morsoul.Data.Grade.Ungraded);

            if (_prepareV2(db, Utf8("SELECT case_id, flawed FROM case_results WHERE profile_id = ?1;"), -1, out IntPtr crStmt, out _) == SQLITE_OK)
            {
                try
                {
                    BindStr(crStmt, 1, profileId);
                    while (_step(crStmt) == SQLITE_ROW)
                    {
                        int cId = ParseCaseNumber(ColStr(crStmt, 0));
                        bool flawed = _columnInt(crStmt, 1) != 0;
                        if (cId >= 1 && cId <= 4)
                        {
                            state.caseGrades[cId - 1] = flawed ? Morsoul.Data.Grade.Flawed : Morsoul.Data.Grade.Standard;
                            GetOrCreateBoard(state, cId).boardSolved = true;
                        }
                    }
                }
                finally
                {
                    _finalize(crStmt);
                }
            }

            return true;
        }

        public static bool HasDatabaseSave() => HasDatabaseSave(1);

        public static bool HasDatabaseSave(int slot)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Application.isEditor) return false;
            return TryLoad(slot, out _);
#else
            return false;
#endif
        }

        public static bool TryLoad(out SaveState state) => TryLoad(1, out state);

        public static bool TryLoad(int slot, out SaveState state)
        {
            state = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Application.isEditor) return false;
            if (!TryOpen(true, out IntPtr db)) return false;
            try
            {
                if (!TryGetProfileIdForSlot(db, slot, out string profileId))
                    return false;

                const string sql = "SELECT checkpoint_json FROM profile_state WHERE profile_id = ?1 LIMIT 1;";
                if (_prepareV2(db, Utf8(sql), -1, out IntPtr stmt, out _) != SQLITE_OK) return false;
                try
                {
                    BindStr(stmt, 1, profileId);
                    if (_step(stmt) == SQLITE_ROW)
                    {
                        string json = ColStr(stmt, 0);
                        if (!string.IsNullOrEmpty(json))
                        {
                            if (SaveSerializer.TryDeserialize(json, out state, out string err))
                                return true;
                            Debug.LogWarning($"LauncherDb: checkpoint_json failed verification for '{profileId}' ({err}); falling back to relational state.");
                        }
                    }
                }
                finally
                {
                    _finalize(stmt);
                }

                return TryReconstructFromRelational(db, profileId, out state);
            }
            finally
            {
                _closeV2(db);
            }
#else
            return false;
#endif
        }

        public static bool TryDeleteSlot(int slot)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Application.isEditor) return false;
            if (!File.Exists(GameLock.DatabasePath)) return false;
            if (!TryOpen(false, out IntPtr db)) return false;
            try
            {
                if (!TryGetProfileIdForSlot(db, slot, out string profileId)) return false;
                if (!Exec(db, "BEGIN IMMEDIATE;")) return false;
                string[] tables = { "case_results", "deduction_links", "clues_found", "case_progress", "profile_state", "profiles" };
                foreach (string tbl in tables)
                    RunStmt(db, $"DELETE FROM {tbl} WHERE profile_id = ?1;", s => BindStr(s, 1, profileId));
                Exec(db, "COMMIT;");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"LauncherDb.TryDeleteSlot failed: {ex.Message}");
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

        public static bool TrySave(SaveState state, string sealedCheckpointJson) =>
            TrySave(state, sealedCheckpointJson, SaveService.ActiveSlot);

        public static bool TrySave(SaveState state, string sealedCheckpointJson, int slot)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Application.isEditor) return false;
            if (state == null || string.IsNullOrEmpty(sealedCheckpointJson)) return false;
            if (!TryOpen(false, out IntPtr db)) return false;
            try
            {
                if (!Exec(db, "BEGIN IMMEDIATE;")) return false;
                string profileId = string.IsNullOrEmpty(state.profileId)
                    ? Guid.NewGuid().ToString()
                    : state.profileId;
                string displayName = SanitizeDisplayName(state.playerName);
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
                        string cCase = $"case_{cId:D2}";
                        RunStmt(db,
                            "INSERT OR IGNORE INTO case_progress (profile_id, case_id, status, progress_version, objective_id, warrant_granted, failed_attempts, hint_tier, updated_at) " +
                            "VALUES (?1, ?2, 'completed', 1, '', 0, 0, 0, ?3);",
                            s => { BindStr(s, 1, profileId); BindStr(s, 2, cCase); _bindInt64(s, 3, now); });
                        RunStmt(db,
                            "UPDATE case_progress SET status = 'completed' WHERE profile_id = ?1 AND case_id = ?2;",
                            s => { BindStr(s, 1, profileId); BindStr(s, 2, cCase); });
                        RunStmt(db,
                            "INSERT OR REPLACE INTO case_results (profile_id, case_id, conclusion_id, correct, flawed, strikes, attempts, completed_at) " +
                            "VALUES (?1, ?2, '', 1, ?3, 0, 1, ?4);",
                            s => { BindStr(s, 1, profileId); BindStr(s, 2, cCase); _bindInt(s, 3, grade == Morsoul.Data.Grade.Flawed ? 1 : 0); _bindInt64(s, 4, now); });
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

