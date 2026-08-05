using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.FarmingIsland
{
    public static class EnumerableExtensions
    {
        private static Random random = new Random();

        public static T SelectRandom<T>(this IEnumerable<T> source)
        {
            if (source == null || !source.Any())
            {
                throw new InvalidOperationException("Source cannot be null or empty!");
            }

            var list = source.ToList();
            int randomIndex = random.Next(list.Count);
            return list[randomIndex];
        }
    }
}
