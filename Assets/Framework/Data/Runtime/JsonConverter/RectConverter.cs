using UnityEngine;

namespace Framework.Data.Runtime.JsonConverter
{
    /// <summary>
    /// Rect 的自定义 JSON 转换器。
    /// ------------------------------------------------------------
    /// 作用：
    /// - 让 Json.NET 能正确序列化和反序列化 UnityEngine.Rect；
    /// - 兼容矩形的基本属性 (x, y, width, height)。
    /// ------------------------------------------------------------
    /// 说明：
    /// 默认的 Newtonsoft.Json 无法直接识别 Unity 的结构体。
    /// 该转换器会在导出时输出 { "x": ..., "y": ..., "width": ..., "height": ... }，
    /// 并在读取时正确还原为 Rect 实例。
    /// </summary>
    public class RectConverter : BaseConverter<Rect>
    {
        /// <summary>
        /// 返回需要序列化的字段名称。
        /// ------------------------------------------------------------
        /// Rect 结构包含四个主要属性：
        /// - x：矩形的 X 坐标
        /// - y：矩形的 Y 坐标
        /// - width：矩形的宽度
        /// - height：矩形的高度
        /// </summary>
        protected override string[] GetPropertyNames()
        {
            return new[] { "x", "y", "width", "height" };
        }
    }
}