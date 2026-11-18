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
        public static string LockPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Morsoul",
            "game.lock");

        public static string DatabasePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Morsoul",
            "game.db");

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

