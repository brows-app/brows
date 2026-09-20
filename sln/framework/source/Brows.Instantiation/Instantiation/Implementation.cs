using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Brows.Instantiation;

/// <summary>
/// Provides implementations of types derived from <typeparamref name="TBase"/>.
/// </summary>
/// <typeparam name="TBase">The base type of the implementations.</typeparam>
public sealed class Implementation<TBase> {
    private readonly AssemblyCollection AssemblyCollection;
    private readonly Lazy<ReadOnlyCollection<Type>> Factory;

    /// <summary>
    /// Gets the collection of implementations derived from <typeparamref name="TBase"/>.
    /// </summary>
    public ReadOnlyCollection<Type> List => field ??= Factory.Value;

    /// <summary>
    /// Gets a predicate used to filter available implementations.
    /// </summary>
    public Func<Type, bool> Predicate { get; }

    /// <summary>
    /// Gets the collection of assemblies from which types are discovered
    /// for implementations.
    /// </summary>
    public IEnumerable<Assembly> Assemblies => AssemblyCollection;

    /// <summary>
    /// Initializes the instance with a collection of assemblies.
    /// </summary>
    /// <param name="assemblies">
    /// The collection of assemblies from which types are discovered
    /// for implementations.
    /// </param>
    /// <param name="predicate">
    /// A predicate used to filter available implementations.
    /// </param>
    public Implementation(IEnumerable<Assembly> assemblies, Func<Type, bool> predicate = null) {
        AssemblyCollection = new(assemblies);
        Predicate = predicate;
        Factory = new(
            mode: LazyThreadSafetyMode.PublicationOnly,
            valueFactory: () => AssemblyCollection
                .Types
                .Where(type => type.IsClass == true)
                .Where(type => type.IsAbstract == false)
                .Where(type => type.ContainsGenericParameters == false)
                .Where(type => typeof(TBase).IsAssignableFrom(type))
                .Where(Predicate ?? (_ => true))
                .ToList()
                .AsReadOnly());
    }

    /// <summary>
    /// Initializes the instance with a collection of assemblies.
    /// </summary>
    /// <param name="assemblies">
    /// The collection of assemblies from which types are discovered
    /// for implementations.
    /// </param>
    public Implementation(params Assembly[] assemblies) : this(assemblies?.AsEnumerable()) {
    }

    /// <summary>
    /// Creates a new collection of implementations derived from <typeparamref name="TBase"/>
    /// found in the given <paramref name="assemblies"/>.
    /// </summary>
    /// <param name="assemblies">
    /// The collection of assemblies from which types are discovered
    /// for implementations.
    /// </param>
    /// <param name="predicate">
    /// A predicate used to filter available implementations.
    /// </param>
    /// <returns>
    /// A new collection of implementations.
    /// </returns>
    public static ReadOnlyCollection<Type> From(IEnumerable<Assembly> assemblies, Func<Type, bool> predicate = null) {
        return new Implementation<TBase>(assemblies, predicate).List;
    }

    /// <summary>
    /// Creates a new collection of implementations derived from <typeparamref name="TBase"/>
    /// found in the given <paramref name="assemblies"/>.
    /// </summary>
    /// <param name="assemblies">
    /// The collection of assemblies from which types are discovered
    /// for implementations.
    /// </param>
    /// <returns>
    /// A new collection of implementations.
    /// </returns>
    public static ReadOnlyCollection<Type> From(params Assembly[] assemblies) {
        return new Implementation<TBase>(assemblies).List;
    }
}
