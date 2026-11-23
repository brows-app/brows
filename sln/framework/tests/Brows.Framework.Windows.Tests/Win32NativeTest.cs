using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Brows;

[TestFixture]
internal sealed class Win32NativeTest {
    private static string NativeDirectory() {
        var configuration = typeof(Win32NativeTest).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?
            .Configuration ?? "";
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null) {
            var candidate = Path.Combine(directory.FullName, "out", configuration, "brows.native", "x64-windows");
            if (Directory.Exists(candidate)) {
                return candidate;
            }
            directory = directory.Parent;
        }
        return null;
    }

    [Test]
    public void Dll_ExportsEveryDeclaredEntryPoint() {
        var nativeDirectory = NativeDirectory();
        if (nativeDirectory is null) {
            Assert.Ignore("The native build output was not found. Build the solution first.");
        }
        var path = Path.Combine(nativeDirectory, Win32Native.Dll);
        Assert.That(File.Exists(path), Is.True, $"{Win32Native.Dll} is not built into {nativeDirectory}.");

        var entryPoints = typeof(Win32Native)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Select(method => (Method: method, Import: method.GetCustomAttribute<DllImportAttribute>()))
            .Where(item => item.Import is not null)
            .Select(item => item.Import.EntryPoint ?? item.Method.Name)
            .ToList();
        Assert.That(entryPoints, Is.Not.Empty);

        var handle = NativeLibrary.Load(path);
        try {
            Assert.Multiple(() => {
                foreach (var entryPoint in entryPoints) {
                    Assert.That(NativeLibrary.TryGetExport(handle, entryPoint, out _), Is.True, entryPoint);
                }
            });
        }
        finally {
            NativeLibrary.Free(handle);
        }
    }
}
