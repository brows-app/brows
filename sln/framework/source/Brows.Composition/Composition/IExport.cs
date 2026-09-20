using System.ComponentModel.Composition;

namespace Brows.Composition;

/// <summary>
/// Implementations of this interface are exposed to importers in the framework.
/// A single instance of the implementation is created at the start of the 
/// program and lives for the duration of the program. This is a marker interface
/// and has no members.
/// </summary>
[InheritedExport(typeof(IExport))]
public interface IExport {
}
