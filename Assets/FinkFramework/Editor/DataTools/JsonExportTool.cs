using System.IO;
using FinkFramework.Runtime.Data.JsonConverter;
using FinkFramework.Runtime.Utils;
using Newtonsoft.Json;

namespace FinkFramework.Editor.DataTools
{
    /// <summary>
    /// 负责导出 JSON（真实数据）的独立工具类。
    /// </summary>
    public class JsonExportTool
    {
        public static void ExportJson(object container, string jsonPath)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                // 注册所有 Unity 类型转换器
                settings.Converters.Add(new Vector2Converter());
                settings.Converters.Add(new Vector3Converter());
                settings.Converters.Add(new Vector4Converter());
                settings.Converters.Add(new QuaternionConverter());
                settings.Converters.Add(new ColorConverter());
                settings.Converters.Add(new Matrix4x4Converter());
                settings.Converters.Add(new BoundsConverter());
                settings.Converters.Add(new RectConverter());
                settings.Converters.Add(new RectOffsetConverter());

                var json = JsonConvert.SerializeObject(container, settings);

                Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? string.Empty);
                File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);

                LogUtil.Success("DataJsonTool", $"已生成 JSON 数据：{jsonPath}");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error("DataJsonTool", $"JSON 导出失败：{ex.Message}");
            }
        }
    }
}