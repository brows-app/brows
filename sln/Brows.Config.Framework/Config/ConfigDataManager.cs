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

        public class Of<TData> : ConfigDataManager, IConfig<TData> where TData : INotifyPropertyChanged, new() {
            private Task<TData> LoadTask;
            private Guid ChangeState = Guid.NewGuid();
            private readonly int ChangeDelay = 1000;

            private TaskHandler TaskHandler =>
                _TaskHandler ?? (
                _TaskHandler = new TaskHandler<Of<TData>>());
            private TaskHandler _TaskHandler;

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

            public TData Loaded { get; private set; }

            public Of(string id) : base(id) {
            }

            public async ValueTask<TData> Load(CancellationToken cancellationToken) {
                if (Loaded == null) {
                    if (LoadTask == null) {
                        LoadTask = Payload(new TData()).Load(cancellationToken);
                    }
                    Loaded = await LoadTask;
                    Loaded.PropertyChanged += Loaded_PropertyChanged;
                }
                return Loaded;
            }
        }
    }
}
