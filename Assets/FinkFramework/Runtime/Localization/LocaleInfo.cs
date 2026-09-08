using System;
using UnityEngine;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 本地化语言信息。
    /// </summary>
    [Serializable]
    public sealed class LocaleInfo : IEquatable<LocaleInfo>
    {
        [SerializeField] private string code;
        [SerializeField] private string displayName;

        /// <summary>
        /// Unity 序列化和 Inspector 创建实例时使用的无参构造函数。
        /// </summary>
        public LocaleInfo() { }

        /// <summary>
        /// 创建一个语言信息。
        /// </summary>
        public LocaleInfo(string code, string displayName = null)
        {
            this.code = Normalize(code);
            this.displayName = displayName;
        }

        /// <summary>
        /// 标准化语言标识，例如 en-US、zh-Hans、zh-Hant。
        /// </summary>
        public string Code => code;

        /// <summary>
        /// 编辑器和语言选择界面使用的显示名称。
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 语言文件名，例如 en_us.json。
        /// </summary>
        public string FileName => ToFileName(Code);

        /// <summary>
        /// 当前语言是否通常使用从右向左的文字方向。
        /// </summary>
        public bool IsRightToLeft => IsRightToLeftCode(Code);

        /// <summary>
        /// 更新语言信息，并统一语言标识格式。
        /// </summary>
        internal void Set(string c, string display = null)
        {
            this.code = Normalize(c);
            this.displayName = display;
        }

        /// <summary>
        /// 标准化语言标识（code）。
        /// 如将 en_us、EN-us、zh_hans 转换为 en-US、zh-Hans。
        /// </summary>
        public static string Normalize(string code)
        {
            if (!TryNormalize(code, out var normalized))
                throw new ArgumentException($"无效的语言标识：{code}", nameof(code));

            return normalized;
        }

        /// <summary>
        /// 尝试标准化语言标识。支持 language、language-Script、language-REGION
        /// 以及 language-Script-REGION 四种常用 BCP 47 形式。
        /// </summary>
        public static bool TryNormalize(string code, out string normalized)
        {
            normalized = null;

            if (string.IsNullOrWhiteSpace(code))
                return false;

            string value = code.Trim().Replace('_', '-');
            string[] parts = value.Split('-');

            if (parts.Length < 1 || parts.Length > 3)
                return false;

            if (!IsLetters(parts[0], 2, 8))
                return false;

            string language = parts[0].ToLowerInvariant();

            string script = null;
            string region = null;
            int nextPartIndex = 1;

            if (nextPartIndex < parts.Length && IsLetters(parts[nextPartIndex], 4, 4))
            {
                script = char.ToUpperInvariant(parts[nextPartIndex][0])
                    + parts[nextPartIndex].Substring(1).ToLowerInvariant();
                nextPartIndex++;
            }

            if (nextPartIndex < parts.Length)
            {
                region = parts[nextPartIndex];
                if (!IsLetters(region, 2, 2) && !IsDigits(region, 3, 3))
                    return false;

                region = region.ToUpperInvariant();
                nextPartIndex++;
            }

            if (nextPartIndex != parts.Length)
                return false;

            normalized = language;
            if (!string.IsNullOrEmpty(script))
                normalized += $"-{script}";
            if (!string.IsNullOrEmpty(region))
                normalized += $"-{region}";
            return true;
        }

        /// <summary>
        /// 将标准语言标识转换为语言文件名。
        /// </summary>
        public static string ToFileName(string code)
        {
            string normalized = Normalize(code);
            return normalized.Replace('-', '_').ToLowerInvariant() + ".json";
        }

        /// <summary>
        /// 从语言文件名提取并标准化语言标识。
        /// </summary>
        public static bool TryFromFileName(string fileName, out string code)
        {
            code = null;

            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            string name = fileName.Trim();
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return false;

            name = name.Substring(0, name.Length - ".json".Length);
            return TryNormalize(name, out code);
        }

        /// <summary>
        /// 获取语言标识的基础语言，例如 zh-Hant → zh。
        /// </summary>
        public static string GetBaseLanguage(string code)
        {
            string normalized = Normalize(code);
            int separatorIndex = normalized.IndexOf('-');
            return separatorIndex < 0
                ? normalized
                : normalized.Substring(0, separatorIndex);
        }

        /// <summary>
        /// 判断语言代码是否属于常见 RTL 语言。
        /// </summary>
        public static bool IsRightToLeftCode(string code)
        {
            if (!TryNormalize(code, out string normalized))
                return false;

            string baseLanguage = GetBaseLanguage(normalized);
            string script = GetScript(normalized);
            if (string.Equals(script, "Latn", StringComparison.Ordinal)
                || string.Equals(script, "Cyrl", StringComparison.Ordinal))
                return false;

            return baseLanguage == "ar"
                || baseLanguage == "dv"
                || baseLanguage == "fa"
                || baseLanguage == "he"
                || baseLanguage == "ku"
                || baseLanguage == "ps"
                || baseLanguage == "sd"
                || baseLanguage == "ug"
                || baseLanguage == "ur"
                || baseLanguage == "yi";
        }

        /// <summary>
        /// 获取语言标识的书写系统，例如 zh-Hant → Hant。
        /// </summary>
        public static string GetScript(string code)
        {
            string normalized = Normalize(code);
            string[] parts = normalized.Split('-');
            return parts.Length > 1 && IsLetters(parts[1], 4, 4)
                ? parts[1]
                : null;
        }

        /// <summary>按不区分大小写的标准语言标识比较语言。</summary>
        public bool Equals(LocaleInfo other)
        {
            return other != null && string.Equals(Code, other.Code, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>按语言标识计算相等性哈希。</summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as LocaleInfo);
        }

        /// <summary>返回标准语言标识的哈希值。</summary>
        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Code ?? string.Empty);
        }

        /// <summary>返回显示名称；没有显示名称时返回语言标识。</summary>
        public override string ToString()
        {
            return string.IsNullOrEmpty(DisplayName) ? Code : DisplayName;
        }

        /// <summary>
        /// 判断字符串是否只包含指定长度范围内的 ASCII 字母。
        /// </summary>
        private static bool IsLetters(string value, int minLength, int maxLength)
        {
            if (value.Length < minLength || value.Length > maxLength)
                return false;

            foreach (var t in value)
            {
                if (!IsAsciiLetter(t))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 判断字符串是否只包含指定长度范围内的 ASCII 数字。
        /// </summary>
        private static bool IsDigits(string value, int minLength, int maxLength)
        {
            if (value.Length < minLength || value.Length > maxLength)
                return false;

            foreach (var t in value)
            {
                if (!IsAsciiDigit(t))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 判断字符是否为 ASCII 英文字母。
        /// </summary>
        private static bool IsAsciiLetter(char value)
        {
            return value is >= 'a' and <= 'z'
                || value is >= 'A' and <= 'Z';
        }

        /// <summary>
        /// 判断字符是否为 ASCII 数字。
        /// </summary>
        private static bool IsAsciiDigit(char value)
        {
            return value is >= '0' and <= '9';
        }
    }
}
