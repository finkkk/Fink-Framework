using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace Framework.Data.Runtime.JsonConverter{
	
	/// <summary>
	/// Json.NET 在 Unity 环境中的集成与初始化工具。
	/// ------------------------------------------------------------
	/// 作用：
	/// - 自动注册所有自定义的 <see cref="Newtonsoft.Json.JsonConverter"/>；
	/// - 统一配置 <see cref="JsonConvert.DefaultSettings"/>；
	/// - 支持 Unity 常用类型（Vector、Color、Bounds、Matrix、Quaternion 等）；
	/// - 兼容第三方或自定义 Converter。
	/// ------------------------------------------------------------
	/// 使用说明：
	/// - 需将 PlayerSettings → Api Compatibility Level 设置为 “.NET 2.0” 或更高；
	/// - 导入 Json.NET DLL（Newtonsoft.Json）；
	/// - 项目启动时会自动调用 Initialize()，注册所有 Converter；
	/// - 可通过修改 <see cref="defaultSettings"/> 自定义全局序列化行为。
	/// ------------------------------------------------------------
	/// 示例：
	/// <code>
	/// Debug.Log(JsonConvert.SerializeObject(Vector3.up));
	/// var vec = JsonConvert.DeserializeObject&lt;Vector3&gt;("{\"x\":1,\"y\":0,\"z\":0}");
	/// </code>
	/// </summary>
	public static class JsonNetUtility{

		#region === 字段 ===
		
		/// <summary>
		/// 默认的 <see cref="JsonSerializerSettings"/>。
		/// ------------------------------------------------------------
		/// 默认属性均保持原值，仅自动填充 <see cref="Converters"/>：
		/// 1. 所有无参构造的自定义 JsonConverter；
		/// 2. 所有来自 <c>Framework.Data.Runtime.JsonConverter</c> 的内置转换器；
		/// 3. 系统内置的 <see cref="StringEnumConverter"/> 与 <see cref="VersionConverter"/>。
		/// </summary>
		public static JsonSerializerSettings defaultSettings = new(){
			Converters = CreateConverters()
		};

		#endregion
		
		#region === 初始化 ===
		/// <summary>
		/// 运行时初始化入口。
		/// ------------------------------------------------------------
		/// 如果 Json.NET 的全局 DefaultSettings 尚未被用户定义，
		/// 则自动绑定为当前模块的 <see cref="defaultSettings"/>。
		/// </summary>
		[RuntimeInitializeOnLoadMethod]
		public static void Initialize()
		{
			if (JsonConvert.DefaultSettings == null)
				JsonConvert.DefaultSettings = () => defaultSettings;
		}
		
		#if UNITY_EDITOR
		/// <summary>
		/// 编辑器下的初始化（解决 RuntimeInitializeOnLoadMethod 编辑器模式无效的问题）。
		/// </summary>
		[UnityEditor.InitializeOnLoadMethod]
		private static void InitializeEditor(){
			Initialize();
		}
		#endif
		#endregion
		
		#region === 转换器注册逻辑 ===
		
		/// <summary>
		/// 创建所有有效的转换器实例。
		/// ------------------------------------------------------------
		/// 包括：
		/// - 自定义的 Converter；
		/// - 系统内置 Converter。
		/// </summary>
		private static List<Newtonsoft.Json.JsonConverter> CreateConverters(){

			var _customs = FindConverterTypes().Select(CreateConverter);

			var _builtins = new Newtonsoft.Json.JsonConverter[]{new StringEnumConverter(), new VersionConverter()};

			return _customs.Concat(_builtins).Where((converter) => null != converter).ToList();

		}

		/// <summary>
		/// 创建指定类型的转换器实例。
		/// </summary>
		private static Newtonsoft.Json.JsonConverter CreateConverter(Type type)
		{
			try
			{
				return Activator.CreateInstance(type) as Newtonsoft.Json.JsonConverter;
			}
			catch (Exception ex)
			{
				Debug.LogErrorFormat("无法创建 JsonConverter 实例 {0}：\n{1}", type, ex);
				return null;
			}
		}

		/// <summary>
		/// 查找所有有效的自定义 Converter 类型。
		/// ------------------------------------------------------------
		/// 条件：
		/// - 继承自 JsonConverter；
		/// - 非抽象类、非泛型定义；
		/// - 具有无参构造函数；
		/// - 不属于 Newtonsoft.Json 自身命名空间；
		/// - 优先排序 Framework.Data.Runtime.JsonConverter 命名空间中的转换器。
		/// </summary>
		private static Type[] FindConverterTypes(){
			
			return AppDomain.CurrentDomain.GetAssemblies(

				).SelectMany((dll) => dll.GetTypes()
				).Where((type) => typeof(Newtonsoft.Json.JsonConverter).IsAssignableFrom(type)

				).Where((type) => (!type.IsAbstract && !type.IsGenericTypeDefinition)
				).Where((type) => null != type.GetConstructor(Type.EmptyTypes)

				).Where((type) => !(null != type.Namespace && type.Namespace.StartsWith("Newtonsoft.Json"))
				).OrderBy((type) => null != type.Namespace && type.Namespace.StartsWith("Framework.Data.Runtime.JsonConverter")
				
			).ToArray();
		}
		#endregion
		
	}

}
