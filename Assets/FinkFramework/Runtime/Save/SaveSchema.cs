using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace FinkFramework.Runtime.Save
{
    /// <summary>
    /// 声明字段或可读写属性在旧版存档中使用过的名称。
    /// JSON 与 Binary 加载都会在当前成员不存在时尝试这些名称，因此单纯重命名成员
    /// 不需要提高 Schema 版本。多个名称按参数顺序声明，当前名称始终拥有最高优先级。
    /// </summary>
    /// <example>
    /// <code>
    /// [FormerSaveNames("Hp", "PlayerHp")]
    /// public long Health = 100;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class FormerSaveNamesAttribute : Attribute
    {
        /// <summary>该成员接受的历史名称集合。</summary>
        public IReadOnlyList<string> Names { get; }

        /// <summary>
        /// 创建历史名称映射。
        /// </summary>
        /// <param name="names">一个或多个非空旧名称；重复或冲突名称会在保存/加载前被拒绝。</param>
        public FormerSaveNamesAttribute(params string[] names)
        {
            Names = names ?? Array.Empty<string>();
        }
    }

    internal static class SaveSchema
    {
        private static readonly ConcurrentDictionary<Type, string[]> ValidationCache = new();

        public static T CreateDefault<T>()
        {
            Type type = typeof(T);
            if (type.IsAbstract || type.IsInterface)
                throw new InvalidOperationException($"存档类型 {type.FullName} 必须是可实例化类型。");

            try
            {
                return (T)Activator.CreateInstance(type, true);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"存档类型 {type.FullName} 必须提供无参构造函数，以便创建当前版本默认值。",
                    exception);
            }
        }

        public static void Validate<T>() => Validate(typeof(T));

        public static void Validate(Type type)
        {
            string[] errors = ValidationCache.GetOrAdd(type, BuildValidationErrors);
            if (errors.Length > 0)
                throw new SaveSchemaValidationException(type, errors);
        }

        public static JsonSerializerSettings CreateJsonSettings(IEnumerable<JsonConverter> converters)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new SaveContractResolver(),
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
                FloatFormatHandling = FloatFormatHandling.String,
                TypeNameHandling = TypeNameHandling.None,
                Formatting = Formatting.Indented
            };

            foreach (JsonConverter converter in converters)
                settings.Converters.Add(converter);

            return settings;
        }

        public static string NormalizeFormerNames<T>(string json)
        {
            JToken token = JToken.Parse(json);
            if (token.Type == JTokenType.Null)
                throw new JsonSerializationException("存档根对象不能为 null。");

            NormalizeToken(typeof(T), token);
            return token.ToString(Formatting.None);
        }

        private static string[] BuildValidationErrors(Type rootType)
        {
            var errors = new List<string>();
            var visited = new HashSet<Type>();
            ValidateType(rootType, rootType.Name, errors, visited);
            return errors.ToArray();
        }

        private static void ValidateType(
            Type type,
            string path,
            List<string> errors,
            HashSet<Type> visited)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(object))
            {
                errors.Add($"{path} 不能使用 object；请声明可确定 Schema 的具体类型");
                return;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                errors.Add($"{path} 不能保存 UnityEngine.Object 引用；请只保存可重建该对象的纯数据");
                return;
            }

            if (IsLeaf(type) || !visited.Add(type))
                return;

            if (type.IsAbstract || type.IsInterface)
            {
                errors.Add($"{path} 的类型 {type.FullName} 不可实例化");
                return;
            }

            if (!type.IsValueType && !type.IsArray && type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null) == null)
            {
                errors.Add($"{path} 的类型 {type.FullName} 必须提供无参构造函数");
            }

            if (TryGetElementType(type, out Type elementType))
            {
                ValidateType(elementType, path + "[]", errors, visited);
                return;
            }

            var identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (MemberInfo member in GetSerializableMembers(type))
            {
                AddIdentity(member.Name, member.Name);

                FormerSaveNamesAttribute former = member.GetCustomAttribute<FormerSaveNamesAttribute>();
                if (former != null)
                {
                    var localNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string rawName in former.Names)
                    {
                        string name = rawName?.Trim();
                        if (string.IsNullOrEmpty(name))
                        {
                            errors.Add($"{path}.{member.Name} 包含空历史名称");
                            continue;
                        }

                        if (!localNames.Add(name))
                            errors.Add($"{path}.{member.Name} 重复声明历史名称 {name}");

                        AddIdentity(name, member.Name);
                    }
                }

                ValidateType(GetMemberType(member), path + "." + member.Name, errors, visited);
            }

            void AddIdentity(string identity, string owner)
            {
                if (identities.TryGetValue(identity, out string existing) && existing != owner)
                    errors.Add($"{path} 中名称 {identity} 同时属于 {existing} 与 {owner}");
                else
                    identities[identity] = owner;
            }
        }

        private static void NormalizeToken(Type type, JToken token)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (token == null || token.Type == JTokenType.Null || IsLeaf(type))
                return;

            if (TryGetElementType(type, out Type elementType))
            {
                if (token is JArray array)
                {
                    foreach (JToken item in array)
                        NormalizeToken(elementType, item);
                }
                else if (token is JObject dictionaryObject)
                {
                    foreach (JProperty property in dictionaryObject.Properties().ToArray())
                        NormalizeToken(elementType, property.Value);
                }
                return;
            }

            if (token is not JObject obj)
                return;

            foreach (MemberInfo member in GetSerializableMembers(type))
            {
                JProperty current = FindProperty(obj, member.Name);
                FormerSaveNamesAttribute former = member.GetCustomAttribute<FormerSaveNamesAttribute>();
                string[] names = former?.Names?.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray()
                                 ?? Array.Empty<string>();

                if (current == null)
                {
                    for (int i = names.Length - 1; i >= 0; i--)
                    {
                        JProperty alias = FindProperty(obj, names[i]);
                        if (alias == null)
                            continue;

                        current = new JProperty(member.Name, alias.Value.DeepClone());
                        obj.Add(current);
                        break;
                    }
                }

                foreach (string name in names)
                {
                    JProperty alias = FindProperty(obj, name);
                    if (alias != null && !ReferenceEquals(alias, current))
                        alias.Remove();
                }

                if (current != null)
                    NormalizeToken(GetMemberType(member), current.Value);
            }
        }

        internal static IEnumerable<MemberInfo> GetSerializableMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            IEnumerable<MemberInfo> fields = type.GetFields(flags)
                .Where(field => !field.IsStatic &&
                                !field.IsDefined(typeof(NonSerializedAttribute), true) &&
                                !field.IsDefined(typeof(JsonIgnoreAttribute), true));
            IEnumerable<MemberInfo> properties = type.GetProperties(flags)
                .Where(property => property.GetIndexParameters().Length == 0 &&
                                   property.GetMethod?.IsPublic == true &&
                                   property.SetMethod?.IsPublic == true &&
                                   !property.IsDefined(typeof(JsonIgnoreAttribute), true));
            return fields.Concat(properties);
        }

        internal static Type GetMemberType(MemberInfo member) => member switch
        {
            FieldInfo field => field.FieldType,
            PropertyInfo property => property.PropertyType,
            _ => throw new ArgumentOutOfRangeException(nameof(member))
        };

        private static JProperty FindProperty(JObject obj, string name) =>
            obj.Properties().FirstOrDefault(property =>
                string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));

        private static bool TryGetElementType(Type type, out Type elementType)
        {
            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return true;
            }

            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                Type[] arguments = type.GetGenericArguments();
                if (definition == typeof(Dictionary<,>))
                {
                    elementType = arguments[1];
                    return true;
                }

                if (typeof(IEnumerable).IsAssignableFrom(type) && arguments.Length == 1)
                {
                    elementType = arguments[0];
                    return true;
                }
            }

            elementType = null;
            return false;
        }

        private static bool IsLeaf(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) ||
                   type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
                   type == typeof(TimeSpan) || type == typeof(Guid) ||
                   type.Namespace == "UnityEngine";
        }

        private sealed class SaveContractResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(
                Type type,
                MemberSerialization memberSerialization)
            {
                HashSet<string> allowed = GetSerializableMembers(type)
                    .Select(member => member.Name)
                    .ToHashSet(StringComparer.Ordinal);

                return base.CreateProperties(type, memberSerialization)
                    .Where(property => allowed.Contains(property.UnderlyingName))
                    .ToList();
            }
        }
    }
}
