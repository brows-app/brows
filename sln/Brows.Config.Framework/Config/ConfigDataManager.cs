using Domore.Threading.Tasks;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config {
    internal class ConfigDataManager : IConfig {
        public string ID { get; }

        public ConfigDataManager(string id) {
            ID = id;
        }

        public class Of<TData> : ConfigDataManager, IConfig<TData> where TData : class, INotifyPropertyChanged, new() {
            private int ChangeState;
            private readonly int ChangeDelay = 1000;
            private readonly SemaphoreSlim ChangeLocker = new(1, 1);

            private TaskCache<TData> Cache =>
                _Cache ?? (
                _Cache = new TaskCache<TData>(async token => {
                    var
                    loaded = await Payload(new TData()).Load(token);
                    loaded.PropertyChanged += Loaded_PropertyChanged;
                    return loaded;
                }));
            private TaskCache<TData> _Cache;

            private ConfigDataPayload<TData> Payload(TData data) {
                return ConfigDataPayload.For(data, ID);
            }

            private async void Loaded_PropertyChanged(object sender, EventArgs e) {
                var changeState = ChangeState = ChangeState + 1; await Task.Delay(ChangeDelay);
                if (changeState == ChangeState) {
                    await ChangeLocker.WaitAsync();
                    try {
                        if (changeState == ChangeState) {
                            ChangeState = 0;
                            var payload = Payload(Loaded);
                            await payload.Save(CancellationToken.None);
                        }
                    }
                    finally {
                        ChangeLocker.Release();
                    }
                }
            }

            public TData Loaded =>
                Cache.Result;

            public Of(string id) : base(id) {
            }

            public ValueTask<TData> Load(CancellationToken token) {
                return Cache.Ready(token);
            }
        }
    }
}
