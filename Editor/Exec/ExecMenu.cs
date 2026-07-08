using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityExec
{
    /// <summary>
    /// Unity Editor menu items for managing the unity-exec server.
    /// Accessible via Tools > UnityExec.
    /// </summary>
    public static class ExecMenu
    {
        const string MENU_ROOT = "Tools/UnityExec/";

        // --- Server Control ---

        [MenuItem(MENU_ROOT + "Start Server", priority = 100)]
        static void StartServer()
        {
            ExecHttpServer.StartServer();
            Debug.Log("[UnityExec] Server started manually.");
        }

        [MenuItem(MENU_ROOT + "Start Server", true)]
        static bool StartServerValidate() => !ExecHttpServer.IsRunning;

        [MenuItem(MENU_ROOT + "Stop Server", priority = 101)]
        static void StopServer()
        {
            ExecHttpServer.StopServer();
            Debug.Log("[UnityExec] Server stopped manually.");
        }

        [MenuItem(MENU_ROOT + "Stop Server", true)]
        static bool StopServerValidate() => ExecHttpServer.IsRunning;

        // --- Status ---

        [MenuItem(MENU_ROOT + "Show Status", priority = 200)]
        static void ShowStatus()
        {
            var settings = ExecSettings.Instance;
            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            var configDir = Path.Combine(home, ".unity-exec");
            var tokenPath = Path.Combine(configDir, "auth-token");
            var auditPath = Path.Combine(configDir, "audit.log");
            var instancesPath = Path.Combine(configDir, "instances.json");
            var installPath = GetInstallPath();

            if (ExecHttpServer.IsRunning)
            {
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                var state = EditorApplication.isCompiling ? "compiling"
                    : EditorApplication.isPlaying ? "playing" : "ready";

                Debug.Log(
                    $"[UnityExec] ═══════════════════════════════════\n" +
                    $"  Status:       RUNNING\n" +
                    $"  ─── Server ───\n" +
                    $"  URL:          http://127.0.0.1:{ExecHttpServer.Port}\n" +
                    $"  Port:         {ExecHttpServer.Port}\n" +
                    $"  PID:          {pid}\n" +
                    $"  State:        {state}\n" +
                    $"  Version:      1.0.0\n" +
                    $"  ─── Project ───\n" +
                    $"  Project:      {Application.dataPath}\n" +
                    $"  Unity:        {Application.unityVersion}\n" +
                    $"  ─── Security ───\n" +
                    $"  Auth token:   {tokenPath} {(File.Exists(tokenPath) ? "✓" : "✗ MISSING")}\n" +
                    $"  Rate limit:   {(settings.maxRequestsPerSecond > 0 ? $"{settings.maxRequestsPerSecond} req/s" : "unlimited")}\n" +
                    $"  Whitelist:    {SecurityPolicy.GetWhitelist().Count} namespaces\n" +
                    $"  ─── Logging ───\n" +
                    $"  Audit log:    {(settings.enableAuditLog ? "enabled" : "disabled")}\n" +
                    $"  Log files:    {AuditLogger.GetFileCount()} files, {FormatSize(AuditLogger.GetTotalSize())}\n" +
                    $"  Rotation:     {settings.auditMaxFileSizeMB}MB × {settings.auditMaxRotatedFiles} files\n" +
                    $"  Retention:    {(settings.auditRetentionDays > 0 ? $"{settings.auditRetentionDays} days" : "forever")}\n" +
                    $"  Log path:     {auditPath}\n" +
                    $"  ─── CLI ───\n" +
                    $"  Shell script: {installPath} {(File.Exists(installPath) ? "✓ installed" : "✗ not installed")}\n" +
                    $"  Instances:    {instancesPath}\n" +
                    $"  ═══════════════════════════════════"
                );
            }
            else
            {
                Debug.Log(
                    $"[UnityExec] ═══════════════════════════════════\n" +
                    $"  Status:       STOPPED\n" +
                    $"  Auto-start:   {(ExecSettings.IsServerEnabled ? "enabled" : "disabled")}\n" +
                    $"  Shell script: {installPath} {(File.Exists(installPath) ? "✓ installed" : "✗ not installed")}\n" +
                    $"  ─── Use Tools > UnityExec > Start Server to start ───\n" +
                    $"  ═══════════════════════════════════"
                );
            }
        }

        // --- Security ---

        [MenuItem(MENU_ROOT + "Regenerate Auth Token", priority = 300)]
        static void RegenerateToken()
        {
            if (EditorUtility.DisplayDialog(
                "UnityExec — Regenerate Token",
                "This will invalidate the current token. All clients will need to read the new token from ~/.unity-exec/auth-token.\n\nContinue?",
                "Regenerate", "Cancel"))
            {
                AuthManager.RegenerateToken();
                Debug.Log("[UnityExec] Auth token regenerated. Update your clients.");
            }
        }

        [MenuItem(MENU_ROOT + "Copy Auth Token", priority = 301)]
        static void CopyToken()
        {
            var tokenPath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".unity-exec", "auth-token");

            if (File.Exists(tokenPath))
            {
                var token = File.ReadAllText(tokenPath).Trim();
                EditorGUIUtility.systemCopyBuffer = token;
                Debug.Log("[UnityExec] Auth token copied to clipboard.");
            }
            else
            {
                Debug.LogWarning("[UnityExec] No auth token found. Start the server first.");
            }
        }

        // --- Audit ---

        [MenuItem(MENU_ROOT + "Open Audit Log", priority = 400)]
        static void OpenAuditLog()
        {
            var logPath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".unity-exec", "audit.log");

            if (File.Exists(logPath))
            {
                EditorUtility.RevealInFinder(logPath);
            }
            else
            {
                Debug.Log("[UnityExec] No audit log found yet. Execute some code first.");
            }
        }

        [MenuItem(MENU_ROOT + "Clear Audit Logs", priority = 401)]
        static void ClearAuditLogs()
        {
            var fileCount = AuditLogger.GetFileCount();
            var totalSize = AuditLogger.GetTotalSize();

            if (fileCount == 0)
            {
                Debug.Log("[UnityExec] No audit logs to clear.");
                return;
            }

            var sizeStr = totalSize < 1024 * 1024
                ? $"{totalSize / 1024}KB"
                : $"{totalSize / (1024 * 1024.0):F1}MB";

            if (EditorUtility.DisplayDialog(
                "UnityExec — Clear Audit Logs",
                $"Delete all audit logs?\n\n  Files: {fileCount}\n  Total size: {sizeStr}",
                "Clear All", "Cancel"))
            {
                AuditLogger.ClearAll();
                Debug.Log("[UnityExec] All audit logs cleared.");
            }
        }

        // --- Install CLI ---

        const string PREF_KEY_INSTALL_PATH = "UnityExec_InstallPath";

        /// <summary>
        /// Resolves install path in priority order:
        /// 1. EditorPrefs (user's previous choice)
        /// 2. ~/.local/bin (user-writable, no sudo needed, XDG standard)
        /// 3. /usr/local/bin (system-wide fallback)
        /// </summary>
        static string GetDefaultInstallDir()
        {
            var saved = EditorPrefs.GetString(PREF_KEY_INSTALL_PATH, "");
            if (!string.IsNullOrEmpty(saved) && Directory.Exists(Path.GetDirectoryName(saved)))
                return Path.GetDirectoryName(saved);

            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            var localBin = Path.Combine(home, ".local", "bin");

            // Prefer ~/.local/bin (user-owned, no sudo)
            if (Directory.Exists(localBin))
                return localBin;

            // Check common PATH directories
            var pathDirs = (System.Environment.GetEnvironmentVariable("PATH") ?? "").Split(':');
            foreach (var candidate in new[] { localBin, "/usr/local/bin" })
            {
                foreach (var dir in pathDirs)
                {
                    if (dir.TrimEnd('/') == candidate.TrimEnd('/'))
                        return candidate;
                }
            }

            return "/usr/local/bin";
        }

        static string GetInstallPath()
        {
            var saved = EditorPrefs.GetString(PREF_KEY_INSTALL_PATH, "");
            if (!string.IsNullOrEmpty(saved))
                return saved;
            return Path.Combine(GetDefaultInstallDir(), "unity-exec");
        }

        [MenuItem(MENU_ROOT + "Install Shell Script", priority = 450)]
        static void InstallShellScript()
        {
            var scriptSrc = FindShellScriptSource();
            if (scriptSrc == null)
            {
                Debug.LogError("[UnityExec] unity-exec.sh not found in package.");
                return;
            }

            var defaultDir = GetDefaultInstallDir();
            var defaultPath = Path.Combine(defaultDir, "unity-exec");

            // Let user confirm or change the install path
            var installPath = EditorUtility.SaveFilePanel(
                "Install unity-exec — Choose location",
                defaultDir,
                "unity-exec",
                "");

            if (string.IsNullOrEmpty(installPath))
                return; // cancelled

            // Remember choice
            EditorPrefs.SetString(PREF_KEY_INSTALL_PATH, installPath);

            try
            {
                var installDir = Path.GetDirectoryName(installPath);

                if (!Directory.Exists(installDir))
                    Directory.CreateDirectory(installDir);

                if (IsWritable(installDir))
                {
                    File.Copy(scriptSrc, installPath, overwrite: true);
                    SetExecutable(installPath);
                }
                else
                {
                    // Need elevated permission (macOS osascript sudo)
                    var tempPath = Path.Combine(Path.GetTempPath(), "unity-exec");
                    File.Copy(scriptSrc, tempPath, overwrite: true);

                    var cmd = $"cp \\\"{tempPath}\\\" \\\"{installPath}\\\" && chmod +x \\\"{installPath}\\\"";
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "osascript",
                            Arguments = $"-e 'do shell script \"{cmd}\" with administrator privileges'",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    process.WaitForExit(30000);

                    if (File.Exists(tempPath))
                        File.Delete(tempPath);

                    if (process.ExitCode != 0)
                    {
                        var err = process.StandardError.ReadToEnd();
                        Debug.LogError($"[UnityExec] Install failed: {err}");
                        return;
                    }
                }

                // Check if install dir is in PATH
                var inPath = IsInPath(installDir);
                var message = $"Installed to:\n  {installPath}\n\nUsage:\n  unity-exec \"Application.dataPath\"";
                if (!inPath)
                {
                    message += $"\n\n⚠️ {installDir} is not in your PATH.\nAdd this to ~/.zshrc or ~/.bashrc:\n  export PATH=\"{installDir}:$PATH\"";
                }

                Debug.Log($"[UnityExec] Shell script installed to {installPath}" +
                    (inPath ? "" : $" (WARNING: {installDir} is not in PATH)"));
                EditorUtility.DisplayDialog("UnityExec", message, "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UnityExec] Install failed: {ex.Message}");
            }
        }

        [MenuItem(MENU_ROOT + "Uninstall Shell Script", priority = 451)]
        static void UninstallShellScript()
        {
            var installPath = GetInstallPath();
            if (!File.Exists(installPath))
            {
                Debug.Log($"[UnityExec] Shell script not found at {installPath}.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "UnityExec — Uninstall Shell Script",
                $"Remove {installPath}?",
                "Uninstall", "Cancel"))
                return;

            try
            {
                var installDir = Path.GetDirectoryName(installPath);

                if (IsWritable(installDir))
                {
                    File.Delete(installPath);
                }
                else
                {
                    var cmd = $"rm \\\"{installPath}\\\"";
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "osascript",
                            Arguments = $"-e 'do shell script \"{cmd}\" with administrator privileges'",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    process.WaitForExit(30000);
                }

                EditorPrefs.DeleteKey(PREF_KEY_INSTALL_PATH);
                Debug.Log($"[UnityExec] Shell script uninstalled from {installPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UnityExec] Uninstall failed: {ex.Message}");
            }
        }

        [MenuItem(MENU_ROOT + "Uninstall Shell Script", true)]
        static bool UninstallShellScriptValidate() => File.Exists(GetInstallPath());

        static string FindShellScriptSource()
        {
            // Find unity-exec.sh relative to this package
            var guids = AssetDatabase.FindAssets("unity-exec t:DefaultAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("unity-exec.sh"))
                    return Path.GetFullPath(path);
            }

            // Fallback: known location
            var fallback = Path.GetFullPath("Assets/modules/unity-exec/unity-exec.sh");
            return File.Exists(fallback) ? fallback : null;
        }

        static bool IsWritable(string dir)
        {
            try
            {
                var testFile = Path.Combine(dir, $".unity_exec_write_test_{System.Guid.NewGuid():N}");
                File.WriteAllText(testFile, "");
                File.Delete(testFile);
                return true;
            }
            catch { return false; }
        }

        static void SetExecutable(string path)
        {
            try
            {
                var p = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo("chmod", $"+x \"{path}\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                p.Start();
                p.WaitForExit(5000);
            }
            catch { }
        }

        static bool IsInPath(string dir)
        {
            var pathDirs = (System.Environment.GetEnvironmentVariable("PATH") ?? "").Split(':');
            var normalized = dir.TrimEnd('/');
            foreach (var d in pathDirs)
            {
                if (d.TrimEnd('/') == normalized)
                    return true;
            }
            return false;
        }

        static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes}B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024}KB";
            return $"{bytes / (1024 * 1024.0):F1}MB";
        }

        // --- Settings ---

        [MenuItem(MENU_ROOT + "Open Settings", priority = 500)]
        static void OpenSettings()
        {
            var asset = Resources.Load<ExecSettings>("ExecSettings");
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            else
            {
                if (EditorUtility.DisplayDialog(
                    "UnityExec — No Settings Asset",
                    "No ExecSettings asset found in Resources/. Create one?\n\n" +
                    "Without it, default settings are used (port 8090, rate limit 10/s).",
                    "Create", "Use Defaults"))
                {
                    CreateSettingsAsset();
                }
            }
        }

        static void CreateSettingsAsset()
        {
            var dir = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var asset = ScriptableObject.CreateInstance<ExecSettings>();
            AssetDatabase.CreateAsset(asset, $"{dir}/ExecSettings.asset");
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log("[UnityExec] ExecSettings asset created at Assets/Resources/ExecSettings.asset");
        }
    }
}
