using System;
using System.Collections.Generic;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// Fink Framework 本地化系统内置常用语言目录。
    /// 配置界面只从此目录提供选择，避免用户手写或新增语言代码。
    /// </summary>
    public static class LocaleCatalog
    {
        private static readonly LocaleInfo[] builtInLocales =
        {
            new LocaleInfo("en-US", "美国英语"),
            new LocaleInfo("en-GB", "英国英语"),
            new LocaleInfo("en-AU", "澳大利亚英语"),
            new LocaleInfo("en-CA", "加拿大英语"),

            new LocaleInfo("zh-Hans", "简体中文"),
            new LocaleInfo("zh-Hant", "繁体中文"),

            new LocaleInfo("ja-JP", "日语"),
            new LocaleInfo("ko-KR", "韩语"),
            new LocaleInfo("fr-FR", "法国法语"),
            new LocaleInfo("fr-CA", "加拿大法语"),
            new LocaleInfo("de-DE", "德语"),
            new LocaleInfo("es-ES", "西班牙语"),
            new LocaleInfo("es-MX", "墨西哥西班牙语"),
            new LocaleInfo("pt-BR", "巴西葡萄牙语"),
            new LocaleInfo("pt-PT", "葡萄牙葡萄牙语"),
            new LocaleInfo("it-IT", "意大利语"),

            new LocaleInfo("ru-RU", "俄语"),
            new LocaleInfo("uk-UA", "乌克兰语"),
            new LocaleInfo("tr-TR", "土耳其语"),
            new LocaleInfo("nl-NL", "荷兰语"),
            new LocaleInfo("pl-PL", "波兰语"),
            new LocaleInfo("cs-CZ", "捷克语"),
            new LocaleInfo("sk-SK", "斯洛伐克语"),
            new LocaleInfo("hu-HU", "匈牙利语"),
            new LocaleInfo("ro-RO", "罗马尼亚语"),
            new LocaleInfo("el-GR", "希腊语"),
            new LocaleInfo("da-DK", "丹麦语"),
            new LocaleInfo("sv-SE", "瑞典语"),
            new LocaleInfo("nb-NO", "挪威语"),
            new LocaleInfo("fi-FI", "芬兰语"),

            new LocaleInfo("vi-VN", "越南语"),
            new LocaleInfo("th-TH", "泰语"),
            new LocaleInfo("id-ID", "印度尼西亚语"),
            new LocaleInfo("ms-MY", "马来语"),
            new LocaleInfo("hi-IN", "印地语"),
            new LocaleInfo("bn-BD", "孟加拉语"),
            new LocaleInfo("ar-SA", "沙特阿拉伯语"),
            new LocaleInfo("ar-EG", "埃及阿拉伯语"),
            new LocaleInfo("he-IL", "希伯来语"),
            new LocaleInfo("fa-IR", "波斯语")
        };

        /// <summary>
        /// 返回内置语言目录。返回只读视图，目录顺序保持稳定。
        /// </summary>
        public static IReadOnlyList<LocaleInfo> BuiltInLocales { get; } = Array.AsReadOnly(builtInLocales);

        /// <summary>
        /// 获取语言 ID 对应的中文名称。未知语言返回空字符串。
        /// </summary>
        public static string GetChineseName(string code)
        {
            if (!LocaleInfo.TryNormalize(code, out string normalized))
                return string.Empty;

            foreach (var t in builtInLocales)
            {
                if (string.Equals(
                        t.Code,
                        normalized,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return t.DisplayName ?? string.Empty;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取编辑器中的语言显示标签：语言 ID（中文名称）。
        /// </summary>
        public static string GetEditorLabel(LocaleInfo locale)
        {
            if (locale == null)
                return string.Empty;

            string displayName = GetChineseName(locale.Code);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = locale.DisplayName;

            return string.IsNullOrWhiteSpace(displayName)
                ? locale.Code
                : $"{locale.Code}（{displayName}）";
        }

        /// <summary>
        /// 查询内置语言。返回副本，避免调用方修改目录内容。
        /// </summary>
        public static bool TryGet(string code, out LocaleInfo locale)
        {
            locale = null;
            if (!LocaleInfo.TryNormalize(code, out string normalized))
                return false;

            foreach (var candidate in builtInLocales)
            {
                if (!string.Equals(candidate.Code, normalized, StringComparison.OrdinalIgnoreCase))
                    continue;

                locale = new LocaleInfo(candidate.Code, GetChineseName(candidate.Code));
                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断语言是否来自内置目录。
        /// </summary>
        public static bool IsBuiltIn(string code)
        {
            return TryGet(code, out _);
        }
    }
}
