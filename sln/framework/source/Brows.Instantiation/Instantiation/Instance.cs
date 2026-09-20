using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Brows.Instantiation;

/// <summary>
/// Provides instantiation of types derived from <typeparamref name="TBase"/>.
/// </summary>
/// <typeparam name="TBase">The base type of the instantiated objects.</typeparam>
public sealed class Instance<TBase> {
    private readonly Implementation<TBase> Implementation;
    private readonly Lazy<ReadOnlyCollection<TBase>> Factory;

    /// <summary>
    /// Gets the collection of instantiated objects of type <typeparamref name="TBase"/>.
    /// </summary>
    public ReadOnlyCollection<TBase> List => field ??= Factory.Value;

    /// <summary>
    /// Gets the collection of assemblies from which types are discovered
    /// for object instantiation.
    /// </summary>
    public IEnumerable<Assembly> Assemblies => Implementation.Assemblies;

    /// <summary>
    /// Initializes the instance with a collection of assemblies.
    /// </summary>
    /// <param name="assemblies">
    /// The collection of assemblies from which types are discovered
    /// for object instantiation.
    /// </param>
    public Instance(IEnumerable<Assembly> assemblies) {
        Implementation = new(assemblies);
        Factory = new(
            mode: LazyThreadSafetyMode.ExecutionAndPublication,
            valueFactory: () => Implementation
                .List
                .Select(type => (TBase)Activator.CreateInstance(type, nonPublic: true))
                .ToList()
                .AsReadOnly());
    }

    /// <summary>
    /// Initializes the instance with a collection of assemblies.
    /// </summary>
    /// <param name="assemblies">
    /// The collection of assemblies from which types are discovered
    /// for object instantiation.
    /// </param>
    public Instance(params Assembly[] assemblies) : this(assemblies?.AsEnumerable()) {
    }

    /// <summary>
    /// Creates a new collection of instances of <typeparamref name="TBase"/>
    /// from types found in the given <paramref name="assemblies"/>.
    /// </summary>
    /// <param name="assemblies">
    /// The collection of assemblies from which types are discovered
    /// for object instantiation.
    /// </param>
    /// <returns>
    /// A new collection of instances.
    /// </returns>
    public static ReadOnlyCollection<TBase> From(params Assembly[] assemblies) {
        return new Instance<TBase>(assemblies).List;
    }
}
