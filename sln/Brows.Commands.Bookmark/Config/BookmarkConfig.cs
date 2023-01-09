using Domore.Notification;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    using Gui;
    using System;

    internal sealed class BookmarkConfig : Notifier, IControlled<IBookmarkConfigController> {
        private IBookmarkConfigController Controller;

        private IConfig<Bookmarks> Config =>
            _Config ?? (
            _Config = Configure.Data<Bookmarks>());
        private IConfig<Bookmarks> _Config;

        public object Items {
            get => _Items ?? (_Items = Array.Empty<Bookmark>());
            private set => Change(ref _Items, value, nameof(Items));
        }
        private object _Items;

        public Bookmark CurrentItem =>
            Controller?.CurrentItem as Bookmark;

        public async Task<Bookmarks> Load(CancellationToken cancellationToken) {
            var config = await Config.Load(cancellationToken);
            Items = config.Bookmark;
            return config;
        }

        public async Task Add(string key, string value, CancellationToken cancellationToken) {
            var k = key?.Trim() ?? "";
            if (k != "") {
                var v = value?.Trim() ?? "";
                if (v != "") {
                    var
                    config = await Config.Load(cancellationToken);
                    config.Bookmark = config.Bookmark
                        .Where(b => b.Key != k)
                        .Append(new Bookmark { Key = k, Loc = v })
                        .ToList();
                    Items = config.Bookmark;
                }
            }
        }

        public async Task Remove(string key, CancellationToken cancellationToken) {
            var k = key?.Trim() ?? "";
            if (k != "") {
                var
                config = await Config.Load(cancellationToken);
                config.Bookmark = config.Bookmark
                    .Where(b => b.Key != k)
                    .ToList();
                Items = config.Bookmark;
            }
        }

        public async Task Clear(CancellationToken cancellationToken) {
            var
            config = await Config.Load(cancellationToken);
            config.Bookmark = new List<Bookmark>();
            Items = config.Bookmark;
        }

        IBookmarkConfigController IControlled<IBookmarkConfigController>.Controller {
            set => Controller = value;
        }
    }
}
