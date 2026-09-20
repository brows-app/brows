using Newtonsoft.Json;
using Brows.Composition;
using System.Collections.Generic;

namespace Brows.Json;

/// <summary>
/// Implementations of this base type provide instances of <see cref="JsonConverter"/>
/// for use during JSON serialization and deserialization.
/// </summary>
/// <remarks>
/// The provided converters are imported at runtime by the <see cref="IJsonSerializerSettingsFactory"/>.
/// Use the imported instance of that type to create <see cref="JsonSerializerSettings"/> for
/// serializing and deserializing types.
/// </remarks>
public abstract class JsonConverterProvider : IExport {
    /// <summary>
    /// When overridden in a derived class, provides instances of <see cref="JsonConverter"/>
    /// for use during JSON serialization and deserialization.
    /// </summary>
    protected internal abstract IEnumerable<JsonConverter> JsonConverters { get; }
}
