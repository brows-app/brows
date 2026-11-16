using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class ProtocolTestServer : IDisposable {
    private static void DeleteTemporaryRoot(string path) {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = Path.GetFullPath(Path.GetTempPath());
        var temporaryPrefix = temporaryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                              Path.DirectorySeparatorChar;
        var isChildOfTemporaryDirectory = fullPath.StartsWith(temporaryPrefix, StringComparison.OrdinalIgnoreCase);
        var hasExpectedName = Path.GetFileName(fullPath).StartsWith(
            "brows-protocol-test-ftp-",
            StringComparison.OrdinalIgnoreCase);
        if (!isChildOfTemporaryDirectory || !hasExpectedName) {
            throw new InvalidOperationException($"Refusing to remove a non-disposable test directory: {fullPath}");
        }
        if (Directory.Exists(fullPath)) {
            Directory.Delete(fullPath, recursive: true);
        }
    }

    private readonly Process Process;
    private readonly string RootPath;

    private static ReadyInfo Readiness(string json) {
        return JsonSerializer.Deserialize<ReadyInfo>(json) ??
               throw new InvalidDataException("The protocol test server returned empty readiness data.");
    }

    public ReadyInfo Info { get; }

    private ProtocolTestServer(Process process, string rootPath, ReadyInfo info) {
        Process = process;
        RootPath = rootPath;
        Info = info;
    }

    public static async Task<ProtocolTestServer> Start(CancellationToken token) {
        var scriptPath = ProtocolTestServerInfrastructure.FindServerScript(
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Environment.GetEnvironmentVariable("BROWS_PROTOCOL_TEST_SERVER_SCRIPT"),
            File.Exists) ??
            throw new FileNotFoundException("The protocol-test server script was not found.");
        var repositoryRoot = ProtocolTestServerInfrastructure.FindRepositoryRoot(scriptPath);
        var pythonPath = ProtocolTestServerInfrastructure.FindPython(
            repositoryRoot,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetEnvironmentVariable("BROWS_PROTOCOL_TEST_PYTHON"),
            File.Exists);
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "brows-protocol-test-ftp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        var readyPath = Path.Combine(rootPath, "ready.json");
        var startInfo = new ProcessStartInfo(pythonPath) {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        startInfo.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--ready-file");
        startInfo.ArgumentList.Add(readyPath);
        startInfo.ArgumentList.Add("--root");
        startInfo.ArgumentList.Add(rootPath);
        Process process = null;
        try {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Python.");
            var json = await ProtocolTestServerInfrastructure.WaitForReadinessAsync(
                process,
                readyPath,
                TimeSpan.FromSeconds(45),
                token);
            var info = Readiness(json);
            return new ProtocolTestServer(process, rootPath, info);
        }
        catch {
            if (process is not null) {
                try {
                    if (!process.HasExited) {
                        process.Kill(entireProcessTree: true);
                    }
                    process.WaitForExit(5000);
                }
                catch (InvalidOperationException) when (process.HasExited) {
                }
                finally {
                    process.Dispose();
                }
            }
            DeleteTemporaryRoot(rootPath);
            throw;
        }
    }

    public void CreateFileAfterNextListing(string name, byte[] contents) {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(contents);
        var nameIsNotSinglePathSegment = name.Contains('/') || name.Contains('\\');
        if (nameIsNotSinglePathSegment) {
            throw new ArgumentException("The test FTP file name must be a single path segment.", nameof(name));
        }
        var markerPath = Path.Combine(RootPath, "ftp-create-after-listing.json");
        File.Delete(Path.Combine(RootPath, "ftp-listing-started"));
        File.Delete(Path.Combine(RootPath, "ftp-listing-completed"));
        var marker = JsonSerializer.Serialize(new {
            name,
            contents = Convert.ToBase64String(contents)
        });
        File.WriteAllText(markerPath + ".tmp", marker);
        File.Move(markerPath + ".tmp", markerPath);
    }

    internal bool ListingRaceCompleted(string name) {
        return File.ReadAllText(Path.Combine(RootPath, "ftp-listing-started")) == name &&
            File.ReadAllText(Path.Combine(RootPath, "ftp-listing-completed")) == name;
    }

    internal void HoldSftpRequest(string command, string path) {
        File.Delete(Path.Combine(RootPath, "sftp-request-entered"));
        File.Delete(Path.Combine(RootPath, "sftp-request-release"));
        var marker = Path.Combine(RootPath, "sftp-request.json");
        File.WriteAllText(marker + ".tmp", JsonSerializer.Serialize(new { command, path }));
        File.Move(marker + ".tmp", marker);
    }

    internal async Task WaitForSftpRequest(CancellationToken token) {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!File.Exists(Path.Combine(RootPath, "sftp-request-entered"))) {
            if (Process.HasExited) {
                throw new InvalidOperationException("The protocol test server exited.");
            }
            await Task.Delay(10, timeout.Token);
        }
    }

    internal void ReleaseSftpRequest() {
        File.WriteAllText(Path.Combine(RootPath, "sftp-request-release"), "");
    }

    public void Dispose() {
        if (!Process.HasExited) {
            Process.Kill(entireProcessTree: true);
            Process.WaitForExit(5000);
        }
        Process.Dispose();
        DeleteTemporaryRoot(RootPath);
    }

    internal sealed class ReadyInfo {
        [JsonPropertyName("sftpPort")]
        public int SftpPort { get; init; }
        [JsonPropertyName("knownHostsPath")]
        public string KnownHostsPath { get; init; }
        [JsonPropertyName("host")]
        public string Host { get; init; }
        [JsonPropertyName("ftpPort")]
        public int FtpPort { get; init; }
        [JsonPropertyName("ftpSessionLimitedPort")]
        public int FtpSessionLimitedPort { get; init; }
        [JsonPropertyName("ftpSessionLimit")]
        public int FtpSessionLimit { get; init; }
        [JsonPropertyName("explicitFtpsPort")]
        public int ExplicitFtpsPort { get; init; }
        [JsonPropertyName("explicitFtpsDnsOnlyPort")]
        public int ExplicitFtpsDnsOnlyPort { get; init; }
        [JsonPropertyName("implicitFtpsPort")]
        public int ImplicitFtpsPort { get; init; }
        [JsonPropertyName("implicitFtpsDnsOnlyPort")]
        public int ImplicitFtpsDnsOnlyPort { get; init; }
        [JsonPropertyName("implicitFtpsRejectDataTlsPort")]
        public int ImplicitFtpsRejectDataTlsPort { get; init; }
        [JsonPropertyName("ipv6Host")]
        public string Ipv6Host { get; init; }
        [JsonPropertyName("ftpIpv6Port")]
        public int? FtpIpv6Port { get; init; }
        [JsonPropertyName("explicitFtpsIpv6Port")]
        public int? ExplicitFtpsIpv6Port { get; init; }
        [JsonPropertyName("implicitFtpsIpv6Port")]
        public int? ImplicitFtpsIpv6Port { get; init; }
        [JsonPropertyName("username")]
        public string Username { get; init; }
        [JsonPropertyName("loginHomeUsername")]
        public string LoginHomeUsername { get; init; }
        [JsonPropertyName("password")]
        public string Password { get; init; }
        [JsonPropertyName("caPath")]
        public string CaPath { get; init; }
        [JsonPropertyName("certPath")]
        public string CertPath { get; init; }
        [JsonPropertyName("rootPath")]
        public string RootPath { get; init; }
        [JsonPropertyName("fixturePath")]
        public string FixturePath { get; init; }
        [JsonPropertyName("posixOnlyFileAvailable")]
        public bool PosixOnlyFileAvailable { get; init; }
        [JsonPropertyName("symlinksAvailable")]
        public bool SymlinksAvailable { get; init; }
    }
}
