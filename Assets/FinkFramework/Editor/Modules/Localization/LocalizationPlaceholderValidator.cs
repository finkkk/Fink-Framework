using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FinkFramework.Runtime.Localization;

namespace FinkFramework.Editor.Modules.Localization
{
    /// <summary>
    /// 检查同一个 Key 在不同语言中的占位符是否一致。
    /// </summary>
    public static class LocalizationPlaceholderValidator
    {
        private static readonly Regex PlaceholderPattern = new Regex(
            @"\{(?<name>[A-Za-z_][A-Za-z0-9_.-]*|[0-9]+)(?<format>:[^{}]+)?\}",
            RegexOptions.Compiled);

        /// <summary>
        /// 校验所有语言中同一 Key 的占位符集合是否一致，并拒绝未闭合或混用格式。
        /// </summary>
        public static bool Validate(
            IReadOnlyList<LocaleInfo> locales,
            IReadOnlyList<string> keys,
            IReadOnlyDictionary<string, Dictionary<string, string>> valuesByLocale,
            out string message)
        {
            if (locales == null || keys == null || valuesByLocale == null)
            {
                message = "占位符校验输入数据不完整。";
                return false;
            }

            var errors = new List<string>();
            foreach (string key in keys)
            {
                List<string> expected = null;
                string expectedLocale = null;

                foreach (LocaleInfo locale in locales)
                {
                    if (locale == null
                        || !LocaleInfo.TryNormalize(locale.Code, out string normalizedLocale)
                        || !valuesByLocale.TryGetValue(
                            normalizedLocale,
                            out Dictionary<string, string> localeValues)
                        || localeValues == null
                        || !localeValues.TryGetValue(key, out string value)
                        || string.IsNullOrWhiteSpace(value))
                        continue;

                    if (!TryReadSignature(value, out List<string> signature, out string parseError))
                    {
                        errors.Add($"{key}（{GetLocaleLabel(locale)}）：{parseError}");
                        continue;
                    }

                    if (expected == null)
                    {
                        expected = signature;
                        expectedLocale = GetLocaleLabel(locale);
                        continue;
                    }

                    if (!expected.SequenceEqual(signature, StringComparer.Ordinal))
                    {
                        errors.Add(
                            $"{key}：{expectedLocale} 使用 [{FormatSignature(expected)}]，" +
                            $"{GetLocaleLabel(locale)} 使用 [{FormatSignature(signature)}]。");
                    }
                }
            }

            if (errors.Count == 0)
            {
                message = null;
                return true;
            }

            var builder = new StringBuilder("发现占位符问题：\n");
            for (int i = 0; i < errors.Count && i < 8; i++)
                builder.AppendLine(errors[i]);
            if (errors.Count > 8)
                builder.AppendLine($"其余 {errors.Count - 8} 个问题已省略。");

            message = builder.ToString();
            return false;
        }

        /// <summary>
        /// 获取文本的占位符结构签名。签名只描述占位符名称和数量，不包含普通翻译内容。
        /// </summary>
        public static string GetSignature(string text)
        {
            if (!TryReadSignature(text, out List<string> signature, out _))
                return $"!invalid!{text ?? string.Empty}";

            return string.Join("\u001f", signature);
        }

        /// <summary>
        /// 提取占位符签名，并检查未闭合括号及命名/位置格式混用。
        /// </summary>
        private static bool TryReadSignature(
            string text,
            out List<string> signature,
            out string error)
        {
            signature = new List<string>();
            error = null;
            MatchCollection matches = PlaceholderPattern.Matches(text ?? string.Empty);

            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                string name = match.Groups["name"].Value;
                signature.Add(int.TryParse(name, out int index) ? $"#{index}" : name);
            }

            if (HasUnmatchedBrace(text, matches))
            {
                error = "发现无法识别或未闭合的占位符。";
                return false;
            }

            bool hasNamed = signature.Any(item => !item.StartsWith("#", StringComparison.Ordinal));
            bool hasPositional = signature.Any(item => item.StartsWith("#", StringComparison.Ordinal));
            if (hasNamed && hasPositional)
            {
                error = "暂不支持混用命名占位符和位置占位符。";
                return false;
            }

            signature.Sort(StringComparer.Ordinal);
            return true;
        }

        /// <summary>
        /// 找出没有被占位符正则覆盖的花括号字符。
        /// </summary>
        private static bool HasUnmatchedBrace(string text, MatchCollection matches)
        {
            string value = text ?? string.Empty;
            for (int index = 0; index < value.Length; index++)
            {
                // {{ 和 }} 是格式化文本中的转义字面量，不参与占位符匹配。
                if ((value[index] == '{' || value[index] == '}')
                    && index + 1 < value.Length
                    && value[index + 1] == value[index])
                {
                    index++;
                    continue;
                }

                if (value[index] != '{')
                    continue;

                if (!matches.Any(match => index >= match.Index && index < match.Index + match.Length))
                    return true;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if ((value[index] == '{' || value[index] == '}')
                    && index + 1 < value.Length
                    && value[index + 1] == value[index])
                {
                    index++;
                    continue;
                }

                if (value[index] != '}')
                    continue;

                if (!matches.Any(match => index >= match.Index && index < match.Index + match.Length))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 将占位符签名转换为用户可读的诊断文本。
        /// </summary>
        private static string FormatSignature(IReadOnlyList<string> signature)
        {
            return signature.Count == 0 ? "无" : string.Join(", ", signature);
        }

        /// <summary>
        /// 获取诊断信息中使用的语言标签。
        /// </summary>
        private static string GetLocaleLabel(LocaleInfo locale)
        {
            return LocaleCatalog.GetEditorLabel(locale);
        }
    }
}
