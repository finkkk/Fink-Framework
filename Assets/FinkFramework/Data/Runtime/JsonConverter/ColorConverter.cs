using UnityEngine;

namespace FinkFramework.Data.Runtime.JsonConverter
{
    /// <summary>
    /// Color 的自定义 JSON 转换器。
    /// ------------------------------------------------------------
    /// 作用：
    /// - 让 Json.NET 能正确序列化和反序列化 UnityEngine.Color；
    /// - 兼容 RGBA 四个通道的数值；
    /// ------------------------------------------------------------
    /// 说明：
    /// 默认的 Newtonsoft.Json 无法正确识别 Unity 的结构体。
    /// 该转换器会在导出时输出 { "r": 1, "g": 0.5, "b": 0.2, "a": 1 }，
    /// 并在读取时正确还原为 Color 实例。
    /// </summary>
    public class ColorConverter : BaseConverter<Color>
    {
        /// <summary>
        /// 返回需要序列化的字段名称。
        /// ------------------------------------------------------------
        /// Color 结构包含四个主要属性：
        /// - r：红色通道
        /// - g：绿色通道
        /// - b：蓝色通道
        /// - a：透明度通道
        /// </summary>
        protected override string[] GetPropertyNames()
        {
            return new[] { "r", "g", "b", "a" };
        }
    }
}