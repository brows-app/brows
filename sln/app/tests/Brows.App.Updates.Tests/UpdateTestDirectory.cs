using Brows.Updates;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Brows;

internal sealed class UpdateTestDirectory : IDisposable {
    internal string Root { get; } = Directory.CreateTempSubdirectory("brows update = O'Brien-").FullName;
    internal byte[] Bytes { get; } = [1, 2, 3, 4];

    internal UpdateInstallation Installation(Architecture os = Architecture.X64, bool installable = true,
        string version = "0.103.0.60") {
        return new(version, Architecture.X64, os, Path.Combine(Root, installable ? "app" : "Debug"), Root);
    }

    internal AppUpdate Update() {
        return new() {
            Version = "0.104.0.61",
            Architecture = "x64",
            Page = new("https://github.com/owner/name/releases/tag/v0.104.0.61"),
            Package = new() {
                Name = "brows-x64-0.104.0.61.msi",
                Url = new("https://github.com/owner/name/releases/download/v0.104.0.61/brows-x64-0.104.0.61.msi"),
                Size = Bytes.Length,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(Bytes))
            }
        };
    }

    void IDisposable.Dispose() {
        Directory.Delete(Root, recursive: true);
    }
}
