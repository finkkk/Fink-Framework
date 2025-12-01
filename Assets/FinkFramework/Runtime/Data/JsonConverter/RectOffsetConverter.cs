using UnityEngine;

namespace FinkFramework.Runtime.Data.JsonConverter
{
    /// <summary>
    /// RectOffset 的自定义 JSON 转换器。
    /// ------------------------------------------------------------
    /// 作用：
    /// - 让 Json.NET 能正确序列化和反序列化 UnityEngine.RectOffset；
    /// - 兼容四个边距属性 (left, right, top, bottom)。
    /// ------------------------------------------------------------
    /// 说明：
    /// 默认的 Newtonsoft.Json 无法直接识别 Unity 的结构体。
    /// 该转换器会在导出时输出 { "left": ..., "right": ..., "top": ..., "bottom": ... }，
    /// 并在读取时正确还原为 RectOffset 实例。
    /// </summary>
    public class RectOffsetConverter : BaseConverter<RectOffset>
    {
        /// <summary>
        /// 返回需要序列化的字段名称。
        /// ------------------------------------------------------------
        /// RectOffset 结构包含四个主要属性：
        /// - left：左边距
        /// - right：右边距
        /// - top：上边距
        /// - bottom：下边距
        /// </summary>
        protected override string[] GetPropertyNames()
        {
            return new[] { "left", "right", "top", "bottom" };
        }
    }
}