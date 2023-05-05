using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Config;
    using Export;

    internal class Bookmark : Command<BookmarkParameter> {
        private IConfig<Bookmarks> Data =>
            _Data ?? (
            _Data = Configure.Data<Bookmarks>());
        private IConfig<Bookmarks> _Data;

        private async Task<bool> Add(string name, Context context, IOperationProgress progress, CancellationToken token) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) {
                return false;
            }
            if (active.HasProvider(out IProvider provider) == false) {
                return false;
            }
            var service = provider.Import<IBookmark>();
            if (service == null) {
                return false;
            }
            var data = await Data.Load(token);
            var existing = data.Bookmark.Select(b => KeyValuePair.Create(b.Key, b.Loc)).ToList();
            var added = new List<KeyValuePair<string, string>>();
            var work = await service.Work(existing, added, provider, progress, token);
            if (work == false) {
                return false;
            }
            foreach (var bookmark in added) {
                var loc = bookmark.Value;
                var key = name ?? bookmark.Key;
                data.Bookmark = data.Bookmark
                    .Where(b => b.Key != key)
                    .Append(new Bookmarked { Key = key, Loc = loc })
                    .ToList();
            }
            return true;
        }

        private async Task<bool> Session(string name, Context context, IOperationProgress progress, CancellationToken token) {
            if (context == null) return false;
            if (context.HasPanels(out var panels) == false) {
                return false;
            }
            var session = panels.AsEnumerable()
                 .Select(panel => panel.HasProvider(out IProvider provider) ? provider.ID : null)
                 .Where(id => id != null)
                 .ToList();
            var
            data = await Data.Load(token);
            data.Bookmark = data.Bookmark
                .Where(b => b.Key != name)
                .Append(new Bookmarked { Key = name, Ses = session })
                .ToList();
            return true;
        }

        private async Task<bool> View(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasCommander(out var commander)) {
                return await commander.ShowPalette($"{InputTrigger} ", cancellationToken);
            }
            return false;
        }

        protected override async IAsyncEnumerable<ICommandSuggestion> Suggest(Context context, [EnumeratorCancellation] CancellationToken token) {
            if (context != null) {
                if (context.DidTrigger(this)) {
                    if (context.HasData(out _) == false) {
                        var data = await Data.Load(token);
                        if (data.Bookmark.Count > 0) {
                            var bookmarkData = new BookmarkData(this, context, data);
                            context.SetData(bookmarkData);
                            context.SetHint(bookmarkData);
                        }
                    }
                }
            }
            await foreach (var suggestion in base.Suggest(context, token)) {
                yield return suggestion;
            }
        }

        protected sealed override bool Work(Context context) {
            if (context == null) return false;
            if (context.GetParameter(out var parameter) == false) return false;
            var session = parameter.Session?.Trim() ?? "";
            var add = parameter.Add;
            context.Operate(async (progress, token) => {
                if (session != "") {
                    await Session(session, context, progress, token);
                }
                if (add) {
                    await Add(null, context, progress, token);
                }
                if (session == "" && add == false) {
                    return await View(context, token);
                }
                return true;
            });
            return session != "" || add == true;
        }
    }
}
