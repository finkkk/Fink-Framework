using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FinkFramework.Runtime.Utils
{
    public class ParseUtil
    {
        #region 辅助函数
        
        public static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            return s.Trim('[', ']', '{', '}', '(', ')', '"', ' ');
        }
        
        public static int SafeParseInt(string s)
        {
            s = Clean(s);
            return int.TryParse(
                s,
                NumberStyles.Integer,
                Invariant,
                out var v
            ) ? v : 0;
        }
        
        public static long SafeParseLong(string s)
        {
            s = Clean(s);
            return long.TryParse(
                s,
                NumberStyles.Integer,
                Invariant,
                out var v
            ) ? v : 0L;
        }
        
        public static short SafeParseShort(string s)
        {
            s = Clean(s);
            return short.TryParse(
                s,
                NumberStyles.Integer,
                Invariant,
                out var v
            ) ? v : (short)0;
        }

        public static ushort SafeParseUShort(string s)
        {
            s = Clean(s);
            return ushort.TryParse(
                s,
                NumberStyles.Integer,
                Invariant,
                out var v
            ) ? v : (ushort)0;
        }

        public static byte SafeParseByte(string s)
        {
            s = Clean(s);
            return byte.TryParse(
                s,
                NumberStyles.Integer,
                Invariant,
                out var v
            ) ? v : (byte)0;
        }
        
        public static sbyte SafeParseSByte(string s)
        {
            s = Clean(s);
            return sbyte.TryParse(
                s,
                NumberStyles.Integer,
                Invariant,
                out var v
            ) ? v : (sbyte)0;
        }

        public static float SafeParseFloat(string s)
        {
            s = Clean(s);
            return float.TryParse(
                s,
                NumberStyles.Float,
                Invariant,
                out var v
            ) ? v : 0f;
        }

        public static double SafeParseDouble(string s)
        {
            s = Clean(s);
            return double.TryParse(
                s,
                NumberStyles.Float,
                Invariant,
                out var v
            ) ? v : 0d;
        }

        public static decimal SafeParseDecimal(string s)
        {
            s = Clean(s);
            return decimal.TryParse(
                s,
                NumberStyles.Float,
                Invariant,
                out var v
            ) ? v : 0m;
        }
        
        public static char SafeParseChar(string s)
        {
            s = Clean(s);
            return char.TryParse(s, out var v) ? v : '\0';
        }
        
        public static float ParseF(string[] arr, int index)
        {
            if (arr == null || index < 0 || index >= arr.Length)
                return 0f;

            var s = Clean(arr[index]);
            return float.TryParse(
                s,
                NumberStyles.Float,
                Invariant,
                out var f
            ) ? f : 0f;
        }
        
        public static object GetDefaultValue(string type)
        {
            switch (type)
            {
                case "string":
                    return "";
                case "int":
                    return 0;
                case "float":
                    return 0f;
                case "double":
                    return 0d;
                case "long":
                    return 0L;
                case "bool":
                    return false;
                case "Vector2":
                    return Vector2.zero;
                case "Vector3":
                    return Vector3.zero;
                case "Vector4":
                    return Vector4.zero;
                case "Color":
                    return Color.white;
                case "DateTime":
                    return DateTime.MinValue;
                case "int[]":
                    return Array.Empty<int>();
                case "float[]":
                    return Array.Empty<float>();
                case "string[]":
                    return Array.Empty<string>();
                case "List<int>":
                    return new List<int>();
                case "List<float>":
                    return new List<float>();
                case "List<string>":
                    return new List<string>();
                default:
                    return null;
            }
        }
        
        #endregion
    }
}