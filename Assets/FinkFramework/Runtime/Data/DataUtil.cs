using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FinkFramework.Runtime.Config;
using FinkFramework.Runtime.Utils;
using OdinSerializer;
using UnityEngine;

namespace FinkFramework.Runtime.Data
{
    /// <summary>
    /// 使用 OdinSerializer 的通用数据存取工具（默认 AES 加密，可选关闭）
    /// 提供 Serialize / Deserialize / Encrypt / Decrypt。
    /// </summary>
    public static class DataUtil
    {
        private const DataFormat Format = DataFormat.Binary;
        // AES加密 盐值与密码
        private static readonly string Password = GlobalConfig.PASSWORD;
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("Fink_AES_Salt");

        #region 数据存储与读取
        /// <summary>
        /// 保存对象为二进制文件（默认 AES 加密，可手动关闭）
        /// </summary>
        public static void Save<T>(string path, T data, bool encrypt = true)
        {
            try
            {
                // 1. 序列化对象为二进制
                byte[] bytes = SerializationUtility.SerializeValue(data, Format);

                // 2. 可选加密
                if (encrypt)
                    bytes = AESEncrypt(bytes, Password);

                // 3. 写入文件
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataUtil", $"保存失败: {path} → {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件加载对象（默认 AES 解密，可手动关闭）
        /// </summary>
        public static T Load<T>(string path, bool decrypt = true)
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

                // 2. 可选解密
                if (decrypt)
                    bytes = AESDecrypt(bytes, Password);

                // 3. 反序列化为对象
                return SerializationUtility.DeserializeValue<T>(bytes, Format);
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataUtil", $"加载失败: {path} → {ex.Message}");
                return default;
            }
        }
        #endregion

        #region AES加密解密
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