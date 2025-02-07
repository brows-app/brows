using Brows.Url.Ftp.ListingItemParsers;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Url.Ftp {
    internal sealed class FtpListingParser : UrlListingParser<FtpListingInfo> {
        private static readonly ILog Log = Logging.For(typeof(FtpListingParser));

        private readonly ImmutableArray<FtpListingItemParser> Agents = [
            new MlsdListingParser(),
            new UnixListingParser(expectDate: true),
            new DosListingParser(),
            new EplfListingParser(),
            new VmsListingParser(),
            new OtherListingParser(),
            new IbmListingParser(),
            new WfFtpListingParser(),
            new MvsListingParser(),
            new MvsPdsListingParser(),
            new OS9ListingParser(),
            new MvsMigratedListingParser(),
            new MvsPds2ListingParser(),
            new MvsTapeListingParser(),
            new HPNonStopListingParser(),
            new ZvmListingParser(),
            new UnixListingParser(expectDate: false),
        ];

        internal FtpListingInfo Parse(IReadOnlyList<string> lines, FtpListingContext context) {
            ArgumentNullException.ThrowIfNull(lines);
            var items = lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => new FtpListingItem(line)).ToList();
            if (items.Count == 0) {
                return null;
            }
            foreach (var agent in Agents) {
                var info = default(FtpListingInfo);
                try {
                    info = agent.Parse(items, context);
                }
                catch (Exception ex) {
                    if (Log.Info()) {
                        Log.Info(ex);
                    }
                }
                if (info != null) {
                    return info;
                }
            }
            return null;
        }

        protected sealed override async IAsyncEnumerable<FtpListingInfo> ParseLines(IAsyncEnumerable<string> lines, [EnumeratorCancellation] CancellationToken token) {
            ArgumentNullException.ThrowIfNull(lines);
            var prev = default(string);
            var context = new FtpListingContext();
            await foreach (var line in lines.WithCancellation(token).ConfigureAwait(false)) {
                if (prev == null) {
                    prev = line;
                    continue;
                }
                var next = line;
                var info = Parse([prev, next], context);
                if (info != null) {
                    prev = null;
                }
                else {
                    info = Parse([prev], context);
                    prev = line;
                }
                if (info != null) {
                    yield return info;
                }
            }
            if (prev != null) {
                var info = Parse([prev], context);
                if (info != null) {
                    yield return info;
                }
            }
        }
    }
}
