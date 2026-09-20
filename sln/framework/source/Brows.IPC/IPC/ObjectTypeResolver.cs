using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Brows.IPC;

/// <summary>
/// Provides factory methods for creating instances of <see cref="IObjectTypeResolver"/>.
/// Also acts as an internal cache for type-resolution agents.
/// </summary>
public sealed class ObjectTypeResolver {
    private readonly ConcurrentDictionary<ObjectTypeInfo, Type> ResolveCache = [];
    private readonly ConcurrentDictionary<Type, ObjectTypeInfo> ReverseCache = [];

    internal IObjectTypeResolver Agent { get; }

    internal ObjectTypeResolver(IObjectTypeResolver agent) {
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    internal Type Resolve(ObjectTypeInfo info, bool throwOnNotResolved) {
        var type = ResolveCache.GetOrAdd(info, info => {
            /*
             * Fallback resolution: try an exact match first, then relax
             * the version, then relax both version and source. This allows
             * forward-compatible deserialization when the sender's type
             * metadata doesn't exactly match the receiver's registrations.
             */
            var type = Agent.Resolve(info);
            if (type is null) {
                type = Agent.Resolve(info.ToAnyVersion());
            }
            if (type is null) {
                type = Agent.Resolve(info.ToAnyVersion().ToAnySource());
            }
            return type;
        });
        if (type is null) {
            if (throwOnNotResolved) {
                throw new ObjectTypeNotResolvedException(info);
            }
        }
        return type;
    }

    internal ObjectTypeInfo Reverse(Type type, bool throwOnNotFound) {
        var info = ReverseCache.GetOrAdd(type, Agent.Reverse);
        if (info is null) {
            if (throwOnNotFound) {
                throw new ObjectTypeNotFoundException(type);
            }
        }
        return info;
    }

    /// <summary>
    /// Returns an instance of <see cref="IObjectTypeResolver"/> for the lookup.
    /// </summary>
    /// <param name="value">The lookup.</param>
    /// <returns>The type resolver.</returns>
    public static IObjectTypeResolver Lookup(IReadOnlyDictionary<ObjectTypeInfo, Type> value) {
        return new LookupResolver(value);
    }

    /// <summary>
    /// Returns an instance of <see cref="IObjectTypeResolver"/> for the lookup.
    /// </summary>
    /// <param name="value">The lookup.</param>
    /// <returns>The type resolver.</returns>
    public static IObjectTypeResolver Lookup(IReadOnlyDictionary<string, Type> value) {
        return new LookupResolver(
            value?.ToDictionary(
                keySelector: v => new ObjectTypeInfo(v.Key),
                elementSelector: v => v.Value));
    }

    /// <summary>
    /// Returns an instance of <see cref="IObjectTypeResolver"/> for the types.
    /// </summary>
    /// <param name="types">The types.</param>
    /// <returns>The type resolver.</returns>
    public static IObjectTypeResolver Lookup(IEnumerable<Type> types) {
        return new LookupResolver(
            types
                ?.Where(type => type is not null)
                ?.ToDictionary(type => new ObjectTypeInfo(type.Name)));
    }

    /// <summary>
    /// Returns an instance of <see cref="IObjectTypeResolver"/> for the types.
    /// </summary>
    /// <param name="types">The types.</param>
    /// <returns>The type resolver.</returns>
    public static IObjectTypeResolver Lookup(params Type[] types) {
        return Lookup(types?.AsEnumerable());
    }

    private sealed class LookupResolver(IReadOnlyDictionary<ObjectTypeInfo, Type> lookup) : IObjectTypeResolver {
        public IReadOnlyDictionary<ObjectTypeInfo, Type> Lookup { get; } = lookup;

        Type IObjectTypeResolver.Resolve(ObjectTypeInfo info) {
            return true == Lookup?.TryGetValue(info, out var type)
                ? type
                : null;
        }

        ObjectTypeInfo IObjectTypeResolver.Reverse(Type type) {
            var info = Lookup.Where(item => item.Value == type).Select(item => item.Key).FirstOrDefault();
            if (info is not null) {
                return info;
            }
            return null;
        }
    }
}
