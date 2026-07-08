using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityExec
{
    /// <summary>
    /// Captures Unity console logs and compilation results in a ring buffer.
    /// Provides structured access for the /logs and /compile HTTP endpoints.
    /// Thread-safe: logs can arrive from any thread via logMessageReceivedThreaded.
    /// SessionState를 사용하여 domain reload 후에도 컴파일 결과를 유지합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class LogCapture
    {
        const int MAX_LOG_ENTRIES = 500;

        // SessionState keys — domain reload 간 컴파일 결과 유지용
        const string SESSION_KEY_LAST_RESULT = "UnityExec_LastCompileResult";
        const string SESSION_KEY_LAST_TIME = "UnityExec_LastCompileTime";
        const string SESSION_KEY_ERRORS_JSON = "UnityExec_CompileErrors";
        const string SESSION_KEY_WARNINGS_JSON = "UnityExec_CompileWarnings";

        struct LogEntry
        {
            public DateTime Timestamp;
            public LogType Type;
            public string Message;
            public string StackTrace;
        }

        struct CompilerEntry
        {
            public string Message;
            public string File;
            public int Line;
            public int Column;
        }

        // Log ring buffer
        static readonly List<LogEntry> s_Entries = new List<LogEntry>();
        static readonly object s_LogLock = new object();

        // Compile state
        static readonly List<CompilerEntry> s_CompileErrors = new List<CompilerEntry>();
        static readonly List<CompilerEntry> s_CompileWarnings = new List<CompilerEntry>();
        static readonly object s_CompileLock = new object();
        static bool s_IsCompiling;
        static string s_LastResult;
        static DateTime s_LastCompileTime;

        static LogCapture()
        {
            // Domain reload 후 SessionState에서 마지막 컴파일 결과 복원
            s_LastResult = SessionState.GetString(SESSION_KEY_LAST_RESULT, "unknown");
            var savedTime = SessionState.GetString(SESSION_KEY_LAST_TIME, "");
            if (!string.IsNullOrEmpty(savedTime) && DateTime.TryParse(savedTime, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                s_LastCompileTime = parsed;

            RestoreCompilerEntriesFromSession();

            Application.logMessageReceivedThreaded += OnLogReceived;
            CompilationPipeline.compilationStarted += OnCompileStarted;
            CompilationPipeline.compilationFinished += OnCompileFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
        }

        static void OnLogReceived(string message, string stackTrace, LogType type)
        {
            lock (s_LogLock)
            {
                s_Entries.Add(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Type = type,
                    Message = message,
                    StackTrace = stackTrace
                });

                if (s_Entries.Count > MAX_LOG_ENTRIES)
                    s_Entries.RemoveRange(0, s_Entries.Count - MAX_LOG_ENTRIES);
            }
        }

        static void OnCompileStarted(object context)
        {
            lock (s_CompileLock)
            {
                s_IsCompiling = true;
                s_CompileErrors.Clear();
                s_CompileWarnings.Clear();
            }
        }

        static void OnCompileFinished(object context)
        {
            lock (s_CompileLock)
            {
                s_IsCompiling = false;
                s_LastCompileTime = DateTime.UtcNow;
                s_LastResult = s_CompileErrors.Count == 0 ? "success" : "failed";

                // SessionState에 저장하여 domain reload 후에도 유지
                SessionState.SetString(SESSION_KEY_LAST_RESULT, s_LastResult);
                SessionState.SetString(SESSION_KEY_LAST_TIME, s_LastCompileTime.ToString("o"));
                PersistCompilerEntriesToSession();
            }
        }

        static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            lock (s_CompileLock)
            {
                foreach (var msg in messages)
                {
                    var entry = new CompilerEntry
                    {
                        Message = msg.message,
                        File = msg.file,
                        Line = msg.line,
                        Column = msg.column
                    };

                    if (msg.type == CompilerMessageType.Error)
                        s_CompileErrors.Add(entry);
                    else
                        s_CompileWarnings.Add(entry);
                }
            }
        }

        /// <summary>
        /// 변경된 파일을 감지하여 필요한 경우에만 리컴파일을 트리거합니다.
        /// </summary>
        public static void TriggerRefresh()
        {
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 전체 스크립트 리컴파일을 강제합니다. 변경 유무와 관계없이 전체 빌드를 수행합니다.
        /// </summary>
        public static void TriggerFullRecompile()
        {
            CompilationPipeline.RequestScriptCompilation();
        }

        /// <summary>
        /// Returns recent log entries filtered by level.
        /// </summary>
        /// <param name="count">Max entries to return (1-500).</param>
        /// <param name="level">"all", "error", "warning", "log", "exception", "assert".</param>
        public static object GetLogs(int count, string level)
        {
            lock (s_LogLock)
            {
                IEnumerable<LogEntry> filtered = s_Entries;

                if (level != "all")
                {
                    LogType? logType = level switch
                    {
                        "error" => LogType.Error,
                        "warning" => LogType.Warning,
                        "log" => LogType.Log,
                        "exception" => LogType.Exception,
                        "assert" => LogType.Assert,
                        _ => null
                    };

                    if (logType.HasValue)
                    {
                        var lt = logType.Value;
                        filtered = filtered.Where(e => e.Type == lt);
                    }
                }

                var all = filtered.ToList();
                var recent = all.Count > count
                    ? all.GetRange(all.Count - count, count)
                    : all;

                var entries = recent.Select(e => new
                {
                    timestamp = e.Timestamp.ToString("o"),
                    level = e.Type.ToString().ToLower(),
                    message = e.Message,
                    stackTrace = string.IsNullOrEmpty(e.StackTrace) ? null : e.StackTrace
                }).ToArray();

                return new
                {
                    entries,
                    totalBuffered = s_Entries.Count,
                    returnedCount = entries.Length,
                    filter = new { level, count }
                };
            }
        }

        /// <summary>
        /// Returns current compilation status and errors/warnings from the last compile.
        /// Domain reload 후에도 SessionState에서 복원된 결과를 반환합니다.
        /// </summary>
        public static object GetCompileStatus()
        {
            lock (s_CompileLock)
            {
                return new
                {
                    isCompiling = s_IsCompiling || EditorApplication.isCompiling,
                    lastResult = s_LastResult,
                    lastCompileTime = s_LastCompileTime == default
                        ? null
                        : s_LastCompileTime.ToString("o"),
                    errors = s_CompileErrors.Select(e => new
                    {
                        message = e.Message,
                        file = e.File,
                        line = e.Line,
                        column = e.Column
                    }).ToArray(),
                    warnings = s_CompileWarnings.Select(e => new
                    {
                        message = e.Message,
                        file = e.File,
                        line = e.Line,
                        column = e.Column
                    }).ToArray(),
                    errorCount = s_CompileErrors.Count,
                    warningCount = s_CompileWarnings.Count
                };
            }
        }

        #region SessionState Persistence

        /// <summary>
        /// 컴파일 에러/경고를 SessionState에 JSON으로 저장합니다.
        /// </summary>
        static void PersistCompilerEntriesToSession()
        {
            SessionState.SetString(SESSION_KEY_ERRORS_JSON, SerializeEntries(s_CompileErrors));
            SessionState.SetString(SESSION_KEY_WARNINGS_JSON, SerializeEntries(s_CompileWarnings));
        }

        /// <summary>
        /// SessionState에서 컴파일 에러/경고를 복원합니다.
        /// </summary>
        static void RestoreCompilerEntriesFromSession()
        {
            var errorsJson = SessionState.GetString(SESSION_KEY_ERRORS_JSON, "");
            var warningsJson = SessionState.GetString(SESSION_KEY_WARNINGS_JSON, "");
            DeserializeEntries(errorsJson, s_CompileErrors);
            DeserializeEntries(warningsJson, s_CompileWarnings);
        }

        static string SerializeEntries(List<CompilerEntry> entries)
        {
            if (entries.Count == 0) return "";
            // 간단한 직렬화: message\tfile\tline\tcolumn\n 형식
            var sb = new System.Text.StringBuilder();
            foreach (var e in entries)
            {
                sb.Append(Escape(e.Message)).Append('\t');
                sb.Append(Escape(e.File)).Append('\t');
                sb.Append(e.Line).Append('\t');
                sb.Append(e.Column).Append('\n');
            }
            return sb.ToString();
        }

        static void DeserializeEntries(string data, List<CompilerEntry> target)
        {
            target.Clear();
            if (string.IsNullOrEmpty(data)) return;

            var lines = data.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('\t');
                if (parts.Length < 4) continue;

                target.Add(new CompilerEntry
                {
                    Message = Unescape(parts[0]),
                    File = Unescape(parts[1]),
                    Line = int.TryParse(parts[2], out var l) ? l : 0,
                    Column = int.TryParse(parts[3], out var c) ? c : 0
                });
            }
        }

        static string Escape(string s) =>
            s?.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n") ?? "";

        static string Unescape(string s) =>
            s?.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\") ?? "";

        #endregion
    }
}
