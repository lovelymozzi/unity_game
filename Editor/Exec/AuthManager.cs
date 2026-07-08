using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace UnityExec
{
    /// <summary>
    /// Manages token-based authentication for the HTTP server.
    /// Generates a cryptographically random token on first start,
    /// stores it at ~/.unity-exec/auth-token, and validates incoming requests.
    /// </summary>
    public static class AuthManager
    {
        const int TOKEN_BYTES = 32;
        static string s_Token;
        static readonly object s_Lock = new object();

        static string ConfigDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".unity-exec");

        static string TokenPath => Path.Combine(ConfigDir, "auth-token");

        /// <summary>
        /// Ensures a token exists. Creates one if missing. Returns the token.
        /// Called once at server startup.
        /// </summary>
        public static string EnsureToken()
        {
            lock (s_Lock)
            {
                if (!string.IsNullOrEmpty(s_Token))
                    return s_Token;

                Directory.CreateDirectory(ConfigDir);

                if (File.Exists(TokenPath))
                {
                    var existing = File.ReadAllText(TokenPath).Trim();
                    if (existing.Length >= 32)
                    {
                        s_Token = existing;
                        ApplyTokenPermissions();
                        return s_Token;
                    }
                }

                s_Token = GenerateToken();
                File.WriteAllText(TokenPath, s_Token);
                ApplyTokenPermissions();

                Debug.Log($"[UnityExec] Auth token generated: {TokenPath}");
                return s_Token;
            }
        }

        /// <summary>
        /// Validates a token from an incoming request header.
        /// </summary>
        public static bool Validate(string token)
        {
            if (string.IsNullOrEmpty(s_Token) || string.IsNullOrEmpty(token))
                return false;

            // Constant-time comparison to prevent timing attacks
            return CryptographicEquals(s_Token, token.Trim());
        }

        /// <summary>
        /// Forces token regeneration (e.g. if compromised).
        /// </summary>
        public static void RegenerateToken()
        {
            lock (s_Lock)
            {
                s_Token = GenerateToken();
                Directory.CreateDirectory(ConfigDir);
                File.WriteAllText(TokenPath, s_Token);
                ApplyTokenPermissions();
                Debug.Log("[UnityExec] Auth token regenerated.");
            }
        }

        static void ApplyTokenPermissions()
        {
            // Restrict file permissions on Unix (owner read/write only)
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo("chmod", $"600 \"{TokenPath}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(info)?.WaitForExit(1000);
            }
            catch
            {
                // Windows or permission issue — skip
            }
        }

        static string GenerateToken()
        {
            var bytes = new byte[TOKEN_BYTES];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        static bool CryptographicEquals(string a, string b)
        {
            if (a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
