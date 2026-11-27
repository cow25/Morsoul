using System;
using System.Diagnostics;
using System.IO;

namespace Morsoul.Core
{
    /// <summary>
    /// Manages %LOCALAPPDATA%\Morsoul\game.lock so the Morsoul Launcher knows
    /// when the game client is running and triggers cloud sync on exit.
    /// </summary>
    public static class GameLock
    {
        static string _cachedDatabasePath;

        static string LocalDataDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Morsoul");

        public static string LockPath => Path.Combine(LocalDataDir, "game.lock");

        public static string DatabasePath =>
            _cachedDatabasePath ??= ResolveDatabasePath(Environment.GetCommandLineArgs());

        public static bool HasExplicitAccountOrDb =>
            HasExplicitAccountOrDbArgs(Environment.GetCommandLineArgs());

        public static string AccountTag
        {
            get
            {
                string stem = Path.GetFileNameWithoutExtension(DatabasePath);
                return string.IsNullOrEmpty(stem) ? "game" : SanitizeFileSegment(stem);
            }
        }

        public static bool HasExplicitAccountOrDbArgs(string[] args)
        {
            return !string.IsNullOrWhiteSpace(ReadArg(args, "--db", "-db", "--db-path", "-db-path")) ||
                   !string.IsNullOrWhiteSpace(ReadArg(args, "--user", "-user", "--user-id"));
        }

        /// <summary>
        /// Resolves the SQLCipher database path from launcher CLI arguments
        /// (--db, --db-path, --user, -user, --user-id), falling back to game.db.
        /// </summary>
        public static string ResolveDatabasePath(string[] args, string baseDir = null)
        {
            string dir = string.IsNullOrEmpty(baseDir) ? LocalDataDir : baseDir;
            string dbArg = ReadArg(args, "--db", "-db", "--db-path", "-db-path");
            if (!string.IsNullOrWhiteSpace(dbArg))
                return Path.IsPathRooted(dbArg) ? dbArg : Path.Combine(dir, dbArg);

            string userArg = ReadArg(args, "--user", "-user", "--user-id");
            if (string.IsNullOrWhiteSpace(userArg))
                return Path.Combine(dir, "game.db");

            if (userArg.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                return Path.IsPathRooted(userArg) ? userArg : Path.Combine(dir, Path.GetFileName(userArg));

            string safeUser = SanitizeFileSegment(userArg);
            if (string.IsNullOrEmpty(safeUser))
                return Path.Combine(dir, "game.db");

            string prefixed = Path.Combine(dir, $"game_{safeUser}.db");
            string direct = Path.Combine(dir, $"{safeUser}.db");
            return !File.Exists(prefixed) && File.Exists(direct) ? direct : prefixed;
        }

        static string ReadArg(string[] args, params string[] names)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.IsNullOrEmpty(a)) continue;
                foreach (string name in names)
                {
                    if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        return args[i + 1].Trim();
                    if (a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                        return a.Substring(name.Length + 1).Trim();
                }
            }
            return null;
        }

        static string SanitizeFileSegment(string raw)
        {
            string trimmed = raw.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = trimmed.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == '/' || chars[i] == '\\' || Array.IndexOf(invalid, chars[i]) >= 0)
                    chars[i] = '_';
            }
            return new string(chars).Replace("..", "_");
        }

        public static void Acquire()
        {
            string dir = Path.GetDirectoryName(LockPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            int pid = Process.GetCurrentProcess().Id;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            File.WriteAllText(LockPath, $"{{\"pid\": {pid}, \"startedAt\": {now}}}");
        }

        public static void Release()
        {
            if (File.Exists(LockPath))
                File.Delete(LockPath);
        }
    }
}

