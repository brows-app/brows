using Brows.Operations;
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using IOFile = System.IO.File;

namespace Brows.Updates;

internal sealed class UpdateDownload {
    private HttpClient Http { get; }

    private static async Task<bool> Verified(string path, AppUpdatePackage package, CancellationToken token) {
        using (var stream = IOFile.OpenRead(path)) {
            return stream.Length == package.Size
                && Matches(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false), package);
        }
    }

    private static bool Matches(byte[] hash, AppUpdatePackage package) {
        return string.Equals(Convert.ToHexString(hash), package.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private async Task Transfer(AppUpdatePackage package, string path, IOperationProgress progress,
                                CancellationToken token) {
        using (var response = await Http.GetAsync(package.Url, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false)) {
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long size && size != package.Size) {
                throw new InvalidDataException("The installer Content-Length differs from its published size.");
            }

            using (var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
                       bufferSize: 1, useAsync: true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (var idle = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                var buffer = new byte[81920];
                var length = 0L;
                while (true) {
                    idle.CancelAfter(IdleTimeout);
                    int read;
                    try {
                        read = await source.ReadAsync(buffer.AsMemory(), idle.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex) when (!token.IsCancellationRequested) {
                        throw new TimeoutException("The installer download stalled.", ex);
                    }
                    idle.CancelAfter(Timeout.InfiniteTimeSpan);
                    if (read == 0) {
                        break;
                    }
                    length += read;
                    if (length > package.Size) {
                        throw new InvalidDataException("The installer exceeds its published size.");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    hash.AppendData(buffer, 0, read);
                    progress.Change(addProgress: read);
                }

                var verified = length == package.Size && Matches(hash.GetHashAndReset(), package);
                if (!verified) {
                    throw new InvalidDataException("The installer does not match its published size or SHA-256.");
                }
            }
        }
    }

    internal TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(60);

    internal UpdateDownload(HttpClient http) {
        Http = http ?? throw new ArgumentNullException(nameof(http));
    }

    internal async Task<string> File(AppUpdate update, string directory, IOperationProgress progress,
                                     CancellationToken token) {
        var package = update.Package;
        var path = Path.Combine(directory, package.Name);
        var partial = path + ".partial";
        Directory.CreateDirectory(directory);
        try {
            progress.Change(setProgress: 0, setTarget: package.Size, kind: OperationProgressKind.FileSize);
            if (IOFile.Exists(path)) {
                if (await Verified(path, package, token).ConfigureAwait(false)) {
                    progress.Change(setProgress: package.Size);
                    return path;
                }
                IOFile.Delete(path);
            }

            await Transfer(package, partial, progress, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            IOFile.Move(partial, path, overwrite: true);
            return path;
        }
        finally {
            IOFile.Delete(partial);
        }
    }
}
