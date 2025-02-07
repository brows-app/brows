using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Url {
    public abstract class UrlClientCache<TClient> where TClient : UrlClient, new() {
        private readonly ConcurrentDictionary<Uri, TClient> Agent = [];

        private static bool Extends(Uri ancestor, Uri descendant, out int? order) {
            ArgumentNullException.ThrowIfNull(ancestor);
            ArgumentNullException.ThrowIfNull(descendant);
            if (ancestor == descendant || ancestor.Equals(descendant)) {
                order = 0;
                return true;
            }
            else {
                order = null;
            }
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
                match = clients.FirstOrDefault(client => client.Uri?.GetLeftPart(UriPartial.Authority) == uri.GetLeftPart(UriPartial.Authority));
            }
            return match;
        }

        private TClient Extend(TClient client, Uri uri) {
            var
            other = (TClient)client.Clone();
            other.Uri = uri;
            return other;
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
}
