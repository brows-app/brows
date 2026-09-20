using NUnit.Framework;
using System;
using System.Linq;

namespace Brows;

/// <summary>
/// Provides information  and acts on tests.
/// </summary>
public static class BrowsTest {
    /// <summary>
    /// Returns the environment in which tests are currently run.
    /// </summary>
    /// <returns>The environment in which tests are currently run.</returns>
    public static BrowsTestEnv Env() {
        var value = Environment.GetEnvironmentVariable("BROWS_TEST_ENV");
        return Enum.TryParse<BrowsTestEnv>(value, out var result)
            ? result
            : BrowsTestEnv.Default;
    }

    /// <summary>
    /// Throws an <see cref="IgnoreException"/> if the current environment exists
    /// in <paramref name="envs"/>.
    /// </summary>
    /// <param name="envs">
    /// The list of environments. If the current environment exists in the list,
    /// a <see cref="IgnoreException"/> is thrown.
    /// </param>
    public static void Ignore(params BrowsTestEnv[] envs) {
        if (envs is null) {
            return;
        }
        var envCurrent = Env();
        if (envs.Any(env => env == envCurrent)) {
            Assert.Ignore($"Ignored in env '{envCurrent}'");
        }
    }
}
