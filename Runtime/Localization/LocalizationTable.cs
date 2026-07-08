using System;
using UnityEngine;

namespace Hwi.Foundation.Localization
{
    [CreateAssetMenu(menuName = "HWI Foundation/Localization/Table")]
    public class LocalizationTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public string value;
        }

        public string localeCode;
        public Entry[] entries;
    }
}
