using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    using Threading.Tasks;

    internal class ConfigDataManager : IConfig {
        public string ID { get; }

        public ConfigDataManager(string id) {
            ID = id;
        }

        public class Of<TData> : ConfigDataManager, IConfig<TData> where TData : class, INotifyPropertyChanged, new() {
            private Guid ChangeState = Guid.NewGuid();
            private readonly int ChangeDelay = 1000;

            private TaskHandler TaskHandler =>
                _TaskHandler ?? (
                _TaskHandler = new TaskHandler<Of<TData>>());
            private TaskHandler _TaskHandler;

            private TaskCache<TData> Cache =>
                _Cache ?? (
                _Cache = new TaskCache<TData>(async cancellationToken => {
                    var
                    loaded = await Payload(new TData()).Load(cancellationToken);
                    loaded.PropertyChanged += Loaded_PropertyChanged;
                    return loaded;
                }));
            private TaskCache<TData> _Cache;

            private ConfigDataPayload<TData> Payload(TData data) {
                return ConfigDataPayload.For(data, ID);
            }

            private void Loaded_PropertyChanged(object sender, EventArgs e) {
                var state = ChangeState = Guid.NewGuid();
                TaskHandler.Begin(async cancellationToken => {
                    await Task.Delay(ChangeDelay, cancellationToken);
                    if (ChangeState == state) {
                        await Payload(Loaded).Save(cancellationToken);
                    }
                });
            }

            public TData Loaded =>
                Cache.Result;

            public Of(string id) : base(id) {
            }

            public ValueTask<TData> Load(CancellationToken cancellationToken) {
                return Cache.Ready(cancellationToken);
            }
        }
    }
}
