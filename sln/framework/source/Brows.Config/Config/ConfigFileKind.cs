namespace Brows.Config;

/// <summary>
/// Kinds of information that are stored via the framework.
/// </summary>
internal enum ConfigFileKind {
    /// <summary>
    /// Configuration that has more to do with how the system
    /// behaves than with what the system has done.
    /// </summary>
    Conf,

    /// <summary>
    /// Similar to <see cref="Conf"/>, but with
    /// a different serialization format.
    /// </summary>
    Json,

    /// <summary>
    /// Information that has more to do with what the system 
    /// has done than with how the system behaves.
    /// </summary>
    Data,
}
