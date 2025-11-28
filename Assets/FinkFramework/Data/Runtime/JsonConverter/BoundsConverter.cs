using UnityEngine;

namespace FinkFramework.Data.Runtime.JsonConverter{
	
	/// <summary>
	/// Bounds 的自定义 JSON 转换器。
	/// ------------------------------------------------------------
	/// 作用：
	/// - 让 Json.NET 能正确序列化和反序列化 UnityEngine.Bounds；
	/// - 兼容中心点 (center) 与范围 (extents) 属性；
	/// ------------------------------------------------------------
	/// 说明：
	/// 默认的 Newtonsoft.Json 无法正确识别 Unity 的结构体。
	/// 该转换器会在导出时输出 { "center": {...}, "extents": {...} }，
	/// 并在读取时正确还原为 Bounds 实例。
	/// </summary>
	public class BoundsConverter : BaseConverter<Bounds>{

		/// <summary>
		/// 通过创建一个虚拟实例并访问属性来“保留”它们，以此防止 IL2CPP 编译时剥离掉 Bounds 的属性。
		/// </summary>
		private void PreserveProperties(){
			
			var _dummy = new Bounds();

			_dummy.center = _dummy.center;
			_dummy.extents = _dummy.extents;

		}

		/// <summary>
		/// 返回需要序列化的字段名称。
		/// ------------------------------------------------------------
		/// Bounds 结构包含两个主要属性：
		/// - center：中心点 (Vector3)
		/// - extents：范围 (Vector3)
		/// </summary>
		protected override string[] GetPropertyNames(){
			return new []{"center", "extents"};
		}

	}

}
