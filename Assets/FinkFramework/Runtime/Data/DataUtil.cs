using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FinkFramework.Odin.OdinSerializer;
using FinkFramework.Runtime.Data.JsonConverter;
using FinkFramework.Runtime.Settings;
using FinkFramework.Runtime.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace FinkFramework.Runtime.Data
{
    /// <summary>
    /// 使用 OdinSerializer 的通用数据存取工具（默认 AES 加密，可前往全局设置中关闭加密）
    /// 提供 Serialize / Deserialize / Encrypt / Decrypt。
    /// </summary>
    public static class DataUtil
    {
        #region 数据存储
        
        /// <summary>
        /// 保存对象为二进制文件（基于全局设置的加密选项自动判断是否加密）
        /// </summary>
        public static void Save<T>(string path, T data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);

            string ext = Path.GetExtension(path).ToLower();

            if (ext == ".json")
            {
                SaveJson(path, data);
                return;
            }

            if (GlobalSettings.Current.EnableEncryption)
                SaveEncrypted(path, data);
            else
                SavePlain(path, data);
        }

        /// <summary>
        /// 不加密 明文保存数据为二进制文件
        /// </summary>
        public static void SavePlain<T>(string path, T data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
                byte[] bytes = SerializationUtility.SerializeValue(data, DataFormat.Binary);
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataUtil", $"保存明文失败: {path} → {ex.Message}");
            }
        }
        
        /// <summary>
        /// AES 加密保存数据为二进制文件
        /// </summary>
        public static void SaveEncrypted<T>(string path, T data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
                // 1. 序列化对象为二进制
                byte[] bytes = SerializationUtility.SerializeValue(data, DataFormat.Binary);
                // 2. AES加密
                bytes = AESEncrypt(bytes, GlobalSettings.Current.Password);
                // 3. 写入文件
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataUtil", $"保存加密数据失败: {path} → {ex.Message}");
            }
        }

        #endregion

        #region 数据加载
        
        /// <summary>
        /// 从文件加载对象（基于全局设置的加密选项自动判断是否加密）
        /// </summary>
        public static T Load<T>(string path)
        {
            string ext = Path.GetExtension(path).ToLower();

            if (ext == ".json")
                return LoadJson<T>(path);

            if (GlobalSettings.Current.EnableEncryption)
                return LoadEncrypted<T>(path);

            return LoadPlain<T>(path);
        }

        /// <summary>
        /// 不加密 明文从文件加载对象
        /// </summary>
        public static T LoadPlain<T>(string path)
        {
            try
            {
                if (!File.Exists(path)) return default;
                byte[] bytes = File.ReadAllBytes(path);
                return SerializationUtility.DeserializeValue<T>(bytes, DataFormat.Binary);
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataUtil", $"加载明文失败: {path} → {ex.Message}");
                return default;
            }
        }
        
        /// <summary>
        /// AES 解密从文件加载对象
        /// </summary>
        public static T LoadEncrypted<T>(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    LogUtil.Warn("DataUtil", $"文件不存在: {path}");
                    return default;
                }
                // 1. 读取文件
                byte[] bytes = File.ReadAllBytes(path);
                // 2. AES解密
                bytes = AESDecrypt(bytes, GlobalSettings.Current.Password);
                // 3. 反序列化为对象
                return SerializationUtility.DeserializeValue<T>(bytes, DataFormat.Binary);
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataUtil", $"加载加密数据失败: {path} → {ex.Message}");
                return default;
            }
        }
    
        #endregion

        #region Json相关
                
        public static void SaveJson<T>(string path, T data)
        {
            try
            {
                var settings = GetJsonSettings();
                string json = JsonConvert.SerializeObject(data, settings);

                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
                File.WriteAllText(path, json);

                LogUtil.Success("DataUtil", $"JSON 保存成功：{path}");
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataUtil", $"保存 JSON 失败: {path} → {ex.Message}");
            }
        }
        
        public static T LoadJson<T>(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    LogUtil.Warn("DataUtil", $"JSON 文件不存在：{path}");
                    return default;
                }

                string json = File.ReadAllText(path);
                var settings = GetJsonSettings();

                return JsonConvert.DeserializeObject<T>(json, settings);
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataUtil", $"加载 JSON 失败: {path} → {ex.Message}");
                return default;
            }
        }

        private static JsonSerializerSettings GetJsonSettings()
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            // 注册所有 Unity 类型转换器（与你导出时完全一致）
            settings.Converters.Add(new Vector2Converter());
            settings.Converters.Add(new Vector3Converter());
            settings.Converters.Add(new Vector4Converter());
            settings.Converters.Add(new QuaternionConverter());
            settings.Converters.Add(new ColorConverter());
            settings.Converters.Add(new Matrix4x4Converter());
            settings.Converters.Add(new BoundsConverter());
            settings.Converters.Add(new RectConverter());
            settings.Converters.Add(new RectOffsetConverter());

            return settings;
        }

        #endregion

        #region AES加密解密
        
        // AES加密 盐值
        // AES加密 盐值
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("Fink_AES_Salt");
        /// <summary>
        /// AES加密
        /// </summary>
        private static byte[] AESEncrypt(byte[] data, string password)
        {
            using Aes aes = Aes.Create();
            var key = new Rfc2898DeriveBytes(password, Salt, 1000, HashAlgorithmName.SHA256);
            aes.Key = key.GetBytes(32);
            aes.IV = key.GetBytes(16);
            aes.Padding = PaddingMode.PKCS7;

            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                cs.Write(data, 0, data.Length);
            return ms.ToArray();
        }

        /// <summary>
        /// AES解密
        /// </summary>
        private static byte[] AESDecrypt(byte[] data, string password)
        {
            using Aes aes = Aes.Create();
            var key = new Rfc2898DeriveBytes(password, Salt, 1000, HashAlgorithmName.SHA256);
            aes.Key = key.GetBytes(32);
            aes.IV = key.GetBytes(16);
            aes.Padding = PaddingMode.PKCS7;

            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                cs.Write(data, 0, data.Length);
            return ms.ToArray();
        }
        
        #endregion
        
        #region 类型映射与查找
        /// <summary>
        /// 类型映射字符串名
        /// </summary>
        private static readonly Dictionary<string, Type> PrimitiveTypes = new()
        {
            { "int", typeof(int) },
            { "float", typeof(float) },
            { "double", typeof(double) },
            { "long", typeof(long) },
            { "bool", typeof(bool) },
            { "string", typeof(string) },

            { "short", typeof(short) },
            { "ushort", typeof(ushort) },
            { "byte", typeof(byte) },
            { "sbyte", typeof(sbyte) },
            { "uint", typeof(uint) },
            { "ulong", typeof(ulong) },

            { "decimal", typeof(decimal) },
            { "char", typeof(char) },

            { "DateTime", typeof(DateTime) },

            { "Vector2", typeof(Vector2) },
            { "Vector3", typeof(Vector3) },
            { "Vector4", typeof(Vector4) },
            { "Color", typeof(Color) },
            { "Matrix4x4", typeof(Matrix4x4) },
        };

        /// <summary>
        /// 根据类型名全局查找类型（兼容不同命名空间）
        /// </summary>
        public static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;
            
            // === 支持数组类型 T[] ===
            if (typeName.EndsWith("[]"))
            {
                string elemTypeName = typeName[..^2];
                Type elemType = FindType(elemTypeName);  // 递归查找元素类型
                return elemType?.MakeArrayType();     // 构造真正的数组类型 T[]
            }

            // === 自动支持泛型 ===
            if (typeName.Contains("<") && typeName.Contains(">"))
                return FindGenericType(typeName);

            // === 基础类型映射 ===
            if (PrimitiveTypes.TryGetValue(typeName, out var t))
                return t;

            // === 常见泛型定义映射 ===
            switch (typeName)
            {
                case "List":
                    return typeof(List<>);
                case "Dictionary":
                    return typeof(Dictionary<,>);
                case "HashSet":
                    return typeof(HashSet<>);
            }

            // === 尝试从反射中查找 ===
            Type type = Type.GetType(typeName);
            if (type != null) return type;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = asm.GetTypes().FirstOrDefault(x => x.Name == typeName);
                    if (type != null) return type;
                }
                catch { /* 某些动态程序集可能抛异常 */ }
            }
            return null;
        }
        
        /// <summary>
        /// 支持递归解析的泛型类型查找（如 Dictionary<string, List<GameItemData>>）
        /// </summary>
        public static Type FindGenericType(string fullName)
        {
            try
            {
                int start = fullName.IndexOf('<');
                int end = fullName.LastIndexOf('>');
                string mainTypeName = fullName[..start].Trim();
                string inner = fullName[(start + 1)..end].Trim();

                string[] args = SplitGenericArgs(inner);
                Type[] argTypes = args.Select(FindType).ToArray();

                var mainType = FindType(mainTypeName);

                if (mainType is { IsGenericTypeDefinition: true })
                    return mainType.MakeGenericType(argTypes);

                return null;
            }
            catch (Exception ex)
            {
                // 特殊处理：忽略“Value cannot be null”这种低级错误
                if (ex.Message.Contains("Value cannot be null"))
                    return null;
                // 其他类型解析问题仍然提示
                LogUtil.Warn("DataUtil", $"泛型类型解析失败: {fullName} ({ex.Message})");
                return null;
            }
        }

        /// <summary>
        /// 拆分泛型参数字符串，自动忽略嵌套尖括号
        /// </summary>
        public static string[] SplitGenericArgs(string inner)
        {
            List<string> parts = new();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c == '<') depth++;
                else if (c == '>') depth--;
                else if (c == ',' && depth == 0)
                {
                    parts.Add(inner[start..i].Trim());
                    start = i + 1;
                }
            }

            parts.Add(inner[start..].Trim());
            return parts.ToArray();
        }

        #endregion
    }
}