namespace Hwi.Foundation.Core
{
    /// <summary>Default no-op logger. Replace via <see cref="FoundationContext.Logger"/>.</summary>
    public sealed class NullFoundationLogger : IFoundationLogger
    {
        public static readonly NullFoundationLogger Instance = new NullFoundationLogger();
        private NullFoundationLogger() { }

        public void Log(string tag, string message) { }
        public void LogWarning(string tag, string message) { }
        public void LogError(string tag, string message, System.Exception ex = null) { }
    }

    /// <summary>Global access point for foundation services. Set <see cref="Logger"/> early in app startup.</summary>
    public static class FoundationContext
    {
        public static IFoundationLogger Logger { get; set; } = NullFoundationLogger.Instance;
    }
}
