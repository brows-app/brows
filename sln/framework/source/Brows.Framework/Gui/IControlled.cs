namespace Brows.Gui;

/// <summary>
/// Accepts the controller that connects an object to its view.
/// </summary>
/// <typeparam name="TController">
/// The controller type accepted by the object.
/// </typeparam>
public interface IControlled<TController> {
    /// <summary>
    /// Sets the controller for this object.
    /// </summary>
    TController Controller { set; }
}
