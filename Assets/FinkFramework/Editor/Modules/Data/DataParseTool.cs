using System;
using System.Collections.Generic;
using System.Linq;
using FinkFramework.Runtime.Data;
using FinkFramework.Runtime.Utils;
using Newtonsoft.Json;
using UnityEngine;
using static FinkFramework.Runtime.Utils.ParseUtil;

namespace FinkFramework.Editor.Modules.Data
{
    /// <summary>
    /// 数据解析工具（DataParseTool）
    /// ------------------------------------------------------------
    /// 功能职责：
    /// 1. 将 Excel 单元格的字符串解析为实际对象；
    /// 2. 支持基础类型、数组、List、Vector2/3/4、Color、Dictionary、JSON、自定义类；
    /// </summary>
    public static class DataParseTool
    {
        /// <summary>
        /// 顶层入口
        /// </summary>
        public static ParseResult ConvertValue(object value, string type, string fieldName = "?", string tableName = "?")
        {
            // 需要返回的解析结果
            var result = new ParseResult();

            if (value == null)
            {
                result.success = true;
                result.value = GetDefaultValue(type);
                return result;
            }
            string str = value.ToString().Trim();
            type = type.Trim();
            str = TextsUtil.NormalizePunctuation(str);
            // 避免字符串 "null" 被误判为有效
            if (string.IsNullOrEmpty(str) || str.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                result.success = true ;
                result.value = GetDefaultValue(type);
                return result;
            }
            try
            { 
                object parsed;
                // ---------- 数组 ----------
                if (type.EndsWith("[]"))
                {
                    parsed =  ParseCollection(str, type[..^2], true, fieldName, tableName, result);
                }
                // ---------- List ----------
                else if (type.StartsWith("List<", StringComparison.OrdinalIgnoreCase))
                {
                    parsed =  ParseCollection(str, type[5..^1], false, fieldName, tableName, result);
                }
                // ---------- Dictionary ----------
                else if (type.StartsWith("Dictionary<", StringComparison.OrdinalIgnoreCase))
                {
                    parsed =  ParseDictionary(str, type, fieldName, tableName, result);
                }
                // ---------- 单值 ----------
                else
                {
                    parsed =  ParseSingleValue(str, type, fieldName, tableName, result);
                }
                
                result.success = true;
                result.value = parsed;
            }
            catch (Exception ex)
            {
                result.success = false;
                result.errors.Add($"[{tableName}] {fieldName} ({type}) 解析失败: {ex.Message}");
                result.value = GetDefaultValue(type);
            }
            
            return result;
        }
        
        /// <summary>
        /// 单值解析（基础类型 + Unity结构体 + JSON类 + 枚举）
        /// </summary>
        private static object ParseSingleValue(string str, string type, string fieldName, string tableName, ParseResult result)
        {
            // ===== enum 优先通道（一定要在 Normalize 之前）=====
            Type tarType = FindTypeCached(type);
            if (tarType is { IsEnum: true })
            {
                try
                {
                    // 允许两种写法：
                    // 1. "枚举内容"  (JSON string)
                    // 2. 枚举内容    (容错写法，可选)
                    string enumStr = str.Trim();

                    // 如果不是 JSON string，尝试补成 JSON string
                    if (!(enumStr.StartsWith("\"") && enumStr.EndsWith("\"")))
                        enumStr = $"\"{enumStr}\"";

                    return JsonConvert.DeserializeObject(enumStr, tarType);
                }
                catch (Exception ex)
                {
                    result.errors.Add(
                        $"[{tableName}] {fieldName} enum 解析失败 ({type}) ← '{str}' : {ex.Message}");
                    return Activator.CreateInstance(tarType);
                }
            }
            
            if (type.StartsWith("Vector", StringComparison.OrdinalIgnoreCase) || type == "Color")
            {
                str = TextsUtil.NormalizePunctuation(str);
            }
            else
            {
                str = TextsUtil.NormalizeDataString(str);
            }
            
