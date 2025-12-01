using System;
using System.Linq;
using System.Text;
using UnityEngine.Events;

namespace FinkFramework.Runtime.Utils
{
    /// <summary>
    /// 字符串工具类：拆分、数字/时间格式化等
    /// </summary>
    public static class TextsUtil
    {
        private static readonly StringBuilder resultStr = new StringBuilder("");
        
        #region 枚举
        /// <summary>
        /// 拆分字符类型
        /// </summary>
        public enum SplitType
        {
            Semicolon = 1,   // ;
            Comma = 2,       // ,
            Percent = 3,     // %
            Colon = 4,       // :
            Space = 5,       // 空格
            Pipe = 6,        // |
            Underscore = 7   // _
        }
        #endregion
        
        #region 拆分字符串相关
        /// <summary>
        /// 拆分字符串 返回字符串数组
        /// </summary>
        /// <param name="str">想要被拆分的字符串</param>
        /// <param name="type">拆分字符类型： 1-; 2-, 3-% 4-: 5-空格 6-| 7-_ </param>
        /// <returns></returns>
        public static string[] SplitStrToStrArr(string str, SplitType type = SplitType.Semicolon)
        {
            if (str == "")
                return Array.Empty<string>();
            string newStr = str;
            switch (type)
            {
                case SplitType.Semicolon:
                {
                    //为了避免英文符号填成了中文符号 我们先进行一个替换
                    while (newStr.IndexOf("；", StringComparison.Ordinal) != -1)
                        newStr = newStr.Replace("；", ";");
                    return newStr.Split(';');
                }
                case SplitType.Comma:
                {
                    //为了避免英文符号填成了中文符号 我们先进行一个替换
                    while (newStr.IndexOf("，", StringComparison.Ordinal) != -1)
                        newStr = newStr.Replace("，", ",");
                    return newStr.Split(',');
                }
                case SplitType.Percent:
                    return newStr.Split('%');
                case SplitType.Colon:
                {
                    //为了避免英文符号填成了中文符号 我们先进行一个替换
                    while (newStr.IndexOf("：", StringComparison.Ordinal) != -1)
                        newStr = newStr.Replace("：", ":");
                    return newStr.Split(':');
                }
                case SplitType.Space:
                    return newStr.Split(' ');
                case SplitType.Pipe:
                    return newStr.Split('|');
                case SplitType.Underscore:
                    return newStr.Split('_');
                default:
                    return Array.Empty<string>();
            }
        }
        /// <summary>
        /// 拆分字符串 返回整形数组
        /// </summary>
        /// <param name="str">想要被拆分的字符串</param>
        /// <param name="type">拆分字符类型： 1-; 2-, 3-% 4-: 5-空格 6-| 7-_ </param>
        /// <returns></returns>
        public static int[] SplitStrToIntArr(string str, SplitType type = SplitType.Semicolon)
        {
            //得到拆分后的字符串数组
            string[] strs = SplitStrToStrArr(str, type);
            return strs.Length == 0 ? Array.Empty<int>() :
                //把字符串数组 转换成 int数组 
                Array.ConvertAll(strs, int.Parse);
        }
        /// <summary>
        /// 专门用来拆分多组键值对形式的数据的 以int返回
        /// </summary>
        /// <param name="str">待拆分的字符串</param>
        /// <param name="typeOne">组间分隔符  1-; 2-, 3-% 4-: 5-空格 6-| 7-_ </param>
        /// <param name="typeTwo">键值对分隔符 1-; 2-, 3-% 4-: 5-空格 6-| 7-_ </param>
        /// <param name="callBack">回调函数</param>
        public static void SplitStrToIntArrTwice(string str, SplitType typeOne, SplitType typeTwo, UnityAction<int, int> callBack)
        {
            string[] strs = SplitStrToStrArr(str, typeOne);
            if (strs.Length == 0)
                return;
            foreach (var t in strs)
            {
                //拆分单个道具的ID和数量信息
                var ints = SplitStrToIntArr(t, typeTwo);
                if (ints.Length == 0)
                    continue;
                callBack.Invoke(ints[0], ints[1]);
            }
        }
        /// <summary>
        /// 专门用来拆分多组键值对形式的数据的 以string返回
        /// </summary>
        /// <param name="str">待拆分的字符串</param>
        /// <param name="typeOne">组间分隔符 1-; 2-, 3-% 4-: 5-空格 6-| 7-_ </param>
        /// <param name="typeTwo">键值对分隔符  1-; 2-, 3-% 4-: 5-空格 6-| 7-_ </param>
        /// <param name="callBack">回调函数</param>
        public static void SplitStrToStrArrTwice(string str, SplitType typeOne, SplitType typeTwo, UnityAction<string, string> callBack)
        {
            string[] strs = SplitStrToStrArr(str, typeOne);
            if (strs.Length == 0)
                return;
            foreach (var t in strs)
            {
                //拆分单个道具的ID和数量信息
                var strs2 = SplitStrToStrArr(t, typeTwo);
                if (strs2.Length == 0)
                    continue;
                callBack.Invoke(strs2[0], strs2[1]);
            }
        }
        #endregion
        
