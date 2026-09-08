using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 本地化文本格式化工具，支持位置参数和基础命名参数。
    /// </summary>
    public static class LocalizationFormatter
    {
        private static readonly Regex namedPlaceholderPattern = new Regex(
            // 排除 {{ 和 }}，让转义大括号保持为字面量，不被当成本地化占位符。
            @"(?<!\{)\{(?<name>[A-Za-z_][A-Za-z0-9_.-]*)(?<format>:[^{}]+)?\}(?!\})",
            RegexOptions.Compiled);
        private static readonly Regex positionalPlaceholderPattern = new Regex(
            @"(?<!\{)\{(?<index>[0-9]+)(?<format>:[^{}]+)?\}(?!\})",
            RegexOptions.Compiled);

        /// <summary>
        /// 使用位置参数、单个命名值或单个匿名对象格式化文本。
        /// </summary>
        public static bool TryFormat(
            string template,
            object[] arguments,
            out string value,
            out string error)
        {
            value = template ?? string.Empty;
            error = null;

            if (arguments == null || arguments.Length == 0 || string.IsNullOrEmpty(template))
                return true;

            if (arguments.Length == 1 && IsAnonymousObject(arguments[0]))
            {
                try
                {
                    var namedValues = ReadObjectValues(arguments[0]);
                    return TryFormatNamed(template, namedValues, out value, out error);
                }
                catch (Exception exception)
                {
                    value = template;
                    error = $"读取命名参数失败：{exception.Message}";
                    return false;
                }
            }

            bool handled = false;
            if (arguments.Length == 1
                && TryFormatSingleNamedValue(
                    template,
                    arguments[0],
                    out value,
                    out error,
                    out handled))
            {
                return true;
            }

            if (handled)
                return false;

            try
            {
                value = string.Format(CultureInfo.CurrentCulture, template, arguments);
                return true;
            }
            catch (FormatException exception)
            {
                error = $"位置参数格式错误：{exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// 处理“一个直接传入值对应一个命名占位符”的便捷格式化形式。
        /// </summary>
        private static bool TryFormatSingleNamedValue(
            string template,
            object argument,
            out string value,
            out string error,
            out bool handled)
        {
            value = template ?? string.Empty;
            error = null;
            handled = false;

            if (string.IsNullOrEmpty(template))
                return false;

            MatchCollection namedMatches = namedPlaceholderPattern.Matches(template);
            if (namedMatches.Count == 0)
                return false;

            // 混用位置占位符和命名占位符时，不能猜测单个值应该绑定到谁。
            if (positionalPlaceholderPattern.IsMatch(template))
            {
                handled = true;
                error = "直接传值不能混用位置占位符和命名占位符，请使用 Tuple 或匿名对象。";
                return false;
            }

            handled = true;
            if (namedMatches.Count != 1)
            {
                error =
                    $"直接传值只支持恰好一个命名占位符，当前检测到 {namedMatches.Count} 个；" +
                    "多个命名占位符请使用 Tuple 或匿名对象。";
                return false;
            }

            var namedValues = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [namedMatches[0].Groups["name"].Value] = argument
            };
            return TryFormatNamed(template, namedValues, out value, out error);
        }

        /// <summary>
        /// 使用显式命名参数格式化文本。
        /// </summary>
        public static bool TryFormat(
            string template,
            (string Name, object Value)[] arguments,
            out string value,
            out string error)
        {
            var namedValues = new Dictionary<string, object>(StringComparer.Ordinal);
            if (arguments != null)
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(arguments[i].Name))
                    {
                        value = template ?? string.Empty;
                        error = "命名参数名称不能为空。";
                        return false;
                    }

                    if (!namedValues.TryAdd(arguments[i].Name, arguments[i].Value))
                    {
                        value = template ?? string.Empty;
                        error = $"命名参数重复：{arguments[i].Name}";
                        return false;
                    }
                }
            }

            return TryFormatNamed(template, namedValues, out value, out error);
        }

        /// <summary>
        /// 使用命名参数替换模板中的命名占位符，并保留缺失参数原文。
        /// </summary>
        private static bool TryFormatNamed(
            string template,
            IReadOnlyDictionary<string, object> values,
            out string value,
            out string error)
        {
            value = template ?? string.Empty;
            error = null;

            if (string.IsNullOrEmpty(template))
                return true;

            bool success = true;
            string formatError = null;
            try
            {
                value = namedPlaceholderPattern.Replace(template, match =>
                {
                    string name = match.Groups["name"].Value;
                    if (!values.TryGetValue(name, out object argument))
                    {
                        success = false;
                        formatError = $"缺少命名参数：{name}";
                        return match.Value;
                    }

                    string format = match.Groups["format"].Success
                        ? match.Groups["format"].Value.Substring(1)
                        : null;
                    return FormatValue(argument, format);
                });
            }
            catch (Exception exception)
            {
                value = template;
                error = $"命名参数格式化失败：{exception.Message}";
                return false;
            }

            error = formatError;
            return success;
        }

        /// <summary>
        /// 使用当前区域性将单个参数转换为文本。
        /// </summary>
        private static string FormatValue(object value, string format)
        {
            if (value == null)
                return string.Empty;

            if (value is IFormattable formattable)
                return formattable.ToString(format, CultureInfo.CurrentCulture);

            return value.ToString();
        }

        /// <summary>
        /// 读取匿名对象的公共可读非索引属性，作为命名占位符的参数字典。
        /// 仅在调用方明确传入匿名对象时使用，避免反射普通业务对象。
        /// </summary>
        private static Dictionary<string, object> ReadObjectValues(object source)
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            PropertyInfo[] properties = source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var property in properties)
            {
                if (property.CanRead && property.GetIndexParameters().Length == 0)
                    values[property.Name] = property.GetValue(source, null);
            }

            return values;
        }

        /// <summary>
        /// 判断参数是否为编译器生成的匿名对象。
        /// </summary>
        private static bool IsAnonymousObject(object value)
        {
            if (value == null)
                return false;

            Type type = value.GetType();
            return Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
                && type.Name.IndexOf("AnonymousType", StringComparison.Ordinal) >= 0;
        }
    }
}
