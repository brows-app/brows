using Brows.Composition;
using System.Threading.Tasks;

namespace Brows.IPC;

/// <summary>
/// Base type for implementations of <see cref="IObjectCallDelegate"/>.
/// </summary>
public abstract class ObjectCallDelegate : IObjectCallDelegate {
    /// <summary>
    /// When overridden in a derived class, gets the type resolver used by the delegate.
    /// </summary>
    public abstract IObjectTypeResolver TypeResolver { get; }

    /// <summary>
    /// When overridden in a derived class, processes input and produces output.
    /// </summary>
    /// <param name="input">The input argument.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that results in the output of the process.</returns>
    public abstract Task<object> Call(object input, CancellationToken token);

    /// <summary>
    /// Implementation of <see cref="IObjectCallFactory"/> that creates instances
    /// of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IObjectCallDelegate"/> to create.</typeparam>
    protected internal abstract class Factory<T> : IObjectCallFactory where T : IObjectCallDelegate {
        [ImportRequired]
        internal IImportAgent Imports { get; set; }

        string IObjectCallFactory.Name => typeof(T).Name;

        IObjectCallDelegate IObjectCallFactory.Create() {
            var imports = Imports;
            if (imports is null) {
                throw new InvalidOperationException("The import agent is null.");
            }
            return imports.Construct<T>();
        }
    }
}
