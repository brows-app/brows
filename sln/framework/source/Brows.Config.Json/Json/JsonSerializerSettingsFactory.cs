using Domore.Logs;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Brows.Composition;
using Brows.Json.ConverterProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Brows.Json;

public sealed class JsonSerializerSettingsFactory : IJsonSerializerSettingsFactory {
    private static readonly ILog Log = Logging.For(typeof(JsonSerializerSettingsFactory));

    [ImportOptional] internal IReadOnlyList<JsonConverterProvider> ConverterProviders { get; set; }
    [ImportOptional] internal IReadOnlyList<JsonSerializationBinder> SerializationBinders { get; set; }

    /// <summary>
    /// This instance provides a default settings factory that can create settings before the
    /// composition framework may be ready.
    /// </summary>
    public static readonly JsonSerializerSettingsFactory Default = new() {
        ConverterProviders = [
            new StringEnumConverterProvider(),
            new TypeConverterConverterProvider()
        ],
        SerializationBinders = [
        ]
    };

    public JsonSerializerSettingsFactory() {
        /*
         * Either the instance provided by Default, or the instance provided by the composition
         * framework should normally be used. This public constructor is made available only
         * for use during testing. Do not otherwise create other instances of this type.
         */
    }

    JsonSerializerSettings IJsonSerializerSettingsFactory.Create(Action<JsonSerializerSettings> callback,
                                                                 IExportContext context) {
        /*
         * When a context is provided in the argument list, it's an indication that these settings
         * are required before the composition framework is ready. So, The imports in the context
         * override the populated imports in this instance.
         */
        var converterProviders =
            context?.ListImports<JsonConverterProvider>() ?? ConverterProviders;
        var serializationBinders =
            context?.ListImports<JsonSerializationBinder>() ?? SerializationBinders;
        var result = new JsonSerializerSettings() {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            ContractResolver = new ContractResolver(),
            Converters = [.. converterProviders
                ?.SelectMany(provider => provider?.JsonConverters ?? [])
                ?.Where(converter => converter is not null) ?? []],
            Error = (s, e) => {
                if (e is not null) {
                    if (Log.Warn()) {
                        Log.Warn(
                            $"JSON serializer error event",
                            $" Current object: {e.CurrentObject?.GetType()?.FullName}",
                            $"Original object: {e.ErrorContext?.OriginalObject?.GetType()?.FullName}",
                            $"        Handled: {e.ErrorContext?.Handled}",
                            $"         Member: {e.ErrorContext?.Member}",
                            $"           Path: {e.ErrorContext?.Path}",
                            e.ErrorContext?.Error
                        );
                    }
                }
            },
            SerializationBinder = new AggregateSerializationBinder {
                SerializationBinders = serializationBinders
            }
        };
        if (callback is not null) {
            /*
             * The callback is useful for setting extra settings that only the caller
             * knows about.
             */
            callback(result);
        }
        return result;
    }

    private sealed class TraceWriter : ITraceWriter {
        TraceLevel ITraceWriter.LevelFilter => TraceLevel.
#if DEBUG
            Verbose
#else
            Warning
#endif
        ;
        void ITraceWriter.Trace(TraceLevel level, string message, Exception ex) {
            var severity = level switch {
                TraceLevel.Verbose => LogSeverity.Debug,
                TraceLevel.Info => LogSeverity.Info,
                TraceLevel.Warning => LogSeverity.Warn,
                TraceLevel.Error => LogSeverity.Error,
                TraceLevel.Off => LogSeverity.None,
                _ => LogSeverity.Critical
            };
            Log.Data(severity, message, ex);
        }
    }

    private sealed class AggregateSerializationBinder : ISerializationBinder {
        private static readonly DefaultSerializationBinder Default = new();

        public IReadOnlyList<JsonSerializationBinder> SerializationBinders { get; set; }

        void ISerializationBinder.BindToName(Type serializedType, out string assemblyName, out string typeName) {
            var name = SerializationBinders
                ?.Select(item => item?.BindToName(serializedType))
                ?.FirstOrDefault(name => name is not null);
            if (name is not null) {
                assemblyName = name.Assembly;
                typeName = name.Type;
                return;
            }
            Default.BindToName(serializedType, out assemblyName, out typeName);
        }

        Type ISerializationBinder.BindToType(string assemblyName, string typeName) {
            var type = SerializationBinders
                ?.Select(item => item?.BindToType(assemblyName, typeName))
                ?.FirstOrDefault(type => type is not null);
            if (type is not null) {
                return type;
            }
            return Default.BindToType(assemblyName, typeName);
        }
    }

    private sealed class ContractResolver : DefaultContractResolver {
        protected sealed override JsonProperty CreateProperty(MemberInfo member,
                                                              MemberSerialization memberSerialization) {
            var property = base.CreateProperty(member, memberSerialization);
            if (property.Writable == false) {
                var propertyInfo = member as PropertyInfo;
                if (propertyInfo != null) {
                    var setMethod = propertyInfo.GetSetMethod(nonPublic: true);
                    if (setMethod != null) {
                        /*
                         * This allows private setters to be used during
                         * JSON deserialization. Otherwise, they'd be
                         * ignored.
                         */
                        property.Writable = true;
                    }
                }
            }
            JsonShouldSerializeAttribute.Register(property);
            return property;
        }
    }
}
