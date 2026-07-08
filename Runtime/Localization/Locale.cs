using System;
using System.Collections.Generic;

namespace Hwi.Foundation.Localization
{
    public static class Locale
    {
        private static readonly List<LocalizationTable> _tables = new List<LocalizationTable>();

        public static string CurrentLocale { get; private set; } = "en";
        public static event Action<string> LocaleChanged;

        public static void RegisterTable(LocalizationTable table)
        {
            if (table == null) return;
            if (!_tables.Contains(table)) _tables.Add(table);
        }

        public static void UnregisterTable(LocalizationTable table)
        {
            if (table == null) return;
            _tables.Remove(table);
        }

        public static void SetLocale(string localeCode)
        {
            if (string.IsNullOrEmpty(localeCode)) return;
            CurrentLocale = localeCode;
            LocaleChanged?.Invoke(localeCode);
        }

        public static string Get(string key, string fallback = null)
        {
            foreach (var t in _tables)
            {
                if (t == null || t.localeCode != CurrentLocale) continue;
                if (t.entries == null) continue;
                for (int i = 0; i < t.entries.Length; i++)
                {
                    if (t.entries[i].key == key) return t.entries[i].value;
                }
            }
            return fallback ?? key;
        }
    }
}
