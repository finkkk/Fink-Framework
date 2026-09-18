using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Serialization;
using FinkFramework.Odin.OdinSerializer;
using FinkFramework.Runtime.Data.JsonConverter;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Save;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;
using Newtonsoft.Json;
using UnityEngine;
// ReSharper disable InvalidXmlDocComment

namespace FinkFramework.Runtime.Data
{
    /// <summary>
    /// 使用 OdinSerializer 的通用数据存取工具（默认 AES 加密，可前往全局设置中关闭加密）
    /// 提供 Serialize / Deserialize / Encrypt / Decrypt。
    /// </summary>
    public static class DataUtil
    {
        // JSON 配置只读复用，避免每次读写数据时重复创建转换器。
        private static readonly JsonSerializerSettings CachedJsonSettings = CreateJsonSettings();
        private static readonly JsonSerializerSettings SaveJsonSettings =
            SaveSchema.CreateJsonSettings(CreateUnityConverters());
        private static readonly ISerializationPolicy SaveSerializationPolicy =
            new CustomSerializationPolicy(
                "FinkFramework.SaveData",
                true,
                member => member switch
                {
                    FieldInfo field => field.IsPublic && !field.IsStatic &&
                                       !field.IsDefined(typeof(NonSerializedAttribute), true) &&
                                       !field.IsDefined(typeof(JsonIgnoreAttribute), true),
                    PropertyInfo property => property.GetIndexParameters().Length == 0 &&
                                             property.GetMethod?.IsPublic == true &&
                                             property.SetMethod?.IsPublic == true &&
                                             !property.IsDefined(typeof(JsonIgnoreAttribute), true),
                    _ => false
                });

        /// <summary>
        /// 将存档对象编码为 JSON UTF-8 或 Odin Binary 字节，并可选择 AES 加密。
        /// 本方法不执行文件 IO、校验或备份；完整存档流程通常应调用 <see cref="SaveManager"/>。
        /// </summary>
        /// <typeparam name="T">存档根类型。</typeparam>
        /// <param name="data">要编码的非空数据。</param>
        /// <param name="format">JSON 或 Binary 编码格式。</param>
        /// <param name="encrypt">是否使用全局数据管线密码进行 AES 加密。</param>
        /// <returns>编码后的独立字节数组。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> 不是受支持格式。</exception>
        public static byte[] SerializeSaveValue<T>(
            T data,
            EnvironmentState.DataLoadMode format,
            bool encrypt)
        {
            return SerializeSaveValue(data, format, encrypt, false);
        }

