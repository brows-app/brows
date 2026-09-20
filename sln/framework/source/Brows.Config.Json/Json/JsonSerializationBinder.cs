using Newtonsoft.Json;
using Brows.Composition;
using System;

namespace Brows.Json;

/// <summary>
/// Implementations of this base type provide a binding mechanism during JSON
/// serialization and deserialization.
/// </summary>
/// <remarks>
/// The binders are imported at runtime by the <see cref="IJsonSerializerSettingsFactory"/>.
/// Use the imported instance of that type to create <see cref="JsonSerializerSettings"/> for
/// serializing and deserializing types.
/// </remarks>
public abstract class JsonSerializationBinder : IExport {
    /// <summary>
    /// When overridden in a derived type, provides binding information for the <paramref name="serializedType"/>.
    /// </summary>
    /// <param name="serializedType">The serialized type.</param>
    /// <returns>
    /// The binding name for the type used during JSON serialization, or <see langword="null"/> if no
    /// binding is available.
    /// </returns>
    protected internal abstract Name BindToName(Type serializedType);

    /// <summary>
    /// When overridden in a derived class, provides the type to use for the specified arguments.
    /// </summary>
    /// <param name="assemblyName">The assembly name binding of the type.</param>
    /// <param name="typeName">The type name binding of the type.</param>
    /// <returns>
    /// The type to use for JSON deserialization.
    /// </returns>
    protected internal abstract Type BindToType(string assemblyName, string typeName);

    /// <summary>
    /// Information about the bound name for JSON serialization.
    /// </summary>
    /// <param name="Assembly">The assembly information.</param>
    /// <param name="Type">The type information</param>
    protected internal sealed record Name(string Assembly, string Type) {
    }
}
