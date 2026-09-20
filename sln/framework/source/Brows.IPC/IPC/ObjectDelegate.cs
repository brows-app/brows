using System.Threading.Tasks;

namespace Brows.IPC;

/// <summary>
/// Base class for types that implement both <see cref="IObjectPostDelegate"/>
/// and <see cref="IObjectCallDelegate"/>.
/// </summary>
public abstract class ObjectDelegate : ObjectPostDelegate, IObjectCallDelegate {
    /// <summary>
    /// When overridden in a derived class, rocesses input and produces output.
    /// </summary>
    /// <param name="input">The input argument.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that results in the output of the process.</returns>
    public abstract Task<object> Call(object input, CancellationToken token);

    /// <summary>
    /// Base class for factories that create instances of both <see cref="IObjectPostDelegate"/>
    /// and <see cref="IObjectCallDelegate"/>.
    /// </summary>
    /// <typeparam name="T">The type of instance to create.</typeparam>
    new protected internal abstract class Factory<T> : ObjectPostDelegate.Factory<T>, IObjectCallFactory
    where T : IObjectPostDelegate, IObjectCallDelegate {
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
