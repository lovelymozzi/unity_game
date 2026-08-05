namespace CryingSnow.FarmingIsland
{
    public interface IProp
    {
        Location Location { get; }
        void Animate(bool instant, System.Action onFinished);
    }

    public enum Location
    {
        None,
        Farm,
        Fruit,
        Fish,
        Silo,
        Stall,
        Barn,
        Dock,
        Tower
    }
}