            if (string.IsNullOrEmpty(str))
                return GetDefaultValue(type);
            try
            {
                // --- 基础类型 ---
                switch (type)
                {
                    case "string": return str;
                    case "int": return SafeParseInt(str);
                    case "long": return SafeParseLong(str);
                    case "float": return SafeParseFloat(str);
                    case "double": return SafeParseDouble(str);
                    case "short": return SafeParseShort(str);
                    case "ushort": return SafeParseUShort(str);
                    case "byte": return SafeParseByte(str);
                    case "sbyte": return SafeParseSByte(str);
                    case "decimal": return SafeParseDecimal(str);
                    case "char": return SafeParseChar(str);
                    case "bool": return str == "1" || str.Equals("true", StringComparison.OrdinalIgnoreCase);
                    case "DateTime": try { return DateTime.Parse(str); }catch (Exception ex) { throw new FormatException($"非法时间格式: '{str}'", ex); }
                }
                
                // --- Unity结构体 ---
                if (type.StartsWith("Vector", StringComparison.OrdinalIgnoreCase))
                {
                    // ===== 支持两种写法 =====
                    // ① 标准 JSON: {"x":1,"y":2} / {"x":1,"y":2,"z":3}
                    if (str.StartsWith("{") && str.EndsWith("}") && str.Contains("\"x\""))
                    {
                        try
                        {
                            return JsonConvert.DeserializeObject<Vector3>(str);
                        }
                        catch (Exception ex)
                        {
                            result.warnings.Add($"[{tableName}] JSON Vector3 解析失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        // ② 简写: (1,2,3) / {1,2,3} / [1,2,3]
                        try
                        {
                            str = str.Trim('(', ')', '{', '}', '[', ']');
                            var p = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
                            return type switch
                            {
                                "Vector2" => new Vector2(ParseF(p, 0), ParseF(p, 1)),
                                "Vector3" => new Vector3(ParseF(p, 0), ParseF(p, 1), ParseF(p, 2)),
                                "Vector4" => new Vector4(ParseF(p, 0), ParseF(p, 1), ParseF(p, 2), ParseF(p, 3)),
                                _ => Vector3.zero
                            };
                        }
                        catch (Exception ex)
                        {
                            throw new FormatException($"非法向量格式: '{str}'", ex);
                        }
                    }
                }
                
                if (type == "Color")
                {
                    // ===== 支持两种写法 =====
                    // ① 标准 JSON: {"r":1,"g":0.5,"b":0.2,"a":1}
                    if (str.StartsWith("{") && str.EndsWith("}") && str.Contains("\"r\""))
                    {
                        try { return JsonConvert.DeserializeObject<Color>(str); }
                        catch (Exception ex)
                        {
                            result.warnings.Add($"[{tableName}] {fieldName} JSON 颜色格式解析失败 ({ex.Message})");
                        }
                    }

                    // ② 简写: (1,0.5,0.2,1) / "#RRGGBB"
                    try
                    {
                        if (str.StartsWith("#") && ColorUtility.TryParseHtmlString(str, out var htmlColor))
                            return htmlColor;

                        str = str.Trim('(', ')');
                        var c = str.Split(',');
                        return new Color(ParseF(c, 0), ParseF(c, 1), ParseF(c, 2), c.Length > 3 ? ParseF(c, 3) : 1f);
                    }
                    catch (Exception ex)
                    {
                        throw new FormatException($"非法颜色格式: '{str}'", ex);
                    }
                }
                
                // --- 其余类型统一走标准 JSON ---
                if ((str.StartsWith("{") && str.EndsWith("}")) || (str.StartsWith("[") && str.EndsWith("]")))
                {
                    try
                    {
                        Type targetType = FindTypeCached(type);
                        if (targetType != null)
                        {
                            var unityObj = JsonUtility.FromJson(str, targetType);
                            return unityObj ?? JsonConvert.DeserializeObject(str, targetType);
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        result.warnings.Add($"[{tableName}] {fieldName} 非标准 JSON 格式或结构错误 ({jsonEx.Message})");
                    }
                }
                else
                {
                    // 不是 JSON，给出警告
                    result.warnings.Add($"[{tableName}] {fieldName} 期望 JSON 格式 ({type})，但输入非 JSON → '{str}'");
                }
            
            }
            catch (Exception e)
            {
                result.errors.Add($"单值解析错误! [{tableName}] {fieldName} ({type}) ← '{str}' 解析失败：{e.Message}");
                throw;
            }

            return str;
        }
        
        /// <summary>
        /// 通用集合解析函数（数组 / List 共用）
        /// ------------------------------------------------------------
        /// 执行顺序：
        /// 1. 内置特殊类型（Color、Vector）
        /// 2. 普通类型拆分 (, 分割)
        /// 3. JSON 兜底解析 ([{...}] / [1,2,3])
        /// 4. 默认返回空容器
        /// ------------------------------------------------------------
        /// 支持格式示例：
        /// - (1,2,3),(4,5,6)
        /// - #FF0000,#00FF00
        /// - [1,2,3]
        /// - [{"id":1},{"id":2}]
        /// </summary>
        private static object ParseCollection(string str, string elementType, bool asArray, string fieldName, string tableName, ParseResult result)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                var emptyType = FindTypeCached(elementType) ?? typeof(string);
                return asArray
                    ? Array.CreateInstance(emptyType, 0)
                    : (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(emptyType));
            }
            
            // 保留原始字符串副本，防止去括号破坏 JSON 格式
            string originalStr = str.Trim();
            
            // --- 去除外层括号（宽松兼容 [ ] { } ( )）---
            while ((str.StartsWith("[") && str.EndsWith("]")) ||
                   (str.StartsWith("{") && str.EndsWith("}")) ||
                   (str.StartsWith("(") && str.EndsWith(")")))
            {
                str = str.Substring(1, str.Length - 2).Trim();
            }
            var elemType = FindTypeCached(elementType) ?? typeof(string);
            // ========================
            // DateTime 特殊类型优先解析
            // ========================
            if (elementType == "DateTime")
            {
                // 去除最外层括号，不破坏内部结构
                string cleaned = originalStr;
                while ((cleaned.StartsWith("[") && cleaned.EndsWith("]")) ||
                       (cleaned.StartsWith("(") && cleaned.EndsWith(")")) ||
                       (cleaned.StartsWith("{") && cleaned.EndsWith("}")))
                {
                    cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();
                }

                var items = cleaned.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (asArray)
                {
                    var arr = new DateTime[items.Length];
                    for (int i = 0; i < items.Length; i++)
                    {
                        string s = items[i].Trim().Trim('"');
                        arr[i] = DateTime.Parse(s);
                    }
                    return arr;
                }
                else
                {
                    var list = new List<DateTime>();
                    foreach (var item in items)
                    {
                        string s = item.Trim().Trim('"');
                        list.Add(DateTime.Parse(s));
                    }
                    return list;
                }
            }
            
            // ========================
            // Color特殊类型优先解析
            // ========================
            if (elementType == "Color")
            {
                // ---------- ① JSON 数组格式 ---------- 
                if ((originalStr.StartsWith("[") && originalStr.EndsWith("]") && originalStr.Contains("\"r\"")) ||
                    (originalStr.StartsWith("[{") && originalStr.EndsWith("}]")))
                {
                    try
                    {
                        var listType = typeof(List<Color>);
                        var deserialized = JsonConvert.DeserializeObject(originalStr, listType);

                        if (deserialized is System.Collections.IList list)
                        {
                            if (asArray)
                            {
                                var arr = new Color[list.Count];
                                list.CopyTo(arr, 0);
                                return arr;
                            }
                            return list;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.warnings.Add(
                            $"[{tableName}] {fieldName} JSON Color数组解析失败 ({ex.Message})");
                    }
                }
                // 检测原始字符串是否为多组括号格式 
                if (!originalStr.Contains("#") && originalStr.Contains("(") && originalStr.Contains("),"))
                {
                    try
                    {
                        List<string> parts = new();
                        int depth = 0, start = 0;
                        string s = originalStr.Trim();

                        for (int i = 0; i < s.Length; i++)
                        {
                            char ch = s[i];
                            if (ch == '(')
                            {
                                if (depth == 0) start = i;
                                depth++;
                            }
                            else if (ch == ')')
                            {
                                depth--;
                                if (depth == 0)
                                {
                                    parts.Add(s[start..(i + 1)].Trim());
                                }
                            }
                        }

                        if (parts.Count > 0)
                        {
                            if (asArray)
                            {
                                var arr = new Color[parts.Count];
                                for (int i = 0; i < parts.Count; i++)
                                {
                                    var pr = ConvertValue(parts[i], "Color", fieldName, tableName);
                                    result.warnings.AddRange(pr.warnings);
                                    result.errors.AddRange(pr.errors);
                                    arr[i] = (Color)pr.value;
                                }
                                return arr;
                            }
                            else
                            {
                                var list = new List<Color>();
                                foreach (var p in parts)
                                {
                                    var pr = ConvertValue(p, "Color", fieldName, tableName);
                                    result.warnings.AddRange(pr.warnings);
                                    result.errors.AddRange(pr.errors);
                                    list.Add((Color)pr.value);
                                }
                                return list;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.warnings.Add($"[{tableName}] {fieldName} 多括号Color数组解析失败 ({ex.Message})");
                    }
                }
                List<string> groups = new();

                if (str.Contains("(") && str.Contains(")"))
                {
                    int depth = 0, start = 0;
                    for (int i = 0; i < str.Length; i++)
                    {
                        char ch = str[i];
                        switch (ch)
                        {
                            case '(':
                                if (depth == 0) start = i;
                                depth++;
                                break;
                            case ')':
                                depth--;
                                if (depth == 0)
                                {
                                    int end = i + 1;
                                    groups.Add(str[start..end].Trim());
                                }
                                break;
                        }
                    }
                }

                if (groups.Count == 0 && str.Contains("#"))
                {
                    var parts = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in parts) groups.Add(p.Trim());
                }

                if (groups.Count == 0)
                {
                    var parts = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in parts) groups.Add(p.Trim());
                }

                if (asArray)
                {
                    var arr = new Color[groups.Count];
                    for (int i = 0; i < groups.Count; i++)
                        arr[i] = (Color)ConvertValue(groups[i], "Color", fieldName, tableName).value;
                    return arr;
                }
                else
                {
                    var list = new List<Color>();
                    foreach (var g in groups)
                    {
                        var pr = ConvertValue(g, "Color", fieldName, tableName);
                        result.warnings.AddRange(pr.warnings);
                        result.errors.AddRange(pr.errors);
                        list.Add((Color)pr.value);
                    }
                    return list;
                }
            }
            
            // ========================
            // Vector特殊类型优先解析
            // ========================
            if (elementType is "Vector2" or "Vector3" or "Vector4")
            {
                Type vecType = FindTypeCached(elementType) ?? typeof(Vector3);

                // --- ① JSON 数组格式 ---  [{"x":1,"y":2,"z":3}]
                if ((originalStr.StartsWith("[") && originalStr.Contains("\"x\"")) || (originalStr.StartsWith("[{") && originalStr.EndsWith("}]")))
                {
                    try
                    {
                        var listType = typeof(List<>).MakeGenericType(vecType);
                        var deserialized = JsonConvert.DeserializeObject(originalStr, listType);

                        if (deserialized is System.Collections.IList list)
                        {
                            if (asArray)
                            {
                                var arr = Array.CreateInstance(vecType, list.Count);
                                list.CopyTo(arr, 0);
                                return arr;
                            }
                            return list;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.warnings.Add($"[{tableName}] {fieldName} JSON Vector解析失败: {ex.Message}");
                    }
                }

                // --- ② 简写格式 ---  (1,2,3),(4,5,6)
                List<string> groups = new();
                int depth = 0, start = -1;
                for (int i = 0; i < str.Length; i++)
                {
                    char c = str[i];
                    if (c == '(')
                    {
                        if (depth == 0) start = i;
                        depth++;
                    }
                    else if (c == ')')
                    {
                        depth--;
                        if (depth == 0 && start >= 0)
                        {
                            groups.Add(str[start..(i + 1)].Trim());
                            start = -1;
                        }
                    }
                }

                // --- 兼容 JSON 样式但无引号：{x:1,y:2,z:3},{x:4,y:5,z:6} ---
                if (groups.Count == 0 && str.Contains("{x:"))
                {
                    var jsonLike = "[" + str.Replace("}{", "},{") + "]";
                    try
                    {
                        var listType = typeof(List<>).MakeGenericType(vecType);
                        var obj = JsonConvert.DeserializeObject(jsonLike, listType);
                        if (asArray)
                        {
                            var list = (System.Collections.IList)obj;
                            var arr = Array.CreateInstance(vecType, list.Count);
                            list.CopyTo(arr, 0);
                            return arr;
                        }
                        return obj;
                    }
                    catch(Exception ex)
                    {
                        result.errors.Add(ex.Message);
                    }
                }

                // --- 容错：若未匹配任何括号，尝试用逗号拆分 ---
                if (groups.Count == 0)
                {
                    var parts = str.Split("),", StringSplitOptions.RemoveEmptyEntries)
                                   .Select(x => x.Contains('(') ? x : "(" + x + ")")
                                   .ToList();
                    groups.AddRange(parts);
                }

                // --- 转换为目标对象 ---
                var listGeneric = typeof(List<>).MakeGenericType(vecType);
                var listInstance = (System.Collections.IList)Activator.CreateInstance(listGeneric);

                foreach (var g in groups)
                {
                    try
                    {
                        listInstance.Add(ConvertValue(g, elementType, fieldName, tableName).value);
                    }
                    catch
                    {
                        listInstance.Add(Activator.CreateInstance(vecType)); // 安全填充
                    }
                }

                if (asArray)
                {
                    var arr = Array.CreateInstance(vecType, listInstance.Count);
                    listInstance.CopyTo(arr, 0);
                    return arr;
                }

                return listInstance;
            }
            
            // ========================
            // 判断类型是否是“内置基础类型”
            // ========================
            string[] primitiveTypes = { "int", "float", "double", "long", "bool", "string", "short", "ushort", "byte", "sbyte", "decimal", "char" };
            bool isPrimitive = primitiveTypes.Contains(elementType);
            // 1. 内置类型 → 宽松拆分
            if (isPrimitive)
            {
                string trimmed = str
                    .Trim('[', ']', '{', '}', '(', ')')
                    .Replace("]", "")
                    .Replace("[", "")
                    .Replace("{", "")
                    .Replace("}", "")
                    .Replace("(", "")
                    .Replace(")", "")
                    .Trim();
                var items = TextsUtil.SplitStrToStrArr(trimmed, TextsUtil.SplitType.Comma);

                if (asArray)
                {
                    var arr = Array.CreateInstance(elemType, items.Length);
                    for (int i = 0; i < items.Length; i++)
                        arr.SetValue(ConvertValue(items[i].Trim('"', ' '), elementType, fieldName, tableName).value, i);
                    return arr;
                }
                else
                {
                    var listType = typeof(List<>).MakeGenericType(elemType);
                    var list = (System.Collections.IList)Activator.CreateInstance(listType);
                    foreach (var item in items)
                        list.Add(ConvertValue(item.Trim('"', ' '), elementType, fieldName, tableName).value);
                    return list;
                }
            }
            // 2. 非内置类型 → JSON 解析
            try
            {
                // 使用未去括号前的原始字符串进行 JSON 尝试
                Type jsonType = FindTypeCached(elementType) ?? typeof(object);
                var listType = typeof(List<>).MakeGenericType(jsonType);
                var deserialized = JsonConvert.DeserializeObject(originalStr, listType);
                if (deserialized != null)
                {
                    if (asArray)
                    {
                        var list = (System.Collections.IList)deserialized;
                        var arr = Array.CreateInstance(jsonType, list.Count);
                        for (int j = 0; j < list.Count; j++)
                            arr.SetValue(list[j], j);
                        return arr;
                    }
                    else return deserialized;
                }
            }
            catch (Exception ex)
            {
                result.warnings.Add($"[{tableName}] [集合解析] {fieldName} JSON解析失败 ({ex.Message})");
            }
            // 默认空容器
            return asArray
                ? Array.CreateInstance(elemType, 0)
                : (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType));
        }

        /// <summary>
        /// 字典解析（支持 Vector/Color/嵌套类型）
        /// ------------------------------------------------------------
        /// 特性：
        /// - 优先尝试 Json.NET 泛型反序列化；
        /// — 若失败，自动回退宽松解析（ManualSplitDict）；
        /// — 支持键值对类型自动转换；
        /// — 支持嵌套 Vector、Color、自定义类等结构。
        /// </summary>
        private static object ParseDictionary(string str, string type, string fieldName, string tableName, ParseResult result)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                result.warnings.Add($"[{tableName}] [字典解析] {fieldName} JSON 为空或无效！");
                return new Dictionary<string, object>();
            }