        #region 数字转字符串相关
        /// <summary>
        /// 得到指定长度的数字转字符串内容，如果长度不够会在前面补0，如果长度超出，会保留原始数值
        /// </summary>
        /// <param name="value">数值</param>
        /// <param name="len">长度</param>
        /// <returns></returns>
        public static string GetNumStr(int value, int len)
        {
            //toString中传入一个 Dn 的字符串
            //代表想要将数字转换为长度位n的字符串
            //如果长度不够 会在前面补0
            return value.ToString($"D{len}");
        }
        /// <summary>
        /// 让指定浮点数保留小数点后n位
        /// </summary>
        /// <param name="value">具体的浮点数</param>
        /// <param name="len">保留小数点后n位</param>
        /// <returns></returns>
        public static string GetDecimalStr(float value, int len)
        {
            //toString中传入一个 Fn 的字符串
            //代表想要保留小数点后几位小数
            return value.ToString($"F{len}");
        }

        /// <summary>
        /// 将较大较长的数 转换为字符串
        /// </summary>
        /// <param name="num">具体数值</param>
        /// <returns>n亿n千万 或 n万n千 或 1000 3434 234</returns>
        public static string GetBigDataToString(int num)
        {
            return num switch
            {
                //如果大于1亿 那么就显示 n亿n千万
                >= 100000000 => BigDataChange(num, 100000000, "亿", "千万"),
                //如果大于1万 那么就显示 n万n千
                >= 10000 => BigDataChange(num, 10000, "万", "千"),
                _ => num.ToString()
            };
        }

        /// <summary>
        /// 把大数据转换成对应的字符串拼接
        /// </summary>
        /// <param name="num">数值</param>
        /// <param name="company">分割单位 可以填 100000000、10000</param>
        /// <param name="bigCompany">大单位 亿、万</param>
        /// <param name="littleCompany">小单位 万、千</param>
        /// <returns></returns>
        private static string BigDataChange(int num, int company, string bigCompany, string littleCompany)
        {
            resultStr.Clear();
            //有几亿、几万
            resultStr.Append(num / company);
            resultStr.Append(bigCompany);
            //有几千万、几千
            int tmpNum = num % company;
            //看有几千万、几千
            tmpNum /= (company / 10);
            //算出来不为0
            if(tmpNum != 0)
            {
                resultStr.Append(tmpNum);
                resultStr.Append(littleCompany);
            }
            return resultStr.ToString();
        }
        #endregion

        #region 时间转换相关
        /// <summary>
        /// 秒转时分秒格式 其中时分秒可以自己传
        /// </summary>
        /// <param name="s">秒数</param>
        /// <param name="ignoreZero">是否忽略0</param>
        /// <param name="isKeepLen">是否保留至少2位（举例即1小时显示为01小时）</param>
        /// <param name="hourStr">小时的拼接字符</param>
        /// <param name="minuteStr">分钟的拼接字符</param>
        /// <param name="secondStr">秒的拼接字符</param>
        /// <returns></returns>
        public static string SecondToHMS(int s, bool ignoreZero = false, bool isKeepLen = false, string hourStr = "时", string minuteStr = "分", string secondStr = "秒")
        {
            //时间不会有负数 所以我们如果发现是负数直接归0
            if (s < 0)
                s = 0;
            //计算小时
            int hour = s / 3600;
            //计算分钟
            //除去小时后的剩余秒
            int second = s % 3600;
            //剩余秒转为分钟数
            int minute = second / 60;
            //计算秒
            second = s % 60;
            //拼接
            resultStr.Clear();
            //如果小时不为0 或者 不忽略0 
            if (hour != 0 || !ignoreZero)
            {
                resultStr.Append(isKeepLen?GetNumStr(hour, 2):hour);//具体几个小时
                resultStr.Append(hourStr);
            }
            //如果分钟不为0 或者 不忽略0 或者 小时不为0
            if(minute != 0 || !ignoreZero || hour != 0)
            {
                resultStr.Append(isKeepLen?GetNumStr(minute,2): minute);//具体几分钟
                resultStr.Append(minuteStr);
            }
            //如果秒不为0 或者 不忽略0 或者 小时和分钟不为0
            if(second != 0 || !ignoreZero || hour != 0 || minute != 0)
            {
                resultStr.Append(isKeepLen?GetNumStr(second,2): second);//具体多少秒
                resultStr.Append(secondStr);
            }

            //如果传入的参数是0秒时
            if(resultStr.Length == 0)
            {
                resultStr.Append(0);
                resultStr.Append(secondStr);
            }

            return resultStr.ToString();
        }
        
