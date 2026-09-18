using System;
using System.Globalization;
using Newtonsoft.Json;

namespace FinkFramework.Runtime.Data.JsonConverter
{
    /// <summary>
    /// 存档 JSON 的 decimal 转换器。
    ///
    /// Unity 使用的 Newtonsoft.Json 版本在把高精度 decimal 经过 JToken
    /// 中转时可能先转换成 double，导致 Decimal.MinValue 被写成科学计数法，
    /// 之后再按 decimal 读取就会失败。能够被 double 无损表示的值仍写成数字；
    /// 需要保留精度的值写成 invariant 字符串，读取时统一还原为 decimal。
    /// </summary>
    public sealed class DecimalConverter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(decimal) ||
                   objectType == typeof(decimal?);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            decimal decimalValue = (decimal)value;
            double doubleValue = (double)decimalValue;
            bool canRoundTripAsDouble;
            try
            {
                canRoundTripAsDouble = (decimal)doubleValue == decimalValue;
            }
            catch (OverflowException)
            {
                canRoundTripAsDouble = false;
            }

            if (canRoundTripAsDouble)
            {
                writer.WriteRawValue(decimalValue.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                writer.WriteValue(decimalValue.ToString(CultureInfo.InvariantCulture));
            }
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                if (objectType == typeof(decimal?))
                    return null;

                throw new JsonSerializationException("非 Nullable decimal 不能读取 null。");
            }

            string text = Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
            if (decimal.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out decimal result))
            {
                return result;
            }

            throw new JsonSerializationException($"无法将 {text} 解析为 decimal。");
        }
    }
}
