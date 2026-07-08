namespace Hwi.Foundation.Core
{
    /// <summary>Implement on services that need a one-time init pass after construction.</summary>
    public interface IInitializable
    {
        void Initialize();
    }
}
