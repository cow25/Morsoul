using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Morsoul.Core
{
    /// <summary>
    /// Reads the 256-bit SQLCipher database key from Windows Credential Manager
    /// (target: "morsoul", generic credential).
    /// </summary>
    public static class KeyStoreReader
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
        static extern void CredFree(IntPtr credential);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct CREDENTIAL
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string Comment;
            public long LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }
#endif

        public static string GetDatabaseHexKey()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // 1 = CRED_TYPE_GENERIC
            string[] targets = { "morsoul:db-key", "db-key@morsoul", "morsoul" };
            foreach (string target in targets)
            {
                if (CredRead(target, 1, 0, out IntPtr credPtr))
                {
                    try
                    {
                        var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                        if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize <= 0)
                            continue;
                        byte[] bytes = new byte[cred.CredentialBlobSize];
                        Marshal.Copy(cred.CredentialBlob, bytes, 0, cred.CredentialBlobSize);
                        string key = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
                        if (IsValidHex64(key)) return key;
                        string utf8Key = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                        if (IsValidHex64(utf8Key)) return utf8Key;
                        if (!string.IsNullOrEmpty(key)) return key;
                    }
                    finally
                    {
                        CredFree(credPtr);
                    }
                }
            }
#endif
            return null;
        }

        static bool IsValidHex64(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 64) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }
            return true;
        }
    }
}

