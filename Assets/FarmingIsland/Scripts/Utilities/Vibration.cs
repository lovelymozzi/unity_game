using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    public static class Vibration
    {
        public static void Vibrate()
        {
            if (Application.isMobilePlatform && PlayerPrefs.GetInt("Vibration ON", 1) > 0)
            {
                try
                {
                    Handheld.Vibrate();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to vibrate: {ex.Message}");
                }
            }
        }
    }
}
