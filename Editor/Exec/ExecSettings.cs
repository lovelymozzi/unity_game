using System.Collections.Generic;
using UnityEngine;

namespace UnityExec
{
    /// <summary>
    /// Runtime settings for the unity-exec server.
    /// Loaded as a singleton from Resources or created with defaults.
    /// Can be overridden by creating an ExecSettings asset in the project.
    /// </summary>
    public class ExecSettings : ScriptableObject
    {
        static ExecSettings s_Instance;

        // EditorPrefs key for server enabled state (persists across sessions)
        const string PREF_KEY_ENABLED = "UnityExec_ServerEnabled";

        [Header("Server")]
        [Tooltip("Enable/disable the HTTP server. Can also be toggled via Tools menu.")]
        public bool serverEnabled = true;

        [Header("Rate Limiting")]
        [Tooltip("Maximum requests per second. 0 = unlimited.")]
        public int maxRequestsPerSecond = 10;

        [Header("Execution")]
        [Tooltip("Maximum queued exec requests. 0 = unlimited.")]
        public int maxQueuedRequests = 100;

        [Tooltip("Server-side timeout for a single exec request in seconds. 0 = unlimited.")]
        public int executionTimeoutSeconds = 30;

        [Header("Security")]
        [Tooltip("Additional namespace prefixes to allow (beyond defaults).")]
        public List<string> additionalWhitelist = new List<string>();

        [Header("Audit")]
        [Tooltip("Enable audit logging of all execution attempts.")]
        public bool enableAuditLog = true;

        [Tooltip("Max size per log file in MB before rotation. Default: 5")]
        [Range(1, 50)]
        public int auditMaxFileSizeMB = 5;

        [Tooltip("Number of rotated log files to keep (audit.log.1, .2, ...). Default: 3")]
        [Range(1, 10)]
        public int auditMaxRotatedFiles = 3;

        [Tooltip("Auto-delete log files older than N days. 0 = keep forever. Default: 30")]
        public int auditRetentionDays = 30;

        /// <summary>
        /// Whether the server is enabled. Uses EditorPrefs so it persists
        /// even without a ScriptableObject asset.
        /// </summary>
        public static bool IsServerEnabled
        {
            get => UnityEditor.EditorPrefs.GetBool(PREF_KEY_ENABLED, true);
            set => UnityEditor.EditorPrefs.SetBool(PREF_KEY_ENABLED, value);
        }

        /// <summary>
        /// Singleton accessor. Returns a default instance if no asset exists.
        /// </summary>
        public static ExecSettings Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = Resources.Load<ExecSettings>("ExecSettings");
                    if (s_Instance == null)
                    {
                        s_Instance = CreateInstance<ExecSettings>();
                        s_Instance.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
                return s_Instance;
            }
        }

        /// <summary>
        /// Reloads settings (useful after asset changes).
        /// </summary>
        public static void Reload()
        {
            s_Instance = null;
            SecurityPolicy.ReloadWhitelist();
        }
    }
}
