using System.Globalization;
using System.IO;
using System.Text;

namespace Brows;

internal sealed class ZipTestSandbox : IDisposable {
    private const string MarkerName = ".brows-zip-test-root";
    private readonly string Marker;

    public string Root { get; }

    public ZipTestSandbox() {
        Root = Path.Combine(Path.GetTempPath(), "Brows.Zip.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Marker = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(Root, MarkerName), Marker, new UTF8Encoding(false));
    }

    public string PathFor(string name) {
        return Path.Combine(Root, name.Replace('/', Path.DirectorySeparatorChar));
    }

    public static string ProviderID(string fileName, string directory = "", params string[] nestedMembers) {
        var builder = new StringBuilder("zip:1|f=");
        builder.Append(Encode(fileName));
        foreach (var nestedMember in nestedMembers) {
            builder.Append("|n=");
            builder.Append(Encode(nestedMember));
        }
        builder.Append("|d=");
        builder.Append(Encode(directory));
        return builder.ToString();
    }

    private static string Encode(string value) {
        var bytes = new UTF8Encoding(false, true).GetBytes(value);
        var builder = new StringBuilder(bytes.Length);
        foreach (var currentByte in bytes) {
            var isLetterOrDigit = currentByte >= 'A' && currentByte <= 'Z'
                || currentByte >= 'a' && currentByte <= 'z'
                || currentByte >= '0' && currentByte <= '9';
            var isOtherSafe = currentByte is (byte)'-' or (byte)'.' or (byte)'_' or (byte)'~'
                or (byte)'/' or (byte)'\\' or (byte)':';
            if (isLetterOrDigit || isOtherSafe) {
                builder.Append((char)currentByte);
            }
            else {
                builder.Append('%');
                builder.Append(currentByte.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return builder.ToString();
    }

    public void Dispose() {
        var fullRoot = Path.GetFullPath(Root);
        var tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Brows.Zip.Tests"));
        var rootPrefix = Path.EndsInDirectorySeparator(tempRoot)
            ? tempRoot
            : tempRoot + Path.DirectorySeparatorChar;
        var isWithinTempRoot = fullRoot.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
        if (isWithinTempRoot is false) {
            throw new InvalidOperationException("Refusing to remove a path outside the ZIP test directory.");
        }

        var markerPath = Path.Combine(fullRoot, MarkerName);
        var markerIsOwned = File.Exists(markerPath) &&
                            string.Equals(File.ReadAllText(markerPath), Marker, StringComparison.Ordinal);
        if (markerIsOwned is false) {
            throw new InvalidOperationException("Refusing to remove a ZIP test directory without its marker.");
        }
        Directory.Delete(fullRoot, recursive: true);
    }
}
