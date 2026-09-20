using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Brows.Json;

/// <summary>
/// Provides a thread-safe cache for determining whether object properties
/// are included in JSON serialization.
/// </summary>
public sealed class JsonPropertyCache {
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, bool>> Cache = [];

    private JsonPropertyCache() {
    }

    /// <summary>
    /// Gets the default instance of <see cref="JsonPropertyCache"/>.
    /// </summary>
    public static JsonPropertyCache Current { get; } = new();

    /// <summary>
    /// Determines if the property identified by <paramref name="propertyName"/> is included
    /// in JSON serialization for the instance <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">The object instance.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>
    /// <see langword="true"/> if the property is included during JSON serialization.
    /// Otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="obj"/> is null.
    /// </exception>
    public bool IsJsonProperty(object obj, string propertyName) {
        if (obj is null) {
            throw new ArgumentNullException(nameof(obj));
        }
        var writeProperties = Cache.GetOrAdd(obj.GetType(), type => new(collection:
            type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .GroupBy(property => property.Name)
                .ToDictionary(
                    group => group.Key,
                    group => group.Any(property =>
                        property.GetCustomAttribute<JsonPropertyAttribute>(inherit: true) != null))));
        if (writeProperties.TryGetValue(propertyName, out var write)) {
            return write;
        }
        return false;
    }
}
