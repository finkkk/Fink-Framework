using UnityEngine;

namespace FinkFramework.Data.Runtime.JsonConverter
{
    /// <summary>
    /// Quaternion 的自定义 JSON 转换器。
    /// ------------------------------------------------------------
    /// 作用：
    /// - 让 Json.NET 能正确序列化和反序列化 UnityEngine.Quaternion；
    /// - 兼容四个分量 (x, y, z, w) 的旋转数据。
    /// ------------------------------------------------------------
    /// 说明：
    /// 默认的 Newtonsoft.Json 无法正确识别 Unity 的结构体。
    /// 该转换器会在导出时输出 { "x": ..., "y": ..., "z": ..., "w": ... }，
    /// 并在读取时正确还原为 Quaternion 实例。
    /// </summary>
    public class QuaternionConverter : BaseConverter<Quaternion>
    {
        /// <summary>
        /// 返回需要序列化的字段名称。
        /// ------------------------------------------------------------
        /// Quaternion 结构包含四个主要属性：
        /// - x：X 分量
        /// - y：Y 分量
        /// - z：Z 分量
        /// - w：W 分量
        /// </summary>
        protected override string[] GetPropertyNames()
        {
            return new[] { "x", "y", "z", "w" };
        }
    }
}