using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityExec
{
    /// <summary>
    /// Secure HTTP server for executing C# code in the Unity Editor.
    /// - Token-based authentication (X-Auth-Token header)
    /// - Rate limiting
    /// - Browser origin blocking
    /// - Main thread marshaling via ConcurrentQueue + EditorApplication.update
    /// - Survives domain reloads via InitializeOnLoad
    /// </summary>
    [InitializeOnLoad]
    public static class ExecHttpServer
    {
        static HttpListener s_Listener;
        static CancellationTokenSource s_Cts;
        static int s_Port;

        // Rate limiting
        static int s_RequestCount;
        static double s_RateLimitWindowStart;
        static readonly object s_RateLimitLock = new object();

        static readonly ConcurrentQueue<WorkItem> s_Queue = new ConcurrentQueue<WorkItem>();
        static int s_QueuedRequestCount;
        static readonly object s_QueueCountLock = new object();
        static volatile bool s_IsStopping;

        struct WorkItem
        {
            public string Code;
            public string[] Usings;
            public TaskCompletionSource<object> Tcs;
            public long EnqueuedAtTimestamp;
        }

        // 큐 지연이 임계값을 넘는 첫 /exec 응답에서 한 번만 안내한다. 도메인 리로드 시 리셋.
        const long ThrottleWarnThresholdMs = 1500;
        static bool s_ThrottleWarningShown;

        // 포트는 8090부터 10개 슬롯 안에서 자동 선택. 사용자 설정 불가 — instances.json + resolve-port.sh가 진실.
        const int BasePort = 8090;
        const int MaxPortAttempts = 10;

        static ExecHttpServer()
        {
            // Unity는 AssetImportWorker·기타 배치모드 서브프로세스에도 [InitializeOnLoad]를 돌린다.
            // 워커가 서버를 띄우면 (1) 의도치 않은 포트(8091~)를 잡고 (2) instances.json을 덮어써서
            // 실제 Editor가 안 보이게 만든다. 인터랙티브 Editor에서만 동작하도록 가드.
            if (Application.isBatchMode) return;

            StartIfEnabled();
            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += StopListener;
            AssemblyReloadEvents.afterAssemblyReload += StartIfEnabled;
            EditorApplication.update += ProcessQueue;
        }

        public static int Port => s_Port;
        public static bool IsRunning => s_Listener != null;

        /// <summary>
        /// Start the server (called automatically if enabled).
        /// Can also be called manually from Tools menu.
        /// </summary>
        public static void StartServer()
        {
            ExecSettings.IsServerEnabled = true;
            Start();
        }

        /// <summary>
        /// Stop the server and disable auto-start.
        /// </summary>
        public static void StopServer()
        {
            ExecSettings.IsServerEnabled = false;
            Stop();
        }

        static void StartIfEnabled()
        {
            if (ExecSettings.IsServerEnabled)
                Start();
        }

        static void Start()
        {
            if (s_Listener != null) return;

            // Ensure auth token exists
            AuthManager.EnsureToken();

            for (var attempt = 0; attempt < MaxPortAttempts; attempt++)
            {
                var port = BasePort + attempt;
                try
                {
                    var listener = new HttpListener();
                    listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    listener.Start();

                    s_Listener = listener;
                    s_Port = port;
                    s_Cts = new CancellationTokenSource();
                    s_IsStopping = false;

                    _ = ListenLoop(s_Cts.Token);

                    // Write instance info for CLI discovery
                    WriteInstanceInfo(port);

                    // Warn about editor throttling
                    CheckEditorThrottling();

                    Debug.Log($"[UnityExec] Server started on port {port} (token auth required)");
                    return;
                }
                catch (HttpListenerException) { }
                catch (System.Net.Sockets.SocketException) { }
            }

            Debug.LogError("[UnityExec] Failed to start server — no available port");
        }

        static void StopListener()
        {
            if (s_Listener == null) return;
            s_IsStopping = true;

            s_Cts?.Cancel();
            s_Cts?.Dispose();
            s_Cts = null;

            try
            {
                s_Listener.Stop();
                s_Listener.Close();
            }
            catch { }

            DrainPendingQueue();

            s_Listener = null;
        }

        static void Stop()
        {
            var port = s_Port;
            StopListener();
            RemoveInstanceInfo();
            lock (s_RateLimitLock)
            {
                s_RequestCount = 0;
                s_RateLimitWindowStart = 0;
            }
            Debug.Log($"[UnityExec] Server stopped (was port {port})");
        }

        static void DrainPendingQueue()
        {
            while (s_Queue.TryDequeue(out var item))
            {
                try
                {
                    item.Tcs.TrySetResult(new ErrorResponse("Server stopped before execution."));
                }
                catch { }
                finally
                {
                    SafeDecrementQueuedCount();
                }
            }
        }

        static void ForceEditorUpdate()
        {
            // RepaintAllViews triggers EditorApplication.update even when unfocused,
            // but only if Interaction Mode is not set to throttling.
            try { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
            catch { }

            // EditorApplication.QueuePlayerLoopUpdate forces a player loop tick,
            // which processes EditorApplication.update callbacks regardless of
            // the editor's focus/throttle state.
            try { EditorApplication.QueuePlayerLoopUpdate(); }
            catch { }
        }

        static void CheckEditorThrottling()
        {
            // Unity 6 removed EditorSettings.focusBehavior.
            // Instead, we ensure commands execute promptly by using
            // QueuePlayerLoopUpdate() in ForceEditorUpdate() on every request.
            // Log a helpful note about the setting.
            Debug.Log(
                "[UnityExec] Tip: If CLI commands are slow when Unity is unfocused,\n" +
                "  set Unity > Settings... > General > Interaction Mode to 'No Throttling'.");
        }

        static void ProcessQueue()
        {
            while (s_Queue.TryDequeue(out var item))
                ProcessItem(item);
        }

        static void ProcessItem(WorkItem item)
        {
            try
            {
                // POST /compile 에서 큐에 넣은 컴파일 트리거 요청 처리
                if (item.Code == "__compile_trigger__")
                {
                    LogCapture.TriggerRefresh();
                    item.Tcs.TrySetResult("compilation triggered");
                    return;
                }
                if (item.Code == "__compile_trigger_full__")
                {
                    LogCapture.TriggerFullRecompile();
                    item.Tcs.TrySetResult("full recompilation triggered");
                    return;
                }

                MaybeWarnThrottling(item.EnqueuedAtTimestamp);

                var result = CsharpExecutor.Execute(item.Code, item.Usings);
                item.Tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                item.Tcs.SetResult(new ErrorResponse(ex.Message));
            }
            finally
            {
                SafeDecrementQueuedCount();
            }
        }

        static void MaybeWarnThrottling(long enqueuedAtTimestamp)
        {
            if (s_ThrottleWarningShown) return;
            if (enqueuedAtTimestamp == 0) return;

            var elapsedMs = (Stopwatch.GetTimestamp() - enqueuedAtTimestamp) * 1000L / Stopwatch.Frequency;
            if (elapsedMs < ThrottleWarnThresholdMs) return;

            s_ThrottleWarningShown = true;
            Debug.LogWarning(
                $"[unity-exec] /exec 큐 지연 {elapsedMs}ms 감지 — Editor 쓰로틀링 가능성. " +
                "Unity > Settings... > General > Interaction Mode 를 'No Throttling' 으로 변경하세요. " +
                "(미설정 시 AI/CLI 요청이 수 초~수십 초 지연될 수 있음)");
        }

        static void SafeDecrementQueuedCount()
        {
            while (true)
            {
                var current = Volatile.Read(ref s_QueuedRequestCount);
                if (current <= 0)
                    return;

                if (Interlocked.CompareExchange(ref s_QueuedRequestCount, current - 1, current) == current)
                    return;
            }
        }

        #region HTTP Handling

        static async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && s_Listener?.IsListening == true)
            {
                try
                {
                    var context = await s_Listener.GetContextAsync();
                    _ = HandleRequest(context);
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException) { break; }
            }
        }

        static async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            response.ContentType = "application/json";

            // Block CORS preflight (browser protection)
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            // Block browser requests (Origin header = browser)
            var origin = request.Headers["Origin"];
            if (origin != null)
            {
                await WriteResponse(response, 403, new ErrorResponse("Browser requests are not allowed."));
                return;
            }

            // Rate limiting
            if (!CheckRateLimit())
            {
                await WriteResponse(response, 429, new ErrorResponse("Rate limit exceeded. Try again later."));
                return;
            }

            // Route requests
            var path = request.Url.AbsolutePath;
            var method = request.HttpMethod;

            if (method == "GET" && path == "/status")
            {
                await HandleStatus(response);
                return;
            }

            if (method == "GET" && path == "/security")
            {
                // Auth required for security info
                if (!await AuthenticateRequest(request, response))
                    return;
                await HandleSecurityInfo(response);
                return;
            }

            if (method == "POST" && path == "/exec")
            {
                // Auth required for exec
                if (!await AuthenticateRequest(request, response))
                {
                    AuditLogger.LogAuthFailure(request.RemoteEndPoint?.ToString() ?? "unknown");
                    return;
                }
                await HandleExec(request, response);
                return;
            }

            if (path == "/compile")
            {
                if (!await AuthenticateRequest(request, response))
                    return;

                if (method == "GET")
                {
                    await HandleCompile(response);
                    return;
                }

                if (method == "POST")
                {
                    await HandleCompileAndWait(request, response);
                    return;
                }
            }

            if (method == "GET" && path == "/logs")
            {
                if (!await AuthenticateRequest(request, response))
                    return;
                await HandleLogs(request, response);
                return;
            }

            await WriteResponse(response, 404, new ErrorResponse($"Unknown endpoint: {method} {path}"));
        }

        static async Task<bool> AuthenticateRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            var token = request.Headers["X-Auth-Token"];
            if (AuthManager.Validate(token))
                return true;

            await WriteResponse(response, 403, new ErrorResponse("Invalid or missing auth token. Check ~/.unity-exec/auth-token"));
            return false;
        }

        static bool CheckRateLimit()
        {
            var maxRps = ExecSettings.Instance.maxRequestsPerSecond;
            if (maxRps <= 0) return true; // unlimited

            lock (s_RateLimitLock)
            {
                var now = EditorApplication.timeSinceStartup;
                if (now - s_RateLimitWindowStart >= 1.0)
                {
                    s_RateLimitWindowStart = now;
                    s_RequestCount = 0;
                }

                s_RequestCount++;
                return s_RequestCount <= maxRps;
            }
        }

        static async Task HandleStatus(HttpListenerResponse response)
        {
            var settings = ExecSettings.Instance;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var auditPath = Path.Combine(home, ".unity-exec", "audit.log");
            long auditSizeBytes = 0;
            try { if (File.Exists(auditPath)) auditSizeBytes = new FileInfo(auditPath).Length; } catch { }

            var data = new
            {
                server = "unity-exec",
                version = "1.0.0",
                port = s_Port,
                project = Application.dataPath,
                unityVersion = Application.unityVersion,
                pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                state = EditorApplication.isCompiling ? "compiling"
                    : EditorApplication.isPlaying ? "playing"
                    : "ready",
                security = new
                {
                    rateLimitPerSecond = settings.maxRequestsPerSecond,
                    whitelistCount = SecurityPolicy.GetWhitelist().Count,
                    auditLogEnabled = settings.enableAuditLog,
                    auditLogSizeBytes = auditSizeBytes,
                    maxCodeSizeBytes = SecurityPolicy.MAX_CODE_SIZE_BYTES,
                    maxQueuedRequests = settings.maxQueuedRequests,
                    executionTimeoutSeconds = settings.executionTimeoutSeconds
                },
                queue = new
                {
                    pendingExecRequests = Volatile.Read(ref s_QueuedRequestCount)
                },
            };
            await WriteResponse(response, 200, new SuccessResponse("Unity instance status", data));
        }

        static async Task HandleSecurityInfo(HttpListenerResponse response)
        {
            var data = new
            {
                whitelist = SecurityPolicy.GetWhitelist(),
                forbiddenPatterns = SecurityPolicy.GetForbiddenPatternDescriptions(),
                maxCodeSizeBytes = SecurityPolicy.MAX_CODE_SIZE_BYTES,
                rateLimitPerSecond = ExecSettings.Instance.maxRequestsPerSecond,
                auditLogEnabled = ExecSettings.Instance.enableAuditLog
            };
            await WriteResponse(response, 200, new SuccessResponse("Security policy", data));
        }

        static async Task HandleExec(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                {
                    var body = await reader.ReadToEndAsync();
                    var json = JObject.Parse(body);

                    var code = json["code"]?.Value<string>();
                    if (string.IsNullOrEmpty(code))
                    {
                        await WriteResponse(response, 400, new ErrorResponse("Missing 'code' field."));
                        return;
                    }

                    var usings = json["usings"]?.ToObject<string[]>();

                    var serverStopping = false;
                    var queueFull = false;
                    lock (s_QueueCountLock)
                    {
                        if (s_IsStopping || s_Listener == null || s_Cts == null || s_Cts.IsCancellationRequested)
                        {
                            serverStopping = true;
                        }
                        else
                        {
                            var maxQueued = ExecSettings.Instance.maxQueuedRequests;
                            if (maxQueued > 0 && s_QueuedRequestCount >= maxQueued)
                                queueFull = true;
                            else
                                s_QueuedRequestCount++;
                        }
                    }
                    if (serverStopping)
                    {
                        await WriteResponse(response, 503, new ErrorResponse("Server is stopping. Try again after it restarts."));
                        return;
                    }
                    if (queueFull)
                    {
                        await WriteResponse(response, 429, new ErrorResponse("Exec queue is full. Try again later."));
                        return;
                    }

                    // Queue for main thread execution
                    var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                    try
                    {
                        s_Queue.Enqueue(new WorkItem
                        {
                            Code = code,
                            Usings = usings,
                            Tcs = tcs,
                            EnqueuedAtTimestamp = Stopwatch.GetTimestamp(),
                        });
                    }
                    catch
                    {
                        SafeDecrementQueuedCount();
                        throw;
                    }
                    ForceEditorUpdate();

                    var timeoutSeconds = ExecSettings.Instance.executionTimeoutSeconds;
                    if (timeoutSeconds <= 0)
                    {
                        var resultNoTimeout = await tcs.Task;
                        var statusCodeNoTimeout = resultNoTimeout is ErrorResponse ? 400 : 200;
                        await WriteResponse(response, statusCodeNoTimeout, resultNoTimeout);
                        return;
                    }

                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                    var completed = await Task.WhenAny(tcs.Task, timeoutTask);
                    if (completed != tcs.Task)
                    {
                        await WriteResponse(response, 408, new ErrorResponse($"Execution timed out after {timeoutSeconds}s."));
                        return;
                    }

                    var result = await tcs.Task;
                    var statusCode = result is ErrorResponse ? 400 : 200;
                    await WriteResponse(response, statusCode, result);
                }
            }
            catch (JsonException ex)
            {
                await WriteResponse(response, 400, new ErrorResponse($"Invalid JSON: {ex.Message}"));
            }
            catch (Exception ex)
            {
                await WriteResponse(response, 500, new ErrorResponse($"Server error: {ex.Message}"));
            }
        }

        static async Task HandleCompile(HttpListenerResponse response)
        {
            var data = LogCapture.GetCompileStatus();
            await WriteResponse(response, 200, new SuccessResponse("Compilation status", data));
        }

        /// <summary>
        /// POST /compile — 메인 스레드에서 스크립트 리컴파일을 트리거하고 즉시 반환합니다.
        /// 결과는 GET /compile로 폴링하여 확인합니다.
        /// Domain reload로 HTTP 연결이 끊기므로 대기하지 않습니다.
        /// </summary>
        /// <summary>
        /// POST /compile — 메인 스레드에서 컴파일을 트리거하고 즉시 반환합니다.
        /// Body(선택): { "full": true } — true이면 전체 리컴파일, false(기본)이면 변경 감지 후 필요시만 리컴파일.
        /// 결과는 GET /compile로 폴링하여 확인합니다.
        /// </summary>
        static async Task HandleCompileAndWait(HttpListenerRequest request, HttpListenerResponse response)
        {
            bool full = false;
            try
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var body = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var json = JObject.Parse(body);
                    if (json["full"] != null)
                        full = json["full"].Value<bool>();
                }
            }
            catch { /* body 파싱 실패 시 기본값 사용 */ }

            // 메인 스레드에서 컴파일 트리거 (WorkItem 큐 활용)
            var triggerCode = full ? "__compile_trigger_full__" : "__compile_trigger__";
            var tcs = new TaskCompletionSource<object>();
            s_Queue.Enqueue(new WorkItem
            {
                Code = triggerCode,
                Tcs = tcs
            });
            ForceEditorUpdate();

            // 큐 처리 대기 (짧은 타임아웃 — 트리거만 확인)
            var timeoutTask = Task.Delay(5000);
            await Task.WhenAny(tcs.Task, timeoutTask);

            var mode = full ? "Full recompilation" : "AssetDatabase.Refresh";
            var data = new
            {
                triggered = true,
                full,
                message = $"{mode} triggered. Poll GET /compile until isCompiling=false to get results."
            };
            await WriteResponse(response, 200, new SuccessResponse("Compilation triggered", data));
        }

        static async Task HandleLogs(HttpListenerRequest request, HttpListenerResponse response)
        {
            var count = 50;
            var countParam = request.QueryString["count"];
            if (countParam != null && int.TryParse(countParam, out var c))
                count = Math.Min(500, Math.Max(1, c));

            var level = request.QueryString["level"] ?? "all";

            var data = LogCapture.GetLogs(count, level);
            await WriteResponse(response, 200, new SuccessResponse("Console logs", data));
        }

        static async Task WriteResponse(HttpListenerResponse response, int statusCode, object result)
        {
            response.StatusCode = statusCode;
            var json = JsonConvert.SerializeObject(result);
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        #endregion

        #region Instance Registry

        static string ConfigDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".unity-exec");

        static string InstancesPath => Path.Combine(ConfigDir, "instances.json");

        static void WriteInstanceInfo(int port)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);

                var instances = new JArray();
                if (File.Exists(InstancesPath))
                {
                    try
                    {
                        instances = JArray.Parse(File.ReadAllText(InstancesPath));
                    }
                    catch { instances = new JArray(); }
                }

                // Remove stale entries for this project
                var projectPath = Application.dataPath;
                var filtered = new JArray();
                foreach (var item in instances)
                {
                    if (item["project"]?.ToString() != projectPath)
                        filtered.Add(item);
                }

                filtered.Add(new JObject
                {
                    ["port"] = port,
                    ["project"] = projectPath,
                    ["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id,
                    ["version"] = Application.unityVersion,
                    ["timestamp"] = DateTime.UtcNow.ToString("o")
                });

                File.WriteAllText(InstancesPath, filtered.ToString(Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityExec] Failed to write instance info: {ex.Message}");
            }
        }

        static void RemoveInstanceInfo()
        {
            try
            {
                if (!File.Exists(InstancesPath)) return;

                var instances = JArray.Parse(File.ReadAllText(InstancesPath));
                var projectPath = Application.dataPath;
                var filtered = new JArray();

                foreach (var item in instances)
                {
                    if (item["project"]?.ToString() != projectPath)
                        filtered.Add(item);
                }

                File.WriteAllText(InstancesPath, filtered.ToString(Formatting.Indented));
            }
            catch { }
        }

        #endregion
    }
}
