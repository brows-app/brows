using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SftpClientCache {
    private static readonly IEqualityComparer<CacheKey> KeyComparer = new CacheKeyEqualityComparer();
    private static readonly StringComparer KnownHostsFileComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly Dictionary<CacheKey, SftpClient> Clients = new(KeyComparer);

    internal SftpClientLease Acquire(Uri uri, string knownHostsFile = null) {
        ArgumentNullException.ThrowIfNull(uri);
        var normalizedKnownHostsFile = Path.GetFullPath(
            string.IsNullOrWhiteSpace(knownHostsFile) ? DefaultKnownHostsFile() : knownHostsFile);
        var username = SftpClient.ResolveUsername(uri);
        var key = Key(uri, normalizedKnownHostsFile, username);
        SftpClient terminal = null;
        SftpClientLease lease;
        lock (Clients) {
            if (Clients.TryGetValue(key, out var cached) && cached.IsTerminal) {
                Clients.Remove(key);
                terminal = cached;
            }
            if (!Clients.TryGetValue(key, out var client)) {
                client = new SftpClient(uri, normalizedKnownHostsFile, username);
                Clients.Add(key, client);
            }
            client.AddProvider();
            lease = new SftpClientLease(this, client);
        }
        if (terminal is not null) {
            _ = DisposeClient(terminal);
        }
        return lease;
    }

    internal void Release(SftpClient client) {
        ArgumentNullException.ThrowIfNull(client);
        var remove = false;
        lock (Clients) {
            var remaining = client.RemoveProvider();
            if (remaining == 0) {
                var key = Key(client.Uri, client.KnownHostsFile, client.Username);
                if (Clients.TryGetValue(key, out var cached) && ReferenceEquals(client, cached)) {
                    Clients.Remove(key);
                }
                remove = true;
            }
        }
        if (remove) {
            _ = DisposeClient(client);
        }
    }

    private static async Task DisposeClient(SftpClient client) {
        try {
            await client.DisposeAsync();
        }
        catch {
        }
    }

    private static string DefaultKnownHostsFile() {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify),
            ".ssh",
            "known_hosts");
    }

    private static CacheKey Key(Uri uri, string knownHostsFile, string username) {
        var port = SftpClient.ResolvePort(uri);
        return new CacheKey(uri.Scheme, uri.IdnHost, port, username, knownHostsFile);
    }

    private readonly record struct CacheKey(
        string Scheme,
        string Host,
        int Port,
        string Username,
        string KnownHostsFile);

    private sealed class CacheKeyEqualityComparer : IEqualityComparer<CacheKey> {
        public bool Equals(CacheKey x, CacheKey y) {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Scheme, y.Scheme) &&
                StringComparer.OrdinalIgnoreCase.Equals(x.Host, y.Host) &&
                x.Port == y.Port &&
                StringComparer.Ordinal.Equals(x.Username, y.Username) &&
                KnownHostsFileComparer.Equals(x.KnownHostsFile, y.KnownHostsFile);
        }

        public int GetHashCode(CacheKey key) {
            var hash = new HashCode();
            hash.Add(key.Scheme, StringComparer.OrdinalIgnoreCase);
            hash.Add(key.Host, StringComparer.OrdinalIgnoreCase);
            hash.Add(key.Port);
            hash.Add(key.Username, StringComparer.Ordinal);
            hash.Add(key.KnownHostsFile, KnownHostsFileComparer);
            return hash.ToHashCode();
        }
    }
}
