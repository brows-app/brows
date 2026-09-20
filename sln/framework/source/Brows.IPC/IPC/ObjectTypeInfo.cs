using Newtonsoft.Json;
using System;

namespace Brows.IPC;

/// <summary>
/// Information about types used by <see cref="IObjectTypeResolver"/>.
/// </summary>
public sealed class ObjectTypeInfo : IEquatable<ObjectTypeInfo> {
    private int? HashCode;
    private string String;

    /// <summary>
    /// The type can come from any source.
    /// </summary>
    public const string AnySource = "*";

    /// <summary>
    /// The type is compatible with any version.
    /// </summary>
    public const string AnyVersion = "*";

    /// <summary>
    /// Gets the name of the type.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the source of the type.
    /// </summary>
    public string TypeSource { get; }

    /// <summary>
    /// Gets the version of the type.
    /// </summary>
    public string TypeVersion { get; }

    /// <summary>
    /// Creates new type information.
    /// </summary>
    /// <param name="typeName">The name of the type.</param>
    /// <param name="typeSource">The source of the type.</param>
    /// <param name="typeVersion">The version of the type.</param>
    public ObjectTypeInfo(string typeName, string typeSource, string typeVersion) {
        TypeName = typeName;
        TypeSource = typeSource;
        TypeVersion = typeVersion;
    }

    /// <summary>
    /// Creates new type information.
    /// </summary>
    /// <param name="typeName">The name of the type.</param>
    public ObjectTypeInfo(string typeName) : this(typeName: typeName, typeSource: AnySource, typeVersion: AnyVersion) {
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current instance.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns><see langword="true"/> if the specified object is of type <see cref="ObjectTypeInfo"/> and is equal to the
    /// current instance; otherwise, <see langword="false"/>.</returns>
    public sealed override bool Equals(object obj) {
        return
            obj is ObjectTypeInfo other &&
            Equals(other);
    }

    /// <summary>
    /// Determines whether the specified <see cref="ObjectTypeInfo"/> is equal to the current instance.
    /// </summary>
    /// <remarks>
    /// Two <see cref="ObjectTypeInfo"/> instances are considered equal if they are not <see langword="null"/> 
    /// and their <c>TypeName</c>, <c>TypeSource</c>, and <c>TypeVersion</c> properties are all equal.
    /// </remarks>
    /// <param name="other">The <see cref="ObjectTypeInfo"/> to compare with the current instance.</param>
    /// <returns>
    /// <see langword="true"/> if the specified <see cref="ObjectTypeInfo"/> is equal to the current instance;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(ObjectTypeInfo other) {
        return
            other is not null &&
            other.TypeName == TypeName &&
            other.TypeSource == TypeSource &&
            other.TypeVersion == TypeVersion;
    }

    /// <summary>
    /// Generates a hash code for the current instance based on its properties.
    /// </summary>
    /// <remarks>The hash code is computed using the values of the <c>TypeName</c>, <c>TypeSource</c>,  and
    /// <c>TypeVersion</c> properties. If these properties are null, their contribution to the hash code is treated as
    /// zero. The method ensures consistent hash codes for instances with the same property values.</remarks>
    /// <returns>An integer representing the hash code of the current instance.</returns>
    public sealed override int GetHashCode() {
        int getHashCode() {
            var hash = 17;
            unchecked {
                hash = hash * 31 + (TypeName?.GetHashCode() ?? 0);
                hash = hash * 31 + (TypeSource?.GetHashCode() ?? 0);
                hash = hash * 31 + (TypeVersion?.GetHashCode() ?? 0);
            }
            return hash;
        }
        return HashCode ??= getHashCode();
    }

    /// <summary>
    /// Converts the current instance to an <see cref="ObjectTypeInfo"/> object with a version-independent
    /// representation.
    /// </summary>
    /// <remarks>
    /// This method is useful when a version-agnostic representation of the type is required.
    /// </remarks>
    /// <returns>
    /// An <see cref="ObjectTypeInfo"/> instance containing the type name, source, and a version-independent identifier.
    /// </returns>
    public ObjectTypeInfo ToAnyVersion() {
        return new(
            typeName: TypeName,
            typeSource: TypeSource,
            typeVersion: AnyVersion);
    }

    /// <summary>
    /// Converts the current instance to an <see cref="ObjectTypeInfo"/> object with a source-independent
    /// representation.
    /// </summary>
    /// <remarks>
    /// This method is useful when a source-agnostic representation of the type is required.
    /// </remarks>
    /// <returns>
    /// An <see cref="ObjectTypeInfo"/> instance containing the type name, source, and a source-independent identifier.
    /// </returns>
    public ObjectTypeInfo ToAnySource() {
        return new(
            typeName: TypeName,
            typeSource: AnySource,
            typeVersion: TypeVersion);
    }

    /// <summary>
    /// Returns a JSON string representation of the current object.
    /// </summary>
    /// <returns>A JSON-formatted string that represents the current object.</returns>
    public sealed override string ToString() {
        return String ??= JsonConvert.SerializeObject(this, Formatting.None);
    }

    /// <summary>
    /// Creates an instance of <see cref="ObjectTypeInfo"/> from the specified <see cref="Type"/>.
    /// </summary>
    /// <param name="type">
    /// The <see cref="Type"/> to extract information from.
    /// </param>
    /// <returns>
    /// An <see cref="ObjectTypeInfo"/> containing the name, source assembly, and version of the specified type.
    /// </returns>
    public static ObjectTypeInfo From(Type type) {
        var assembly = type?.Assembly;
        var assemblyName = assembly?.GetName();
        return new(
            typeName: type?.Name,
            typeSource: assemblyName?.Name,
            typeVersion: assemblyName?.Version?.ToString());
    }
}
