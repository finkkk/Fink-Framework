using System;
using System.Runtime.Serialization;

namespace FinkFramework.Odin.OdinSerializer
{
    /// <summary>
    /// Opt-in marker used by persistence systems that need field initializers and parameterless
    /// constructors to provide defaults for members missing from older serialized data.
    /// Normal Odin deserialization remains unchanged unless this marker is supplied.
    /// </summary>
    public static class ConstructedObjectDeserialization
    {
        private static readonly object Marker = new();

        /// <summary>
        /// Creates the opt-in streaming context used by save-data deserialization.
        /// Formatters receiving this context construct objects normally before applying serialized members,
        /// allowing missing members from older files to retain constructor and field-initializer defaults.
        /// </summary>
        /// <returns>A streaming context recognized only by this Odin integration.</returns>
        public static StreamingContext CreateStreamingContext()
        {
            return new StreamingContext(StreamingContextStates.File, Marker);
        }

        internal static bool IsEnabled(DeserializationContext context)
        {
            return context != null && ReferenceEquals(context.StreamingContext.Context, Marker);
        }

        internal static bool TryCreate(Type type, out object value)
        {
            value = null;
            if (type == null || type.IsAbstract || type.IsInterface || type.IsArray)
                return false;

            try
            {
                value = Activator.CreateInstance(type, true);
                return value != null || type.IsValueType;
            }
            catch
            {
                return false;
            }
        }
    }
}
