using UnityEngine;

namespace FinkFramework.Runtime.Data.JsonConverter
{
    /// <summary>
    /// Vector2 的自定义 JSON 转换器。
    /// ------------------------------------------------------------
    /// 作用：
    /// - 让 Json.NET 能正确序列化和反序列化 UnityEngine.Vector2；
    /// - 兼容两个分量 (x, y) 的数值。
    /// ------------------------------------------------------------
    /// 说明：
    /// 默认的 Newtonsoft.Json 无法正确识别 Unity 的结构体。
    /// 该转换器会在导出时输出 { "x": ..., "y": ... }，
    /// 并在读取时正确还原为 Vector2 实例。
    /// </summary>
    public class Vector2Converter : BaseConverter<Vector2>
    {
        /// <summary>
        /// 返回需要序列化的字段名称。
        /// ------------------------------------------------------------
        /// Vector2 结构包含两个主要属性：
        /// - x：X 分量
        /// - y：Y 分量
        /// </summary>
        protected override string[] GetPropertyNames()
        {
            return new[] { "x", "y" };
        }
    }
}