namespace CryingSnow.FarmingIsland
{
    public static class NumericExtensions
    {
        public static string ToAbbreviatedString(this int num)
        {
            if (num >= 1_000_000_000)
                return (num / 1_000_000_000.0).ToString("0.##") + "b";
            else if (num >= 1_000_000)
                return (num / 1_000_000.0).ToString("0.##") + "m";
            else if (num >= 1_000)
                return (num / 1_000.0).ToString("0.##") + "k";
            else
                return num.ToString();
        }
    }
}
