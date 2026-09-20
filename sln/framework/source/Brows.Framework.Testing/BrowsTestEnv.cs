namespace Brows;

/// <summary>
/// Environment in which tests are running.
/// </summary>
public enum BrowsTestEnv {
    /// <summary>
    /// The default environment.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Tests are running as a result of a GitHub action.
    /// </summary>
    GitHubAction = 1
}
