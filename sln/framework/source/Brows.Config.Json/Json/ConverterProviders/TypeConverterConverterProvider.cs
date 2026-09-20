using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace Brows.Json.ConverterProviders;

internal sealed class TypeConverterConverterProvider : JsonConverterProvider {
    private static readonly ConcurrentDictionary<Type, bool> CanConvertCache = [];

    protected internal sealed override IEnumerable<JsonConverter> JsonConverters { get; } =
        [new TypeConverterConverter()];

    private sealed class TypeConverterConverter : JsonConverter {
        /*
         * JSON.Net (Newtonsoft.Json) has some non-obvious behavior when a type implements
         * IConvertible and also has a TypeConverter attribute applied. So, this JSON
         * converter attempts to force the use of the TypeConverter on any of our own types.
         */

        public override bool CanConvert(Type objectType) {
            return CanConvertCache.GetOrAdd(objectType, objectType => {
                if (objectType.IsEnum) {
                    /*
                     * Don't include enum types, because they may be handled by
                     * other converters.
                     */
                    return false;
                }
                var objectTypeNamespace = objectType.Namespace;
                var objectTypeIsInBrows =
                    objectTypeNamespace == "Brows" ||
                    objectTypeNamespace?.StartsWith("Brows.") == true;
                if (objectTypeIsInBrows == false) {
                    return false;
                }
                var typeConverter = TypeDescriptor.GetConverter(objectType);
                if (typeConverter is null) {
                    return false;
                }
                if (typeConverter.GetType() == typeof(TypeConverter)) {
                    return false;
                }
                return true;
            });
        }

        public sealed override object ReadJson(JsonReader reader,
                                               Type objectType,
                                               object existingValue,
                                               JsonSerializer serializer) {
            var typeConverter = TypeDescriptor.GetConverter(objectType);
            var val = reader?.Value;
            var obj = typeConverter?.ConvertFrom(
                context: null,
                culture: CultureInfo.InvariantCulture,
                val);
            return obj;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            var type = value?.GetType();
            if (type is null) {
                writer?.WriteNull();
                return;
            }
            var typeConverter = TypeDescriptor.GetConverter(type);
            var str = typeConverter?.ConvertToString(
                context: null,
                culture: CultureInfo.InvariantCulture,
                value: value);
            writer?.WriteValue(str);
        }
    }
}
