using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Url;

public abstract class UrlClientCache<TClient> where TClient : UrlClient, new() {
    private static readonly UriEqualityComparer CacheUriComparer = new();

    private readonly ConcurrentDictionary<Uri, TClient> Agent = new(CacheUriComparer);

    private static bool SameUserInfo(Uri first, Uri second) {
        return StringComparer.Ordinal.Equals(first.UserInfo, second.UserInfo);
    }

    private static bool SameAuthority(Uri first, Uri second) {
        return StringComparer.OrdinalIgnoreCase.Equals(first.Scheme, second.Scheme) &&
            StringComparer.OrdinalIgnoreCase.Equals(first.IdnHost, second.IdnHost) &&
            first.Port == second.Port &&
            SameUserInfo(first, second);
    }

    private static bool Extends(Uri ancestor, Uri descendant, out int? order) {
        ArgumentNullException.ThrowIfNull(ancestor);
        ArgumentNullException.ThrowIfNull(descendant);
        if (!SameUserInfo(ancestor, descendant)) {
            order = null;
            return false;
        }
        if (CacheUriComparer.Equals(ancestor, descendant)) {
            order = 0;
            return true;
        }
        order = null;
        var aPath = ancestor.GetLeftPart(UriPartial.Path);
        var dPath = descendant.GetLeftPart(UriPartial.Path);
        var aParts = aPath.Split('/');
        var dParts = dPath.Split('/');
        for (var i = 0; i < dParts.Length; i++) {
            if (aParts.Length > i) {
                if (aParts[i] == dParts[i]) {
                    if (order == null) {
                        order = dParts.Length;
                    }
                    else {
                        order--;
                    }
                }
                else {
                    order = null;
                    break;
                }
            }
            else {
                break;
            }
        }
        return order != null;
    }

    private static TClient Match(Uri uri, IEnumerable<TClient> clients) {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(clients);
        var match = clients
            .Select(client => Extends(client.Uri, uri, out var order) ? new { client, order } : null)
            .Where(item => item is not null)
            .OrderBy(item => item.order)
            .Select(item => item.client)
            .FirstOrDefault();
        if (match == null) {
            match = clients.FirstOrDefault(client => SameAuthority(client.Uri, uri));
        }
        return match;
    }

    private TClient Extend(TClient client, Uri uri) {
        if (!SameAuthority(client.Uri, uri)) {
            return new() { Uri = uri };
        }
        var
        other = (TClient)client.Clone();
        other.Uri = uri;
        return other;
    }

    private sealed class UriEqualityComparer : IEqualityComparer<Uri> {
        public bool Equals(Uri first, Uri second) {
            if (ReferenceEquals(first, second)) {
                return true;
            }
            var eitherUriIsNull = first is null || second is null;
            if (eitherUriIsNull) {
                return false;
            }
            return first.Equals(second) && SameUserInfo(first, second);
        }

        public int GetHashCode(Uri uri) {
            ArgumentNullException.ThrowIfNull(uri);
            var hash = new HashCode();
            hash.Add(uri.GetHashCode());
            hash.Add(uri.UserInfo, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }

    public TClient Get(Uri uri, IEnumerable<TClient> existing = null) {
        ArgumentNullException.ThrowIfNull(uri);
        var exists = Agent.Values;
        var others = exists
            .Concat(existing ?? [])
            .Where(client => client != null);
        return Agent.GetOrAdd(uri, uri => {
            var client = Match(uri, others);
            if (client != null) {
                return Extend(client, uri);
            }
            return new() { Uri = uri };
        });
    }
}
