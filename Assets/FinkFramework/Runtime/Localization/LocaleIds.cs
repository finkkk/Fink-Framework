namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 框架内置语言 ID 常量。
    ///
    /// 语言 ID 仍由 LocalizationSettingsAsset 决定是否启用；这些常量只负责提供
    /// 可补全、不会拼写错误的代码入口，不会生成额外的 C# 文件。
    /// </summary>
    public static class LocaleIds
    {
        public const string EnglishUS = "en-US";
        public const string EnglishGB = "en-GB";
        public const string EnglishAU = "en-AU";
        public const string EnglishCA = "en-CA";

        public const string ChineseSimplified = "zh-Hans";
        public const string ChineseTraditional = "zh-Hant";

        // 常用短名称，便于按钮脚本直接使用。
        public const string EnUS = EnglishUS;
        public const string ZhHans = ChineseSimplified;

        public const string Japanese = "ja-JP";
        public const string Korean = "ko-KR";
        public const string FrenchFrance = "fr-FR";
        public const string FrenchCanada = "fr-CA";
        public const string German = "de-DE";
        public const string SpanishSpain = "es-ES";
        public const string SpanishMexico = "es-MX";
        public const string PortugueseBrazil = "pt-BR";
        public const string PortuguesePortugal = "pt-PT";
        public const string Italian = "it-IT";

        public const string Russian = "ru-RU";
        public const string Ukrainian = "uk-UA";
        public const string Turkish = "tr-TR";
        public const string Dutch = "nl-NL";
        public const string Polish = "pl-PL";
        public const string Czech = "cs-CZ";
        public const string Slovak = "sk-SK";
        public const string Hungarian = "hu-HU";
        public const string Romanian = "ro-RO";
        public const string Greek = "el-GR";
        public const string Danish = "da-DK";
        public const string Swedish = "sv-SE";
        public const string Norwegian = "nb-NO";
        public const string Finnish = "fi-FI";

        public const string Vietnamese = "vi-VN";
        public const string Thai = "th-TH";
        public const string Indonesian = "id-ID";
        public const string Malay = "ms-MY";
        public const string Hindi = "hi-IN";
        public const string Bengali = "bn-BD";
        public const string ArabicSaudiArabia = "ar-SA";
        public const string ArabicEgypt = "ar-EG";
        public const string Hebrew = "he-IL";
        public const string Persian = "fa-IR";
    }
}
