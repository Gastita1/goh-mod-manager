using System;
using System.Collections.Generic;
using System.Globalization;

namespace GoHMM
{
    // 界面文案：以中文原文为 key，查表得到当前语言；查不到就回退中文
    public static class L
    {
        public static readonly string[] Codes = { "zh", "en", "ja", "ko", "es", "fr", "de", "ru" };

        public static string Code = "zh";

        static readonly Dictionary<string, string[]> table =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        public static string NativeName(string code)
        {
            switch (code)
            {
                case "zh": return "简体中文";
                case "en": return "English";
                case "ja": return "日本語";
                case "ko": return "한국어";
                case "es": return "Español";
                case "fr": return "Français";
                case "de": return "Deutsch";
                case "ru": return "Русский";
                default: return code;
            }
        }

        public static string ShortName(string code)
        {
            switch (code)
            {
                case "zh": return "中文";
                case "en": return "EN";
                case "ja": return "日本語";
                case "ko": return "한국어";
                case "es": return "ES";
                case "fr": return "FR";
                case "de": return "DE";
                case "ru": return "RU";
                default: return code.ToUpper();
            }
        }

        static int Index(string code)
        {
            for (int i = 0; i < Codes.Length; i++) if (Codes[i] == code) return i;
            return 0;
        }

        // 首次启动时按系统语言猜一个
        public static string Detect()
        {
            try
            {
                string n = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
                if (n == "zh") return "zh";
                if (n == "ja") return "ja";
                if (n == "ko") return "ko";
                if (n == "es") return "es";
                if (n == "fr") return "fr";
                if (n == "de") return "de";
                if (n == "ru") return "ru";
                return "en";
            }
            catch { return "zh"; }
        }

        public static string T(string zh)
        {
            if (Code == "zh" || string.IsNullOrEmpty(zh)) return zh;
            string[] row;
            if (!table.TryGetValue(zh, out row)) return zh;
            int i = Index(Code);
            if (i <= 0 || i - 1 >= row.Length) return zh;
            string v = row[i - 1];
            return string.IsNullOrEmpty(v) ? zh : v;
        }

        public static string F(string zh, params object[] args)
        {
            try { return string.Format(T(zh), args); }
            catch { return zh; }
        }

        // 注册翻译：zh, en, ja, ko, es, fr, de, ru
        static void Add(string zh, string en, string ja, string ko, string es, string fr, string de, string ru)
        {
            table[zh] = new string[] { en, ja, ko, es, fr, de, ru };
        }

        public static void LoadTable()
        {
            LocalizationData.Register(Add);
        }
    }
}
