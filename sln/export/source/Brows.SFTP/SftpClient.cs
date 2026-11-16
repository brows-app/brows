using Brows.SFTP.Native;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using NativeSftpConnection = Brows.SFTP.Native.SftpConnection;
using NativeSftp = Brows.SFTP.Native.Sftp;

namespace Brows;

internal sealed class SftpClient : IAsyncDisposable, ISftpFileOperations {
    private const uint OpenFlagRead = 1;
    private const uint OpenFlagWrite = 2;
    private const uint OpenFlagCreate = 8;
    private const uint OpenFlagTruncate = 16;
    private const uint OpenFlagExclusive = 32;
    private const uint DefaultFilePermissions = 0x1A4;
    private const uint DefaultDirectoryPermissions = 0x1ED;

    private static readonly ILog Log = Logging.For(typeof(SftpClient));
    private static readonly TimeSpan HandleGateWaitTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DisposalGateWaitTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DisposalAbortGateWaitTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SlowGateWait = TimeSpan.FromMilliseconds(500);

    private readonly SemaphoreSlim Gate = new(1, 1);
    private readonly SemaphoreSlim ConnectionGate = new(1, 1);
    private readonly CancellationTokenSource Shutdown = new();
    private readonly HashSet<IDisposable> Handles = [];
    private readonly Lock HandleLocker = new();
    private readonly Lock StateLocker = new();
    private readonly Dictionary<IDisposable, CancellationTokenSource> HandleTokens = [];
    private readonly TaskCompletionSource DisposalCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly NativeSftpConnection Connection = new();
    private SftpKnownHostStatus HostStatus = SftpKnownHostStatus.NotFound;
    private string HostFingerprint;
    private string HostKey;
    private bool Connected;
    private bool HostKeyApproved;
    private bool Authenticated;
    private int DisposeStarted;
    private int Disposed;
    private int TransportAborted;
    private int ProviderCount;

    private SftpKnownHosts KnownHosts => field ??= new SftpKnownHosts(Connection);
    private NativeSftp Sftp => field ??= new NativeSftp(Connection);
    private string Describe => $"{Username}@{HostName}:{Port}";
    private Task DeferredCleanup { get; set; }

    internal Uri Uri { get; }
    internal string KnownHostsFile { get; }

