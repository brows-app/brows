using System.Collections.Generic;

namespace Brows.Composition;

/// <summary>
/// Data used by implementations of <see cref="IExportAndVary"/>.
/// </summary>
public interface IExportVariables {
    /// <summary>
    /// Gets the types of which variables are defined.
    /// </summary>
    internal IEnumerable<Type> Keys { get; }

    /// <summary>
    /// Gets the variable of the specified type.
    /// </summary>
    /// <param name="key">The type of variable to get.</param>
    /// <returns>The variable of the specified type, or null if no variable exists.</returns>
    internal object Get(Type key);

    /// <summary>
    /// Gets the data item of the specified type that exists in the data.
    /// </summary>
    /// <typeparam name="TVariable">The type of data to get.</typeparam>
    /// <returns>
    /// The data of the specified type, or the default value for the type 
    /// if no data exists.
    /// </returns>
    TVariable Get<TVariable>() where TVariable : IExportVariable;
}