        /// <summary>
        /// 将存档对象编码为字节。处理顺序固定为“序列化 → GZip 压缩 → AES 加密”，
        /// 解码时必须传入完全一致的格式、加密和压缩参数。
        /// </summary>
        /// <typeparam name="T">存档根类型；仅公共字段和公共可读写属性参与存档序列化。</typeparam>
        /// <param name="data">要编码的非空数据。</param>
        /// <param name="format">JSON 或 Binary 编码格式。</param>
        /// <param name="encrypt">是否使用全局数据管线密码进行 AES 加密。</param>
        /// <param name="compress">是否在加密前使用 GZip 压缩。</param>
        /// <returns>编码、压缩和加密后的独立字节数组。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> 不是受支持格式。</exception>
        /// <exception cref="InvalidOperationException">请求加密但全局密码不可用。</exception>
        public static byte[] SerializeSaveValue<T>(
            T data,
            EnvironmentState.DataLoadMode format,
            bool encrypt,
            bool compress)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));
            ValidateSaveFormat(format);

            byte[] bytes;
            if (format == EnvironmentState.DataLoadMode.Json)
            {
                string json = JsonConvert.SerializeObject(data, SaveJsonSettings);
                bytes = new UTF8Encoding(false).GetBytes(json);
            }
            else
            {
                var context = new SerializationContext();
                context.Config.SerializationPolicy = SaveSerializationPolicy;
                context.Config.DebugContext.ErrorHandlingPolicy = ErrorHandlingPolicy.ThrowOnWarningsAndErrors;
                bytes = SerializationUtility.SerializeValue(data, DataFormat.Binary, context);
            }

            if (compress)
                bytes = Compress(bytes);
            if (encrypt)
                bytes = AESEncrypt(bytes, GetEncryptionPassword());
            return bytes;
        }

        /// <summary>
        /// 将未压缩或已解密的存档字节还原为对象。Binary 模式会执行无参构造函数和字段初始化器，
        /// 从而让旧档缺失字段保留当前版本默认值；JSON 模式还会应用 <see cref="FormerSaveNamesAttribute"/>。
        /// </summary>
        /// <typeparam name="T">目标存档根类型。</typeparam>
        /// <param name="data">非空存档字节。</param>
        /// <param name="format">写入时使用的格式。</param>
        /// <param name="encrypted">字节是否经过 AES 加密。</param>
        /// <returns>还原后的强类型实例。</returns>
        /// <exception cref="InvalidDataException"><paramref name="data"/> 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> 不是受支持格式。</exception>
        public static T DeserializeSaveValue<T>(
            byte[] data,
            EnvironmentState.DataLoadMode format,
            bool encrypted)
        {
            return DeserializeSaveValue<T>(data, format, encrypted, false);
        }

        /// <summary>
        /// 将存档字节还原为对象。处理顺序固定为“AES 解密 → GZip 解压 → 反序列化”。
        /// 参数必须与编码时一致，否则会返回格式、解密或解压异常。
        /// </summary>
        /// <typeparam name="T">目标存档根类型。</typeparam>
        /// <param name="data">非空存档字节。</param>
        /// <param name="format">写入时使用的格式。</param>
        /// <param name="encrypted">字节是否经过 AES 加密。</param>
        /// <param name="compressed">解密后的字节是否经过 GZip 压缩。</param>
        /// <returns>还原后的强类型实例。</returns>
        /// <exception cref="InvalidDataException"><paramref name="data"/> 为空或压缩内容无效。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> 不是受支持格式。</exception>
        /// <exception cref="CryptographicException">密钥不匹配或密文损坏。</exception>
        public static T DeserializeSaveValue<T>(
            byte[] data,
            EnvironmentState.DataLoadMode format,
            bool encrypted,
            bool compressed)
        {
            if (data == null || data.Length == 0)
                throw new InvalidDataException("存档 Payload 为空。");
            ValidateSaveFormat(format);

            byte[] bytes = encrypted
                ? AESDecrypt(data, GetEncryptionPassword())
                : data;
            if (compressed)
                bytes = Decompress(bytes);

            if (format == EnvironmentState.DataLoadMode.Json)
            {
                string json = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
                string normalized = SaveSchema.NormalizeFormerNames<T>(json);

                if (typeof(T).IsValueType)
                    return JsonConvert.DeserializeObject<T>(normalized, SaveJsonSettings);

                T target = SaveSchema.CreateDefault<T>();
                JsonConvert.PopulateObject(normalized, target, SaveJsonSettings);
                return target;
            }

            var context = new DeserializationContext(
                ConstructedObjectDeserialization.CreateStreamingContext());
            context.Config.SerializationPolicy = SaveSerializationPolicy;
            context.Config.DebugContext.ErrorHandlingPolicy = ErrorHandlingPolicy.ThrowOnWarningsAndErrors;
            return SerializationUtility.DeserializeValue<T>(bytes, DataFormat.Binary, context);
        }

        #region 数据存储
        
        /// <summary>
        /// 保存对象为二进制文件（基于全局设置的加密选项自动判断是否加密）
        /// </summary>
        public static void Save<T>(string path, T data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);

            string ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".json")
            {
                SaveJson(path, data);
                return;
            }

            if (GlobalSettingsRuntimeLoader.Current.EnableEncryption)
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
                bytes = AESEncrypt(bytes, GlobalSettingsRuntimeLoader.Current.Password);
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
            string ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".json")
                return LoadJson<T>(path);

            if (GlobalSettingsRuntimeLoader.Current.EnableEncryption)
                return LoadEncrypted<T>(path);

            return LoadPlain<T>(path);
        }

        /// <summary>
        /// 从内存字节加载对象。
        /// 用于 Android / iOS 的 StreamingAssets URI，避免将 jar:file:// URI 交给 System.IO。
        /// </summary>
        public static T LoadFromBytes<T>(byte[] bytes, string extension)
        {
            try
            {
                if (bytes == null || bytes.Length == 0)
                    return default;

                string ext = extension ?? string.Empty;
                if (!ext.StartsWith("."))
                    ext = "." + ext;
                ext = ext.ToLowerInvariant();

                if (ext == ".json")
                {
                    // 兼容旧版本导出的 UTF-8 BOM JSON。
                    string json = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
                    return JsonConvert.DeserializeObject<T>(json, GetJsonSettings());
                }

                if (GlobalSettingsRuntimeLoader.Current.EnableEncryption)
                    bytes = AESDecrypt(bytes, GlobalSettingsRuntimeLoader.Current.Password);

                return SerializationUtility.DeserializeValue<T>(bytes, DataFormat.Binary);
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataUtil", $"从字节加载数据失败（扩展名：{extension}）：{ex.Message}");
                return default;
            }
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
                bytes = AESDecrypt(bytes, GlobalSettingsRuntimeLoader.Current.Password);
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
            return CachedJsonSettings;
        }

        private static JsonSerializerSettings CreateJsonSettings()
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
            settings.Converters.Add(new DecimalConverter());
            settings.Converters.Add(new Matrix4x4Converter());
            settings.Converters.Add(new BoundsConverter());
            settings.Converters.Add(new RectConverter());
            settings.Converters.Add(new RectOffsetConverter());

            return settings;
        }

        private static IEnumerable<Newtonsoft.Json.JsonConverter> CreateUnityConverters()
        {
            return new Newtonsoft.Json.JsonConverter[]
            {
                new Vector2Converter(),
                new Vector3Converter(),
                new Vector4Converter(),
                new QuaternionConverter(),
                new ColorConverter(),
                new DecimalConverter(),
                new Matrix4x4Converter(),
                new BoundsConverter(),
                new RectConverter(),
                new RectOffsetConverter()
            };
        }

        #endregion

        #region AES加密解密

        private static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(
                       output,
                       System.IO.Compression.CompressionLevel.Fastest,
                       true))
                gzip.Write(data, 0, data.Length);
            return output.ToArray();
        }

        private static byte[] Decompress(byte[] data)
        {
            using var input = new MemoryStream(data, false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
        
        // AES加密 盐值
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("Fink_AES_Salt");
        /// <summary>
        /// AES加密
        /// </summary>
        private static byte[] AESEncrypt(byte[] data, string password)
        {
            using Aes aes = Aes.Create();
            using var key = new Rfc2898DeriveBytes(password, Salt, 1000, HashAlgorithmName.SHA256);
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
            using var key = new Rfc2898DeriveBytes(password, Salt, 1000, HashAlgorithmName.SHA256);
            aes.Key = key.GetBytes(32);
            aes.IV = key.GetBytes(16);
            aes.Padding = PaddingMode.PKCS7;

            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                cs.Write(data, 0, data.Length);
            return ms.ToArray();
        }

        private static string GetEncryptionPassword()
        {
            if (!GlobalSettingsRuntimeLoader.TryGet(out var settings) || settings == null)
                throw new InvalidOperationException("GlobalSettingsAsset 未加载，无法取得存档加密配置。");

            if (string.IsNullOrEmpty(settings.Password))
                throw new InvalidOperationException("存档加密密码不能为空。");

            return settings.Password;
        }

        private static void ValidateSaveFormat(EnvironmentState.DataLoadMode format)
        {
            if (format != EnvironmentState.DataLoadMode.Json &&
                format != EnvironmentState.DataLoadMode.Binary)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(format),
                    format,
                    "存档格式只支持 Json 或 Binary。");
            }
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

            // === 优先查找框架自动生成的数据类（避免与 UnityEditor 内部类冲突） ===
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = asm.GetTypes().FirstOrDefault(x =>
                            x.Name == typeName &&
                            x.Namespace != null &&
                            x.Namespace.Contains("Data.AutoGen.DataClass")  // 优先你的数据类
                    );

                    if (type != null)
                        return type;
                }
                catch
                {
                    // ignored
                }
            }

            // === 再退而求其次查找所有同名类型（但排除 UnityEditor.* 命名空间） ===
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = asm.GetTypes().FirstOrDefault(x =>
                        x.Name == typeName &&
                        (x.Namespace == null || !x.Namespace.StartsWith("UnityEditor"))
                    );

                    if (type != null)
                        return type;
                }
                catch
                {
                    // ignored
                }
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
                var argTypes = args.Select(FindType).ToArray();

                var mainType = FindType(mainTypeName);

                return mainType is { IsGenericTypeDefinition: true } ? mainType.MakeGenericType(argTypes) : null;
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
