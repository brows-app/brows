using Brows.Gui;
using System;
using System.Threading.Tasks;

namespace Brows.Providers;

/// <summary>
/// Provides the dispatcher boundary used by a provider detail set.
/// </summary>
public interface IProviderDetailSetController : IController {
    /// <summary>
    /// Runs provider detail transitions on the owning user interface dispatcher.
    /// </summary>
    /// <param name="work">
    /// The asynchronous view update to run on the owning user interface thread.
    /// </param>
    /// <returns>
    /// A task that completes when the update finishes.
    /// </returns>
    Task InvokeAsync(Func<Task> work);
}