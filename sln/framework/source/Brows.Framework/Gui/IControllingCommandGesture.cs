using Brows.Commands;
using Brows.Providers;

namespace Brows.Gui;

/// <summary>
/// Defines a source that restricts which commands can handle a gesture.
/// </summary>
/// <remarks>
/// Gesture dispatch from this source is limited to configured commands accepted by
/// <see cref="Allows(ICommand)"/>. Dispatch also requires that the active panel owns the same
/// <see cref="Provider"/> instance. The panel is not activated during scoped dispatch.
/// A source that owns text input should implement
/// <see cref="IControllingText"/> separately to suppress text forwarding.
/// </remarks>
public interface IControllingCommandGesture : IControllingGesture {
    /// <summary>
    /// Gets the provider that owns this gesture source.
    /// </summary>
    IProvider Provider { get; }

    /// <summary>
    /// Determines whether a configured command may handle a gesture from this source.
    /// </summary>
    /// <param name="command">
    /// The configured command to evaluate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the command may handle the gesture; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    bool Allows(ICommand command);
}