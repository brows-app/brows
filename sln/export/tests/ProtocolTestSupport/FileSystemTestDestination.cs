using Brows.Composition;
using Brows.Providers;
using System;
using System.IO;
using System.Reflection;

namespace Brows;

internal sealed class FileSystemTestDestination : IDisposable {
    internal Provider Provider { get; }
    internal ICopyProvidedIO Copy { get; }

    internal FileSystemTestDestination(string path) {
        var assembly = Assembly.Load("Brows.FileSystem");
        var providerType = assembly.GetType("Brows.FileSystemProvider", true);
        var factoryType = assembly.GetType("Brows.FileSystemProviderFactory", true);
        Provider = (Provider)Activator.CreateInstance(providerType,
            [Activator.CreateInstance(factoryType, true), Directory.CreateDirectory(path), false, 0]);
        Copy = (ICopyProvidedIO)Activator.CreateInstance(
            providerType.GetNestedType("CopyProvidedIO", BindingFlags.NonPublic), true);
    }

    public void Dispose() {
        Provider.Dispose();
    }
}
