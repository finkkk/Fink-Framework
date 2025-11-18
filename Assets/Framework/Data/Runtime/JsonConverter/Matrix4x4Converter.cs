using System.Linq;
using UnityEngine;

namespace Framework.Data.Runtime.JsonConverter
{
    /// <summary>
    /// Matrix4x4 的自定义 JSON 转换器。
    /// ------------------------------------------------------------
    /// 作用：
    /// - 让 Json.NET 能正确序列化和反序列化 UnityEngine.Matrix4x4；
    /// - 兼容所有 4×4 矩阵元素（m00 ~ m33）。
    /// ------------------------------------------------------------
    /// 说明：
    /// 默认的 Newtonsoft.Json 无法直接识别 Unity 的 Matrix4x4 结构。
    /// 该转换器会在导出时输出 { "m00":..., "m01":..., ..., "m33":... }，
    /// 并在读取时正确还原为 Matrix4x4 实例。
    /// </summary>
    public class Matrix4x4Converter : BaseConverter<Matrix4x4>
    {
        /// <summary>
        /// 返回需要序列化的字段名称。
        /// ------------------------------------------------------------
        /// Matrix4x4 结构包含 16 个浮点数：
        /// m00 ~ m03, m10 ~ m13, m20 ~ m23, m30 ~ m33。
        /// </summary>
        protected override string[] GetPropertyNames()
        {
            var indexes = new[] { "0", "1", "2", "3" };
            return indexes.SelectMany(row => indexes.Select(column => "m" + row + column)).ToArray();
        }
    }
}