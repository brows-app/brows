using Brows.Composition;

namespace Brows.Config;

/// <summary>
/// Local configuration for the current user of the system.
/// </summary>
public interface IConfigUser : IConfig, IExport {
}
