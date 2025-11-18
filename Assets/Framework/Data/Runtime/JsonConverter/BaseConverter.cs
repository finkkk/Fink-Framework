using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Framework.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Framework.Data.Runtime.JsonConverter{

	/// <summary>
	/// 通用抽象基类：部分字段序列化的自定义 JSON 转换器。
	/// ------------------------------------------------------------
	/// 作用：
	/// - 允许仅序列化指定字段或属性；
	/// - 适用于 Unity 或第三方类（无法在其上添加 JsonIgnoreAttribute 的情况）；
	/// - 基于反射访问成员，因此请确保成员不会被 Unity 剥离。
	/// ------------------------------------------------------------
	/// 使用示例：
	/// <code>
	/// public class SomeConverter : BaseConverter<SomeConverter>
	/// {
	///		protected override string[] GetPropertyNames() {
	///		return new [] { "someField", "someProperty", "etc" };
	/// }
	/// </code>
	/// </summary>
	public abstract class BaseConverter<T> : Newtonsoft.Json.JsonConverter{

		#region === 静态工具方法 ===

		/// <summary>
		/// 根据名称获取指定的字段或属性。
		/// </summary>
		private static MemberInfo GetMember(string name){

			const BindingFlags _flag = BindingFlags.Instance | BindingFlags.Public;

			var _field = typeof(T).GetField(name, _flag);
			if (_field != null) return _field;

			var _property = typeof(T).GetProperty(name, _flag);
			if (_property == null)
			{
				LogUtil.Error("JsonConverter", $"类型 {typeof(T).Name} 中未找到公共字段或属性：{name}");
				return null;
			}

			if (_property.GetGetMethod() == null)
			{
				LogUtil.Error("JsonConverter", $"属性 {typeof(T).Name}.{name} 不可读！");
				return null;
			}

			if (_property.GetSetMethod() == null)
			{
				LogUtil.Error("JsonConverter", $"属性 {typeof(T).Name}.{name} 不可写！");
				return null;
			}

			if (_property.GetIndexParameters().Any())
			{
				LogUtil.Warn("JsonConverter", $"暂不支持带索引的属性：{typeof(T).Name}.{name}");
				return null;
			}

			return _property;

		}

		/// <summary>
		/// 从成员中读取值。
		/// </summary>
		private static object GetValue(MemberInfo member, object target){

			if(member is FieldInfo info) return info.GetValue(target);

			else return (member as PropertyInfo).GetValue(target, null);

		}

		/// <summary>
		/// 向成员写入值。
		/// </summary>
		private static void SetValue(MemberInfo member, object target, object value){

			if(member is FieldInfo info) info.SetValue(target, value);

			else (member as PropertyInfo).SetValue(target, value, null);

		}

		/// <summary>
		/// 获取成员的类型。
		/// </summary>
		private static Type GetValueType(MemberInfo member){

			if(member is FieldInfo info) return info.FieldType;

			else return (member as PropertyInfo).PropertyType;

		}

		#endregion

		#region === 缓存字段 ===
		
		/// <summary>
		/// 已缓存的属性名称及对应反射成员。
		/// </summary>
		// ReSharper disable once StaticMemberInGenericType
		private static Dictionary<string, MemberInfo> _properties;

		#endregion

		#region === 实例方法 ===

		/// <summary>
		/// 获取字段名与成员映射。
		/// </summary>
		private Dictionary<string, MemberInfo> GetProperties()
		{
			if (_properties != null)
				return _properties;

			var names = GetPropertyNames();

			if (names == null || !names.Any())
			{
				LogUtil.Error("JsonConverter", $"类型 {typeof(T).Name} 的 GetPropertyNames() 返回为空，无法初始化序列化字段映射！");
				_properties = new Dictionary<string, MemberInfo>();
				return _properties;
			}

			foreach (var name in names)
			{
				if (string.IsNullOrEmpty(name))
				{
					LogUtil.Warn("JsonConverter", $"类型 {typeof(T).Name} 的 GetPropertyNames() 包含空字符串字段名，将被忽略。");
				}
			}

			try
			{
				_properties = names
					.Where(n => !string.IsNullOrEmpty(n))
					.Distinct()
					.ToDictionary(n => n, GetMember);
			}
			catch (Exception ex)
			{
				LogUtil.Error("JsonConverter", $"类型 {typeof(T).Name} 初始化字段映射失败：{ex.Message}");
				_properties = new Dictionary<string, MemberInfo>();
			}

			return _properties;
		}

		/// <summary>
		/// 派生类需重写此方法以定义需要序列化的字段名。
		/// </summary>
		protected abstract string[] GetPropertyNames();

		/// <summary>
		/// 创建一个实例，用于 ReadJson() 填充数据。
		/// </summary>
		protected T CreateInstance(){
			return Activator.CreateInstance<T>();
		}

		/// <summary>
		/// 判断当前类型是否匹配泛型参数 T。
		/// </summary>
		public override bool CanConvert(Type objectType){
			return typeof(T) == objectType;
		}


		/// <summary>
		/// 从 JSON 读取并反序列化为对象。
		/// ------------------------------------------------------------
		/// 注意：
		/// - 必须通过 object 引用进行反射，否则 struct 类型可能产生拷贝；
		/// - 通过 CreateInstance() 保证可安全重写。
		/// </summary>
		public override object ReadJson(
			JsonReader reader,
			Type objectType,
			object existingValue,
			JsonSerializer serializer
		){

			if(JsonToken.Null == reader.TokenType) return null;

			var _object = JObject.Load(reader);
			var _result = CreateInstance() as object;

			foreach(var _pair in GetProperties()){
				var _value = _object[_pair.Key].ToObject(GetValueType(_pair.Value), serializer);
				SetValue(_pair.Value, _result, _value);
			}

			return _result;

		}

		/// <summary>
		/// 将对象中指定的字段写入 JSON。
		/// ------------------------------------------------------------
		/// 只会输出通过 GetPropertyNames() 指定的成员。
		/// </summary>
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer){

			var _object = new JObject();

			foreach(var _pair in GetProperties()){
				var _value = GetValue(_pair.Value, value);
				_object[_pair.Key] = JToken.FromObject(_value, serializer);
			}

			_object.WriteTo(writer);

		}

		#endregion
	}
}
