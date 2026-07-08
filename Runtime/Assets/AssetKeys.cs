using UnityEngine.AddressableAssets;

namespace Hwi.Foundation.Assets
{
    public static class AssetKeys
    {
        public static bool IsRegistered(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            foreach (var locator in Addressables.ResourceLocators)
            {
                if (locator.Locate(key, typeof(UnityEngine.Object), out _)) return true;
            }
            return false;
        }
    }
}
