namespace Hwi.Foundation.Core
{
    public interface IFoundationLogger
    {
        void Log(string tag, string message);
        void LogWarning(string tag, string message);
        void LogError(string tag, string message, System.Exception ex = null);
    }
}
