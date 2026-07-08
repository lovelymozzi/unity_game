using System;
using System.IO;
using System.Text;

namespace UnityExec
{
    /// <summary>
    /// Writes audit logs for all code execution attempts.
    /// Logs to ~/.unity-exec/audit.log with automatic rotation and retention.
    /// </summary>
    public static class AuditLogger
    {
        const int MAX_CODE_PREVIEW = 200;
        static readonly object s_Lock = new object();
        static bool s_CleanupDone;

        static string ConfigDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".unity-exec");

        static string LogPath => Path.Combine(ConfigDir, "audit.log");

        static string RotatedLogPath(int index) => Path.Combine(ConfigDir, $"audit.log.{index}");

        /// <summary>
        /// Logs an execution attempt.
        /// </summary>
        public static void Log(string code, string result, string detail = null)
        {
            if (!ExecSettings.Instance.enableAuditLog)
                return;

            try
            {
                lock (s_Lock)
                {
                    Directory.CreateDirectory(ConfigDir);

                    if (!s_CleanupDone)
                    {
                        PurgeExpiredLogs();
                        s_CleanupDone = true;
                    }

                    RotateIfNeeded();

                    var preview = TruncateCode(code);
                    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var sb = new StringBuilder();
                    sb.Append($"[{timestamp}] [{result}]");
                    if (!string.IsNullOrEmpty(detail))
                        sb.Append($" {detail}");
                    sb.Append($" | {preview}");
                    sb.AppendLine();

                    File.AppendAllText(LogPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Audit logging should never crash the server
            }
        }

        public static void LogSuccess(string code, string resultSummary = null)
            => Log(code, "SUCCESS", resultSummary);

        public static void LogBlocked(string code, string reason)
            => Log(code, "BLOCKED", reason);

        public static void LogError(string code, string error)
            => Log(code, "ERROR", error);

        public static void LogAuthFailure(string remoteEndpoint)
            => Log("(no code)", "AUTH_FAIL", $"from {remoteEndpoint}");

        /// <summary>
        /// Clears all audit log files. Called from Unity menu.
        /// </summary>
        public static void ClearAll()
        {
            lock (s_Lock)
            {
                try
                {
                    if (File.Exists(LogPath))
                        File.Delete(LogPath);

                    var settings = ExecSettings.Instance;
                    for (int i = 1; i <= settings.auditMaxRotatedFiles; i++)
                    {
                        var path = RotatedLogPath(i);
                        if (File.Exists(path))
                            File.Delete(path);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Returns total size of all audit log files in bytes.
        /// </summary>
        public static long GetTotalSize()
        {
            long total = 0;
            try
            {
                if (File.Exists(LogPath))
                    total += new FileInfo(LogPath).Length;

                var settings = ExecSettings.Instance;
                for (int i = 1; i <= settings.auditMaxRotatedFiles; i++)
                {
                    var path = RotatedLogPath(i);
                    if (File.Exists(path))
                        total += new FileInfo(path).Length;
                }
            }
            catch { }
            return total;
        }

        /// <summary>
        /// Returns count of log files (including main).
        /// </summary>
        public static int GetFileCount()
        {
            int count = 0;
            if (File.Exists(LogPath)) count++;
            var settings = ExecSettings.Instance;
            for (int i = 1; i <= settings.auditMaxRotatedFiles; i++)
            {
                if (File.Exists(RotatedLogPath(i))) count++;
            }
            return count;
        }

        /// <summary>
        /// Rotates audit.log → audit.log.1 → audit.log.2 → ... → delete oldest
        /// </summary>
        static void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(LogPath))
                    return;

                var settings = ExecSettings.Instance;
                var maxSize = settings.auditMaxFileSizeMB * 1024L * 1024L;
                if (new FileInfo(LogPath).Length < maxSize)
                    return;

                var maxFiles = settings.auditMaxRotatedFiles;

                // Delete the oldest
                var oldest = RotatedLogPath(maxFiles);
                if (File.Exists(oldest))
                    File.Delete(oldest);

                // Shift: .2 → .3, .1 → .2, etc.
                for (int i = maxFiles - 1; i >= 1; i--)
                {
                    var src = RotatedLogPath(i);
                    var dst = RotatedLogPath(i + 1);
                    if (File.Exists(src))
                        File.Move(src, dst);
                }

                // Current → .1
                File.Move(LogPath, RotatedLogPath(1));
            }
            catch { }
        }

        /// <summary>
        /// Deletes rotated log files older than retention period.
        /// Runs once per session on first log write.
        /// </summary>
        static void PurgeExpiredLogs()
        {
            try
            {
                var settings = ExecSettings.Instance;
                if (settings.auditRetentionDays <= 0)
                    return; // 0 = keep forever

                var cutoff = DateTime.UtcNow.AddDays(-settings.auditRetentionDays);

                for (int i = settings.auditMaxRotatedFiles; i >= 1; i--)
                {
                    var path = RotatedLogPath(i);
                    if (!File.Exists(path))
                        continue;

                    if (File.GetLastWriteTimeUtc(path) < cutoff)
                        File.Delete(path);
                }

                // Also check main log
                if (File.Exists(LogPath) && File.GetLastWriteTimeUtc(LogPath) < cutoff)
                    File.Delete(LogPath);
            }
            catch { }
        }

        static string TruncateCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return "(empty)";

            var singleLine = code.Replace("\r", "").Replace("\n", " ").Trim();
            if (singleLine.Length <= MAX_CODE_PREVIEW)
                return singleLine;

            return singleLine.Substring(0, MAX_CODE_PREVIEW) + "...";
        }
    }
}
