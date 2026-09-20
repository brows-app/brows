using Brows.Composition;
using System.Collections.Generic;
using System.Linq;

namespace Brows;

/// <summary>
/// Variables made available to instances of <see cref="IExportAndVary"/>
/// during import initialization.
/// </summary>
public sealed class ImportVariables {
    private readonly IReadOnlyDictionary<Type, object> Lookup;

    internal IExportVariables ExportVariables =>
        new ExportVariablesImplementation(Lookup);

    public ImportVariables(params IExportVariable[] items) : this(items?.AsEnumerable()) {
    }

    public ImportVariables(IEnumerable<IExportVariable> items) {
        Lookup = (items ?? [])
            .Where(item => item is not null)
            .Cast<object>()
            .GroupBy(item => item.GetType())
            .ToDictionary(
                group => group.Key,
                group => group.Last());
    }

    /// <summary>
    /// Sets the variable of type <typeparamref name="TVariable"/>.
    /// </summary>
    /// <typeparam name="TVariable">The type of the variable to set.</typeparam>
    /// <param name="value">The value of the variable.</param>
    /// <returns>
    /// The instance of <see cref="ImportVariables"/> on which the method 
    /// was called.
    /// </returns>
    /// <remarks>
    /// The type in <typeparamref name="TVariable"/> is used as the parameter to
    /// <see cref="IExportVariables.Get{T}"/> when resolving variables.
    /// </remarks>
    public ImportVariables Set<TVariable>(TVariable value) where TVariable : IExportVariable {
        return new(
            Lookup
                .Values
                .OfType<IExportVariable>()
                .Concat(new[] { value as IExportVariable }));
    }

    private sealed class ExportVariablesImplementation : IExportVariables {
        internal IReadOnlyDictionary<Type, object> Lookup { get; }

        internal ExportVariablesImplementation(IReadOnlyDictionary<Type, object> lookup) {
            Lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        }

        IEnumerable<Type> IExportVariables.Keys => Lookup.Keys;

        object IExportVariables.Get(Type key) {
            return Lookup.TryGetValue(key, out var value)
                ? value
                : default;
        }

        TVariable IExportVariables.Get<TVariable>() {
            return Lookup.TryGetValue(typeof(TVariable), out var value)
                ? (TVariable)value
                : default;
        }
    }
}
