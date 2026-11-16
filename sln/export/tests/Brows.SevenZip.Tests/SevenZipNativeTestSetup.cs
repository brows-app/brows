using Brows.SevenZip;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Loads the native library that talks to the 7-Zip engine before the tests
/// of the plugin are run.
/// </summary>
[SetUpFixture]
[NonParallelizable]
internal sealed class SevenZipNativeTestSetup {
    [OneTimeSetUp]
    public async Task LoadNativeSevenZip() {
        var directory = Path.Combine(AppContext.BaseDirectory, "brows.native", "x64-windows");
        foreach (var name in new[] { "brows_7z.dll", "brows_framework.dll", "7zip.dll" }) {
            var path = Path.Combine(directory, name);
            if (File.Exists(path) == false) {
                throw new FileNotFoundException(
                    $"The native library '{path}' does not exist. " +
                    "Build the native project Brows.SevenZip into sln/out/<Configuration>/brows.native/x64-windows.");
            }
        }
        var native = new SevenZipNative();
        var loaded = await native.Loaded(CancellationToken.None);
        if (loaded == false) {
            throw new InvalidOperationException("The native library of the 7-Zip plugin could not be loaded.");
        }
    }
}
