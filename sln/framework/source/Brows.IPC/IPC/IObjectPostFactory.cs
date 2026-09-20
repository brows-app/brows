using Brows.Composition;

namespace Brows.IPC;

/// <summary>
/// Contract for factory types that create instances of <see cref="IObjectPostDelegate"/>.
/// </summary>
/// <remarks>
/// This contract participates in the DI framework and may be exported.
/// See <see cref="IExport"/>.
/// </remarks>
public interface IObjectPostFactory : IExport {
    /// <summary>
    /// The name of the factory and/or the delegate that is created.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Creates an instance of <see cref="IObjectPostDelegate"/>.
    /// </summary>
    /// <returns>The created instance of <see cref="IObjectPostDelegate"/>.</returns>
    IObjectPostDelegate Create();
}