    private async Task<T> Run<T>(Func<CancellationToken, T> work,
                                 CancellationToken token,
                                 CancellationToken handleToken = default) {
        ArgumentNullException.ThrowIfNull(work);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref Disposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, Shutdown.Token, handleToken);
        var waitStarted = Stopwatch.GetTimestamp();
        try {
            await Gate.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested && Volatile.Read(ref Disposed) != 0) {
            throw new ObjectDisposedException(nameof(SftpClient));
        }
        var waited = Stopwatch.GetElapsedTime(waitStarted);
        if (waited >= SlowGateWait) {
            if (Log.Debug()) {
                Log.Debug(Log.Join("SFTP request waited for the session", Describe, waited));
            }
        }
        try {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref Disposed) != 0, this);
            linked.Token.ThrowIfCancellationRequested();
            if (Connection.IsAborted) {
                MarkTransportAborted();
                throw new IOException("The SFTP connection was aborted. Reopen the location to reconnect.");
            }
            try {
                return await Task.Run(() => work(linked.Token), linked.Token).ConfigureAwait(false);
            }
            catch (Exception) when (token.IsCancellationRequested) {
                throw new OperationCanceledException(token);
            }
            catch (Exception) when (Volatile.Read(ref Disposed) != 0) {
                throw new ObjectDisposedException(nameof(SftpClient));
            }
        }
        finally {
            try {
                if (Connection.IsAborted) {
                    MarkTransportAborted();
                }
            }
            finally {
                Gate.Release();
            }
        }
    }

    private void MarkTransportAborted() {
        var first = Interlocked.Exchange(ref TransportAborted, 1) == 0;
        if (first) {
            if (Log.Warn()) {
                Log.Warn(Log.Join("SFTP connection aborted", Describe));
            }
        }
    }

    private bool Failed(string operation, string path, Exception error, CancellationToken token) {
        var canceled = error is OperationCanceledException && token.IsCancellationRequested;
        if (canceled) {
            if (Log.Debug()) {
                Log.Debug(Log.Join("SFTP operation canceled", Describe, operation, path));
            }
            return false;
        }
        if (Log.Warn()) {
            Log.Warn(Log.Join("SFTP operation failed", Describe, operation, path, error.GetType().Name, error.Message));
        }
        return false;
    }

    private void AddHandle(IDisposable handle) {
        Handles.Add(handle);
        lock (HandleLocker) {
            HandleTokens.Add(handle, new CancellationTokenSource());
        }
    }

    private CancellationToken HandleToken(IDisposable handle) {
        lock (HandleLocker) {
            if (!HandleTokens.TryGetValue(handle, out var source)) {
                throw new ObjectDisposedException(handle.GetType().FullName);
            }
            return source.Token;
        }
    }

    private void CancelHandle(IDisposable handle) {
        CancellationTokenSource source;
        lock (HandleLocker) {
            HandleTokens.TryGetValue(handle, out source);
        }
        source?.Cancel();
    }

    private bool RemoveHandle(IDisposable handle) {
        var removed = Handles.Remove(handle);
        lock (HandleLocker) {
            if (removed) {
                HandleTokens.Remove(handle);
            }
        }
        return removed;
    }

    private async Task<bool> WaitForHandleGate() {
        return await Gate.WaitAsync(HandleGateWaitTimeout).ConfigureAwait(false);
    }

    private async Task<bool> TryAddresses(IReadOnlyList<IPAddress> addresses, CancellationToken token) {
        Exception lastException = null;
        foreach (var address in addresses) {
            token.ThrowIfCancellationRequested();
            try {
                await Run(runToken => {
                    Connection.SetAddress(address);
                    Connection.SetPort(Port);
                    Connection.SetUsername(Username);
                    Connection.Connect(runToken);
                    return true;
                }, token).ConfigureAwait(false);
                if (Log.Info()) {
                    Log.Info(Log.Join("SFTP connected", Describe, address));
                }
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                await CloseFailedConnection().ConfigureAwait(false);
                throw;
            }
            catch (ObjectDisposedException) when (Volatile.Read(ref Disposed) != 0) {
                throw;
            }
            catch (Exception ex) {
                lastException = ex;
                if (Log.Info()) {
                    Log.Info(Log.Join("SFTP connect address failed", Describe, address, ex.Message));
                }
                try {
                    await Run(_ => {
                        Connection.Close();
                        return true;
                    }, CancellationToken.None).ConfigureAwait(false);
                }
                catch {
                }
            }
        }
        if (lastException is not null) {
            throw new IOException($"Unable to connect to SFTP host {HostName}:{Port}.", lastException);
        }
        return false;
    }

    private async Task ReadHostKey(CancellationToken token) {
        var knownHostsFile = KnownHostsFile;
        using var knownHost = new SftpKnownHost();
        var host = await Run(_ => {
            var key = (string)null;
            var status = KnownHostStatus.NotFound;
            if (File.Exists(knownHostsFile)) {
                KnownHosts.Check(knownHostsFile, HostName, Port, knownHost);
                key = knownHost.KeyBase64;
                status = knownHost.Status;
            }
            var fingerprint = Connection.Fingerprint;
            var fingerprintHash = Connection.FingerprintHashFunc;
            return (
                Key: key,
                Status: status,
                Fingerprint: $"{fingerprintHash}:{Convert.ToBase64String(fingerprint).TrimEnd('=')}");
        }, token).ConfigureAwait(false);
        var hostStatus = host.Status switch {
            KnownHostStatus.Match => SftpKnownHostStatus.Match,
            KnownHostStatus.Mismatch => SftpKnownHostStatus.Mismatch,
            KnownHostStatus.NotFound => SftpKnownHostStatus.NotFound,
            _ => SftpKnownHostStatus.Error
        };
        lock (StateLocker) {
            HostKey = host.Key;
            HostStatus = hostStatus;
            HostKeyApproved = hostStatus == SftpKnownHostStatus.Match;
            HostFingerprint = host.Fingerprint;
        }
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP host key", Describe, hostStatus, host.Fingerprint, knownHostsFile));
        }
    }

    private void ResetConnectionState() {
        lock (StateLocker) {
            Connected = false;
            HostStatus = SftpKnownHostStatus.Uninitialized;
            HostKey = null;
            HostFingerprint = null;
            HostKeyApproved = false;
            Authenticated = false;
        }
    }

    private async Task CloseFailedConnection() {
        try {
            await Run(_ => {
                Connection.Close();
                return true;
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) {
            if (Log.Debug()) {
                Log.Debug(Log.Join("SFTP connection cleanup failed", ex));
            }
        }
    }

    private async Task<NativeSftp> Ready(CancellationToken token) {
        await Run(runToken => {
            Sftp.Ready(runToken);
            return true;
        }, token).ConfigureAwait(false);
        return Sftp;
    }

    internal async Task Connect(CancellationToken token) {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref Disposed) != 0, this);
        using var connectToken = CancellationTokenSource.CreateLinkedTokenSource(token, Shutdown.Token);
        try {
            await ConnectionGate.WaitAsync(connectToken.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested && Volatile.Read(ref Disposed) != 0) {
            throw new ObjectDisposedException(nameof(SftpClient));
        }
        try {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref Disposed) != 0, this);
            if (Volatile.Read(ref TransportAborted) != 0) {
                throw new IOException("The SFTP connection was aborted during cleanup.");
            }
            lock (StateLocker) {
                if (Connected) {
                    return;
                }
            }
            var connected = false;
            if (Log.Info()) {
                Log.Info(Log.Join("SFTP connecting", Describe));
            }
            try {
                var addresses = await Dns.GetHostAddressesAsync(HostName, connectToken.Token).ConfigureAwait(false);
                connected = await TryAddresses(addresses, token).ConfigureAwait(false);
                if (!connected) {
                    throw new IOException($"No addresses were found for SFTP host {HostName}.");
                }
                await ReadHostKey(token).ConfigureAwait(false);
                ObjectDisposedException.ThrowIf(Volatile.Read(ref Disposed) != 0, this);
                if (Volatile.Read(ref TransportAborted) != 0) {
                    throw new IOException("The SFTP connection was aborted during cleanup.");
                }
                lock (StateLocker) {
                    Connected = true;
                }
            }
            catch (Exception ex) {
                if (token.IsCancellationRequested is false) {
                    if (Log.Warn()) {
                        Log.Warn(Log.Join("SFTP connection failed", Describe, ex.GetType().Name, ex.Message));
                    }
                }
                ResetConnectionState();
                if (connected) {
                    await CloseFailedConnection().ConfigureAwait(false);
                }
                var disposedWithoutCallerCancellation =
                    ex is OperationCanceledException &&
                    !token.IsCancellationRequested &&
                    Volatile.Read(ref Disposed) != 0;
                if (disposedWithoutCallerCancellation) {
                    throw new ObjectDisposedException(nameof(SftpClient), ex);
                }
                throw;
            }
        }
        finally {
            ConnectionGate.Release();
        }
    }

    internal async Task<bool> TrustHostKey(CancellationToken token) {
        bool replace;
        lock (StateLocker) {
            if (HostStatus == SftpKnownHostStatus.Match) {
                HostKeyApproved = true;
                return true;
            }
            if (HostStatus is not SftpKnownHostStatus.NotFound and not SftpKnownHostStatus.Mismatch) {
                return false;
            }
            replace = HostStatus == SftpKnownHostStatus.Mismatch;
        }
        var file = KnownHostsFile;
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        if (!File.Exists(file)) {
            using var created = File.Create(file);
        }
        var key = await Run(_ => {
            KnownHosts.Trust(file, HostName, Port, replace);
            return Convert.ToBase64String(Connection.Fingerprint);
        }, token).ConfigureAwait(false);
        lock (StateLocker) {
            HostKey = key;
            HostStatus = SftpKnownHostStatus.Match;
            HostKeyApproved = true;
        }
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP host key trusted", Describe, "replaced", replace, file));
        }
        return true;
    }

    internal async Task<bool> AuthenticateByPassword(SecureString password, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(password);
        if (!HostApproved) {
            return false;
        }
        try {
            var authenticated = await Run(runToken => {
                Connection.Authenticate(password, runToken);
                return Connection.Authenticated();
            }, token).ConfigureAwait(false);
            lock (StateLocker) {
                Authenticated = authenticated;
            }
            if (Log.Info()) {
                Log.Info(Log.Join("SFTP password authentication", Describe, "authenticated", authenticated));
            }
            return authenticated;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            throw;
        }
        catch (Exception ex) {
            if (Log.Warn()) {
                Log.Warn(Log.Join("SFTP authentication failed", Describe, ex.GetType().Name, ex.Message));
            }
            return false;
        }
    }

    internal async Task<bool> AuthenticateByKey(string publicKeyFile,
                                               string privateKeyFile,
                                               SecureString passphrase,
                                               CancellationToken token) {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyFile);
        if (!HostApproved) {
            return false;
        }
        try {
            var authenticated = await Run(runToken => {
                Connection.Authenticate(publicKeyFile, privateKeyFile, passphrase, runToken);
                return Connection.Authenticated();
            }, token).ConfigureAwait(false);
            lock (StateLocker) {
                Authenticated = authenticated;
            }
            if (Log.Info()) {
                Log.Info(Log.Join("SFTP key authentication", Describe, privateKeyFile, "authenticated", authenticated));
            }
            return authenticated;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            throw;
        }
        catch (Exception ex) {
            if (Log.Warn()) {
                Log.Warn(Log.Join("SFTP authentication failed", Describe, ex.GetType().Name, ex.Message));
            }
            return false;
        }
    }

    internal async Task<SftpDirectory> OpenDirectory(string path, CancellationToken token) {
        if (Log.Debug()) {
            Log.Debug(Log.Join("SFTP OpenDirectory", Describe, path));
        }
        try {
            var sftp = await Ready(token).ConfigureAwait(false);
            return await Run(runToken => {
                var directory = sftp.OpenDirectory(path);
                try {
                    directory.Ready(runToken);
                    AddHandle(directory);
                    return new SftpDirectory(this, directory);
                }
                catch {
                    directory.Dispose();
                    throw;
                }
            }, token).ConfigureAwait(false);
        }
        catch (Exception error) when (Failed("OpenDirectory", path, error, token)) {
            throw;
        }
    }

    internal async Task<SftpMetadata> ReadDirectory(SftpDirectory directory, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(directory);
        return await Run(runToken => {
            using var item = new SftpItem();
            var end = directory.Agent.Next(item, runToken);
            if (end) {
                return null;
            }
            return new SftpMetadata(item.Name,
                                    item.Flags,
                                    item.Size,
                                    item.Uid,
                                    item.Gid,
                                    item.Permissions,
                                    item.Atime,
                                    item.Mtime);
        }, token, HandleToken(directory.Agent)).ConfigureAwait(false);
    }

    internal async Task<SftpMetadata> Stat(string path, bool link, CancellationToken token) {
        var sftp = await Ready(token).ConfigureAwait(false);
        return await Run(runToken => sftp.Stat(path, link, runToken), token).ConfigureAwait(false);
    }

    internal async Task<SftpStatResult> StatLinkTarget(string path, CancellationToken token) {
        var sftp = await Ready(token).ConfigureAwait(false);
        return await Run(runToken => {
            try {
                return new SftpStatResult(sftp.Stat(path, link: false, runToken), null, null);
            }
            catch (NativeErrorException ex) {
                var status = sftp.LastError;
                runToken.ThrowIfCancellationRequested();
                return new SftpStatResult(null, status, ex);
            }
        }, token).ConfigureAwait(false);
    }

    internal async Task<Stream> OpenRead(string path, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP OpenRead", Describe, path));
        }
        try {
            var sftp = await Ready(token).ConfigureAwait(false);
            var file = await Run(runToken => {
                var result = sftp.OpenFile(path, flags: OpenFlagRead, mode: 0);
                try {
                    result.Ready(runToken);
                    AddHandle(result);
                    return result;
                }
                catch {
                    result.Dispose();
                    throw;
                }
            }, token).ConfigureAwait(false);
            return new SftpFileStream(this, file, canRead: true, canWrite: false);
        }
        catch (Exception error) when (Failed("OpenRead", path, error, token)) {
            throw;
        }
    }

    internal async Task<Stream> OpenWrite(string path, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP OpenWrite", Describe, path));
        }
        try {
            var sftp = await Ready(token).ConfigureAwait(false);
            var file = await Run(runToken => {
                var result = sftp.OpenFile(
                    path,
                    flags: OpenFlagWrite | OpenFlagCreate | OpenFlagTruncate,
                    mode: DefaultFilePermissions);
                try {
                    result.Ready(runToken);
                    AddHandle(result);
                    return result;
                }
                catch {
                    result.Dispose();
                    throw;
                }
            }, token).ConfigureAwait(false);
            return new SftpFileStream(this, file, canRead: false, canWrite: true);
        }
        catch (Exception error) when (Failed("OpenWrite", path, error, token)) {
            throw;
        }
    }

    internal async Task CreateFile(string path, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP CreateFile", Describe, path));
        }
        try {
            var sftp = await Ready(token).ConfigureAwait(false);
            var file = await Run(runToken => {
                var result = sftp.OpenFile(
                    path,
                    flags: OpenFlagWrite | OpenFlagCreate | OpenFlagExclusive,
                    mode: DefaultFilePermissions);
                try {
                    result.Ready(runToken);
                    AddHandle(result);
                    return result;
                }
                catch {
                    result.Dispose();
                    throw;
                }
            }, token).ConfigureAwait(false);
            using var stream = new SftpFileStream(this, file, canRead: false, canWrite: true);
        }
        catch (Exception error) when (Failed("CreateFile", path, error, token)) {
            throw;
        }
    }

    internal async Task<int> Read(SftpFile file, byte[] buffer, int offset, int count, CancellationToken token) {
        try {
            ArgumentNullException.ThrowIfNull(file);
            return await Run(
                runToken => file.Read(buffer, offset, count, runToken),
                token,
                HandleToken(file)).ConfigureAwait(false);
        }
        catch (Exception error) when (Failed("Read", null, error, token)) {
            throw;
        }
    }

    internal async Task<int> Write(SftpFile file, byte[] buffer, int offset, int count, CancellationToken token) {
        try {
            ArgumentNullException.ThrowIfNull(file);
            return await Run(
                runToken => file.Write(buffer, offset, count, runToken),
                token,
                HandleToken(file)).ConfigureAwait(false);
        }
        catch (Exception error) when (Failed("Write", null, error, token)) {
            throw;
        }
    }

    internal async Task CreateDirectory(string path, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP CreateDirectory", Describe, path));
        }
        try {
            var sftp = await Ready(token).ConfigureAwait(false);
            await Run(runToken => {
                sftp.CreateDirectory(path, DefaultDirectoryPermissions, runToken);
                return true;
            }, token).ConfigureAwait(false);
        }
        catch (Exception error) when (Failed("CreateDirectory", path, error, token)) {
            throw;
        }
    }

    internal async Task CreateDirectoryIfMissing(string path, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP CreateDirectoryIfMissing", Describe, path));
        }
        try {
            var sftp = await Ready(token).ConfigureAwait(false);
            await Run(runToken => {
                try {
                    var info = sftp.Stat(path, link: false, runToken);
                    if (!SftpPosixMode.IsDirectory(info.Permissions)) {
                        throw new IOException($"The SFTP path already exists and is not a directory: {path}");
                    }
                    return true;
                }
                catch (Exception) {
                    var status = sftp.LastError;
                    if (status is not 2 and not 10) {
                        throw;
                    }
                    try {
                        sftp.CreateDirectory(path, DefaultDirectoryPermissions, runToken);
                    }
                    catch (NativeErrorException ex) {
                        throw new IOException($"The SFTP server refused to create directory {path} (status {sftp.LastError}).", ex);
                    }
                    return true;
                }
            }, token).ConfigureAwait(false);
        }
        catch (Exception error) when (Failed("CreateDirectoryIfMissing", path, error, token)) {
            throw;
        }
    }

    internal async Task DeleteFile(string path, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP DeleteFile", Describe, path));
        }
        try {
            var sftp = await Ready(token).ConfigureAwait(false);
            await Run(runToken => {
                sftp.DeleteFile(path, runToken);
                return true;
            }, token).ConfigureAwait(false);
        }
        catch (Exception error) when (Failed("DeleteFile", path, error, token)) {
            throw;
        }
    }

    internal async Task DeleteDirectory(string path, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP DeleteDirectory", Describe, path));
        }
        try {
            var sftp = await Ready(token).ConfigureAwait(false);
            await Run(runToken => {
                sftp.DeleteDirectory(path, runToken);
                return true;
            }, token).ConfigureAwait(false);
        }
        catch (Exception error) when (Failed("DeleteDirectory", path, error, token)) {
            throw;
        }
    }

    internal async Task Rename(string source, string destination, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP Rename", Describe, source + " -> " + destination));
        }
        try {
            var sftp = await Ready(token).ConfigureAwait(false);
            await Run(runToken => {
                sftp.Rename(source, destination, runToken);
                return true;
            }, token).ConfigureAwait(false);
        }
        catch (Exception error) when (Failed("Rename", source + " -> " + destination, error, token)) {
            throw;
        }
    }

    internal static string ResolveUsername(Uri uri) {
        ArgumentNullException.ThrowIfNull(uri);
        var rawUser = uri.UserInfo?.Split(':', 2)[0];
        var username = Uri.UnescapeDataString(string.IsNullOrWhiteSpace(rawUser) ? Environment.UserName : rawUser);
        if (string.IsNullOrWhiteSpace(username)) {
            throw new UriFormatException("An SFTP URI must specify a username.");
        }
        return username;
    }

    internal static int ResolvePort(Uri uri) {
        ArgumentNullException.ThrowIfNull(uri);
        var port = uri.Port;
        if (port == 0) {
            throw new UriFormatException("An SFTP URI port must be greater than zero.");
        }
        return port < 0 ? 22 : port;
    }

    internal async Task Close(IDisposable handle) {
        if (handle is null) {
            return;
        }
        if (Volatile.Read(ref Disposed) != 0) {
            return;
        }
        CancelHandle(handle);
        if (!await WaitForHandleGate().ConfigureAwait(false)) {
            await DisposeAsync().ConfigureAwait(false);
            return;
        }
        try {
            if (Volatile.Read(ref Disposed) != 0) {
                return;
            }
            if (RemoveHandle(handle)) {
                await Task.Run(handle.Dispose).ConfigureAwait(false);
            }
        }
        finally {
            try {
                if (Connection.IsAborted) {
                    MarkTransportAborted();
                }
            }
            finally {
                Gate.Release();
            }
        }
    }

    internal async Task Close(SftpFile file) {
        if (file is null) {
            return;
        }
        ObjectDisposedException.ThrowIf(Volatile.Read(ref Disposed) != 0, file);
        CancelHandle(file);
        if (!await WaitForHandleGate().ConfigureAwait(false)) {
            await DisposeAsync().ConfigureAwait(false);
            throw new ObjectDisposedException(
                file.GetType().FullName,
                "The SFTP client was disposed before the file CLOSE result could be verified.");
        }
        try {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref Disposed) != 0, file);
            if (!RemoveHandle(file)) {
                return;
            }
            await Task.Run(() => {
                try {
                    file.Close(CancellationToken.None);
                }
                finally {
                    file.Dispose();
                }
            }).ConfigureAwait(false);
        }
        finally {
            try {
                if (Connection.IsAborted) {
                    MarkTransportAborted();
                }
            }
            finally {
                Gate.Release();
            }
        }
    }

    internal async Task Close(SftpDir directory) {
        if (directory is null || Volatile.Read(ref Disposed) != 0) {
            return;
        }
        CancelHandle(directory);
        if (!await WaitForHandleGate().ConfigureAwait(false)) {
            await DisposeAsync().ConfigureAwait(false);
            return;
        }
        try {
            if (Volatile.Read(ref Disposed) != 0 || !RemoveHandle(directory)) {
                return;
            }
            await Task.Run(() => {
                try {
                    directory.Close(CancellationToken.None);
                }
                finally {
                    directory.Dispose();
                }
            }).ConfigureAwait(false);
        }
        finally {
            try {
                if (Connection.IsAborted) {
                    MarkTransportAborted();
                }
            }
            finally {
                Gate.Release();
            }
        }
    }

    internal string HostName => Uri.DnsSafeHost.Trim('[', ']');
    internal string Username { get; }
    internal int Port { get; }
    internal string Fingerprint {
        get {
            lock (StateLocker) {
                return HostFingerprint;
            }
        }
    }

    internal string KnownHostKey {
        get {
            lock (StateLocker) {
                return HostKey;
            }
        }
    }

    internal SftpKnownHostStatus KnownHostState {
        get {
            lock (StateLocker) {
                return HostStatus;
            }
        }
    }

    internal bool HostApproved {
        get {
            lock (StateLocker) {
                return HostKeyApproved;
            }
        }
    }

    internal bool IsAuthenticated {
        get {
            lock (StateLocker) {
                return Authenticated;
            }
        }
    }
    internal bool IsTerminal => Volatile.Read(ref Disposed) != 0 || Volatile.Read(ref TransportAborted) != 0;
    internal SftpClient(Uri uri, string knownHostsFile = null)
        : this(uri, knownHostsFile, ResolveUsername(uri)) {
    }

    internal SftpClient(Uri uri, string knownHostsFile, string username) {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        Port = ResolvePort(Uri);
        KnownHostsFile = string.IsNullOrWhiteSpace(knownHostsFile)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify),
                ".ssh",
                "known_hosts")
            : Path.GetFullPath(knownHostsFile);
        Username = username ?? throw new ArgumentNullException(nameof(username));
        if (string.IsNullOrWhiteSpace(Username)) {
            throw new UriFormatException("An SFTP URI must specify a username.");
        }
    }

    internal void AddProvider() {
        ProviderCount++;
    }

    internal int RemoveProvider() {
        if (ProviderCount > 0) {
            ProviderCount--;
        }
        return ProviderCount;
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.CompareExchange(ref DisposeStarted, 1, 0) != 0) {
            await DisposalCompleted.Task.ConfigureAwait(false);
            return;
        }
        Volatile.Write(ref Disposed, 1);
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP client disposing", Describe));
        }
        try {
            Shutdown.Cancel();
            var gateAcquired = await Gate.WaitAsync(DisposalGateWaitTimeout).ConfigureAwait(false);
            if (!gateAcquired) {
                if (Log.Warn()) {
                    Log.Warn(Log.Join("SFTP operations did not finish; aborting the connection", Describe));
                }
                Volatile.Write(ref TransportAborted, 1);
                Connection.RequestAbort();
                gateAcquired = await Gate.WaitAsync(DisposalAbortGateWaitTimeout).ConfigureAwait(false);
            }
            if (!gateAcquired) {
                /*
                 * Keep the client and its native children alive until the gate
                 * owner exits; this caller still receives a bounded failure.
                 */
                DeferredCleanup = Task.Run(DisposeResourcesAfterGate);
                throw new TimeoutException("SFTP operations did not release the client gate after transport abort.");
            }
            try {
                await Task.Run(DisposeResources).ConfigureAwait(false);
            }
            finally {
                Gate.Release();
            }
            DisposalCompleted.TrySetResult();
        }
        catch (Exception ex) {
            DisposalCompleted.TrySetException(ex);
            throw;
        }
    }

    private async Task DisposeResourcesAfterGate() {
        try {
            await Gate.WaitAsync().ConfigureAwait(false);
            try {
                await Task.Run(DisposeResources).ConfigureAwait(false);
            }
            finally {
                Gate.Release();
            }
        }
        catch (Exception ex) {
            if (Log.Debug()) {
                Log.Debug(Log.Join("Deferred SFTP client cleanup failed", ex));
            }
        }
    }

    private void DisposeResources() {
        foreach (var handle in Handles.ToArray()) {
            try {
                if (handle is SftpFile file) {
                    file.Close(CancellationToken.None);
                }
                else if (handle is SftpDir directory) {
                    directory.Close(CancellationToken.None);
                }
            }
            catch (Exception ex) {
                if (Log.Debug()) {
                    Log.Debug(Log.Join("SFTP handle close failed during client disposal", ex));
                }
            }
            try {
                handle.Dispose();
            }
            catch (Exception ex) {
                if (Log.Debug()) {
                    Log.Debug(Log.Join("SFTP handle dispose failed during client disposal", ex));
                }
            }
        }
        Handles.Clear();
        lock (HandleLocker) {
            HandleTokens.Clear();
        }
        Sftp?.Dispose();
        KnownHosts?.Dispose();
        Connection.Dispose();
    }

    Task ISftpFileOperations.DeleteFile(string path, CancellationToken token) {
        return DeleteFile(path, token);
    }

    Task ISftpFileOperations.DeleteDirectory(string path, CancellationToken token) {
        return DeleteDirectory(path, token);
    }

    Task ISftpFileOperations.Rename(string source, string destination, CancellationToken token) {
        return Rename(source, destination, token);
    }
}
