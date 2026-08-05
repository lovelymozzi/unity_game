using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    public static class Vector3Extensions
    {
        public static Vector3 Flatten(this Vector3 vector)
        {
            return new Vector3(vector.x, 0, vector.z);
        }
    }
}