        /// <summary>
        /// 秒转00:00:00 格式
        /// </summary>
        /// <param name="s">秒数</param>
        /// <param name="ignoreZero">是否忽略0</param>
        /// <returns></returns>
        public static string SecondToHMS2(int s, bool ignoreZero = false)
        {
            return SecondToHMS(s, ignoreZero, true, ":", ":", "");
        }
        #endregion

        #region 字符串处理相关
        /// <summary>
        /// 驼峰命名转换（首字母小写）
        /// </summary>
        public static string ToCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            input = RemoveInvalidChars(input);
            return char.ToLowerInvariant(input[0]) + input[1..];
        }

        /// <summary>
        /// Pascal命名转换（首字母大写）
        /// </summary>
        public static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            input = RemoveInvalidChars(input);
            return char.ToUpperInvariant(input[0]) + input.Substring(1);
        }

        /// <summary>
        /// 过滤非法字符（仅保留字母、数字、下划线）
        /// </summary>
        public static string RemoveInvalidChars(string input)
        {
            var valid = input.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray();
            return new string(valid);
        }
        
        /// <summary>
        /// 修正中文字符
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string NormalizePunctuation(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input
                .Replace("（", "(").Replace("）", ")")
                .Replace("【", "[").Replace("】", "]")
                .Replace("「", "{").Replace("」", "}")
                .Replace("｛", "{").Replace("｝", "}")
                .Replace("，", ",").Replace("：", ":");
        }
        
        /// <summary>
        /// 修正为合法 JSON 格式：
        /// 自动替换中文符号、中文引号、补齐 key 引号、大括号。
        /// 兼容 Excel 里填写的非标准 JSON（如 HP:100,MP:50）
        /// </summary>
        public static string NormalizeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // ---------- 基础替换（中文符号） ----------
            input = input
                .Replace("（", "(").Replace("）", ")")
                .Replace("【", "[").Replace("】", "]")
                .Replace("「", "{").Replace("」", "}")
                .Replace("，", ",").Replace("：", ":")
                .Replace("“", "\"").Replace("”", "\"")
                .Replace("‘", "\"").Replace("’", "\"")
                .Trim();

            // ---------- 自动补大括号 ----------
            if (!input.StartsWith("{") && !input.StartsWith("["))
                input = "{" + input + "}";

            // ---------- 自动补引号 ----------
            // 将 {HP:100,MP:50} → {"HP":100,"MP":50}
            input = System.Text.RegularExpressions.Regex.Replace(
                input,
                @"([{,]\s*)([A-Za-z0-9_]+)(\s*:)",
                "$1\"$2\"$3");

            return input;
        }
        
        /// <summary>
        /// 对任意 Excel 读取的字符串进行全自动格式清洗：
        /// 1. 修正全角符号（中日韩括号、引号、逗号、冒号）
        /// 2. 自动补全 JSON 格式（如 HP:100 → {"HP":100}）
        /// 3. 自动修正括号类型（() {} → []）
        /// 4. 去除多余空格和换行
        /// </summary>
        public static string NormalizeDataString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // ========= 特殊处理：如果明显是时间格式，直接返回 =========
            // 例如：2025/10/26 10:00:00、2025-09-30T09:30:00、2025年8月1日15:00
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    input,
                    @"^\s*\{?\s*\d{4}[-/年]\d{1,2}[-/月]\d{1,2}[ T日]?\d{0,2}:?\d{0,2}:?\d{0,2}\s*\}?\s*$"))
            {
                // 去除多余括号但不修改内容
                return input.Trim('{', '}', ' ');
            }

            // --- 基础中文符号清洗 ---
            input = input
                .Replace("（", "(").Replace("）", ")")
                .Replace("【", "[").Replace("】", "]")
                .Replace("「", "{").Replace("」", "}")
                .Replace("｛", "{").Replace("｝", "}")
                .Replace("［", "[").Replace("］", "]")
                .Replace("“", "\"").Replace("”", "\"")
                .Replace("‘", "\"").Replace("’", "\"")
                .Replace("，", ",").Replace("：", ":")
                .Replace("；", ";")
                .Replace("、", ",")
                .Trim();

            // --- 自动替换异常括号成中括号 ---
            if (input.StartsWith("(") && input.EndsWith(")"))
                input = "[" + input.Trim('(', ')') + "]";
            if (input.StartsWith("{") && input.EndsWith("}"))
            {
                // 如果看起来更像数组而非字典（内部全是数字或逗号），也转成 []
                string inner = input.Trim('{', '}');
                if (!inner.Contains(":") && (inner.Contains(",") || inner.Contains(" ")))
                    input = "[" + inner + "]";
            }

            // --- JSON自动修复 ---
            if ((input.Contains(":") && !input.Contains("\"")) || input.Contains("“") || input.Contains("「"))
                input = NormalizeJsonString(input);

            // --- 清除多余空格、换行 ---
            input = input.Replace("\r", "").Replace("\n", "").Trim();
            return input;
        }
        #endregion
    }
}
