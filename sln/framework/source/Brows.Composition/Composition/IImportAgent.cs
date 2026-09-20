using System.Collections.Generic;

namespace Brows.Composition;

/// <summary>
/// Provides an interface that enables constructing instances of types based on imports,
/// populating the imports of existing objects, and discovering the
/// <see cref="IExport"/> instances available to the extension framework.
/// </summary>
public interface IImportAgent {
    /// <summary>
    /// Gets a flag that indicates whether or not the imports are ready
    /// for use, i.e. whether or not they've been initialized.
    /// </summary>
    bool Ready { get; }

    /// <summary>
    /// Creates a new instance of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of instance to be created.</typeparam>
    /// <returns>The new instance of <typeparamref name="T"/>.</returns>
    T Construct<T>();

    /// <summary>
    /// Populates an object's imports.
    /// </summary>
    /// <param name="obj">The object whose imports should be populated.</param>
    /// <param name="force">
    /// True to force population of the object even after
    /// detecting that it has already been populated. To skip population of
    /// objects that have already been populated, set to false. The default
    /// value is false.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="obj"/> is null.
    /// </exception>
    void Populate(object obj, bool force = false);

    /// <summary>
    /// Gets the collection of instances of type <typeparamref name="T"/> available to the extension framework.
    /// </summary>
    /// <typeparam name="T">The type of implementation of <see cref="IExport"/> that the list contains.</typeparam>
    /// <param name="throwIfNotReady">
    /// A flag that indicates whether or not an exception is thrown when <see cref="Ready"/> is false.
    /// If true, an exception is thrown if <see cref="Ready"/> is false.
    /// If false, and <see cref="Ready"/> is false, an empty list is returned.
    /// </param>
    /// <returns>
    /// The list of imported instances. If no instances are available, the list contains zero items.
    /// </returns>
    IReadOnlyList<T> List<T>(bool throwIfNotReady = true) where T : IExport;

    /// <summary>
    /// Finds the first instance of <typeparamref name="T"/> available to the extension framework.
    /// </summary>
    /// <typeparam name="T">The type of implementation of <see cref="IExport"/> to find.</typeparam>
    /// <param name="throwIfNotFound">
    /// A flag that indicates whether or not an exception is thrown when an instance of <typeparamref name="T"/>
    /// is not found. If true, an exception is thrown if no instance is found. If false, null is returned if
    /// no instance is found.
    /// </param>
    /// <param name="throwIfNotReady">
    /// A flag that indicates whether or not an exception is thrown when <see cref="Ready"/> is false.
    /// If true, an exception is thrown if <see cref="Ready"/> is false.
    /// If false, and <see cref="Ready"/> is false, the default value of <typeparamref name="T"/>
    /// is returned.
    /// </param>
    /// <returns>
    /// The first instance of <typeparamref name="T"/> found by the framework,
    /// or null if no instance is found and <paramref name="throwIfNotFound"/> is false,
    /// or if <see cref="Ready"/> is false and <paramref name="throwIfNotReady"/> is false.
    /// </returns>
    /// <exception cref="ImportNotFoundException">
    /// Thrown if no instance of <typeparamref name="T"/> is found and <paramref name="throwIfNotFound"/> is true.
    /// </exception>
    T Find<T>(bool throwIfNotFound = false, bool throwIfNotReady = true) where T : IExport;
}