            try
            {
                // 安全拆分 Dictionary<key,value> 内部类型
                string inner = type[11..^1];
                string[] parts = DataUtil.SplitGenericArgs(inner);  
        
                if (parts.Length != 2)
                    throw new FormatException($"非法字典定义: {type}");

                string keyType = parts[0].Trim();
                string valueType = parts[1].Trim();

                var keyTypeResolved = FindTypeCached(keyType);
                var valueTypeResolved = FindTypeCached(valueType);

                if (keyTypeResolved == null || valueTypeResolved == null)
                {
                    return new Dictionary<string, object>();
                }

                if (!str.TrimStart().StartsWith("{"))
                    throw new FormatException($"[{tableName}] {fieldName} JSON 格式不合法，期望对象 {{...}}");

                // ==== 严格模式 ====
                try
                {
                    var dictType = typeof(Dictionary<,>).MakeGenericType(keyTypeResolved, valueTypeResolved);
                    return JsonConvert.DeserializeObject(str, dictType);
                }
                catch
                {
                    // ==== 宽松模式 ====
                    var raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(str);
                    if (raw == null)
                        return new Dictionary<string, object>();

                    var res = (System.Collections.IDictionary)Activator.CreateInstance(
                        typeof(Dictionary<,>).MakeGenericType(keyTypeResolved, valueTypeResolved)
                    );

                    foreach (var kv in raw)
                    {
                        try
                        {
                            object key = keyTypeResolved == typeof(string)
                                ? kv.Key
                                : Convert.ChangeType(kv.Key, keyTypeResolved);
                            object value = ConvertValue(kv.Value, valueType, fieldName, tableName).value;
                            res[key] = value;
                        }
                        catch (Exception e)
                        {
                            result.warnings.Add($"[字典解析] 表:{tableName}  {fieldName}  键:{kv.Key} 转换失败:{e.Message}");
                        }
                    }

                    return res;
                }
            }
            catch (Exception ex)
            {
                result.warnings.Add($"[{tableName}] [字典解析] {fieldName} JSON 解析失败：{ex.Message}");
                return new Dictionary<string, object>();
            }
        }
        
        #region 类型查找缓存
        private static readonly Dictionary<string, Type> TypeCache = new();
        public static Type FindTypeCached(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
    
            if (TypeCache.TryGetValue(name, out var t))
                return t;

            try
            {
                t = DataUtil.FindType(name);
            }
            catch (Exception ex)
            {
                // 特殊处理：忽略“Value cannot be null”这种低级错误
                if (ex.Message.Contains("Value cannot be null"))
                    return null;
                // 其他类型解析问题仍然提示
                LogUtil.Warn("DataParseTool", $"类型查找失败: {name} ({ex.Message})");
                t = null;
            }

            TypeCache[name] = t;
            return t;
        }
        
        public static void ClearTypeCache()
        {
            TypeCache.Clear();
        }
        #endregion

        #region 解析结果数据结构

        public class ParseResult
        {
            public bool success;      // true:成功 false:失败
            public object value;      // 最后解析出的值
            public readonly List<string> warnings = new();
            public readonly List<string> errors = new();
        }

        #endregion
    }
}