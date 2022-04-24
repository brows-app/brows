using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    using Logger;
    using System;
    using Threading.Tasks;

    public class DataManager<TData> where TData : IDataModel, new() {
        private static TData Data;
        private static Guid PropertyChangedState = Guid.NewGuid();
        private static readonly int PropertyChangedDelay = 1000;
        private static readonly object DataLocker = new object();

        private static TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<DataManager<TData>>());
        private static TaskHandler _TaskHandler;

        private static ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(DataManager<TData>)));
        private static ILog _Log;

        private static void Data_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            var state = PropertyChangedState = Guid.NewGuid();
            TaskHandler.Begin(async token => {
                await Task.Delay(PropertyChangedDelay, token);

                if (PropertyChangedState == state) {
                    await Async.Run(token, () => {
                        if (PropertyChangedState == state) {
                            lock (DataLocker) {
                                if (PropertyChangedState == state) {
                                    var data = Data;
                                    var store = data.Store;
                                    if (Log.Info()) {
                                        Log.Info(nameof(store.Save));
                                    }
                                    store.Save(data);
                                }
                            }
                        }
                    });
                }
            });
        }

        public async Task<TData> Load(CancellationToken cancellationToken) {
            if (Data == null) {
                await Async.Run(cancellationToken, () => {
                    if (Data == null) {
                        lock (DataLocker) {
                            if (Data == null) {
                                if (Log.Info()) {
                                    Log.Info(nameof(Load));
                                }
                                try {
                                    Data = (TData)(new TData().Store.Load());
                                }
                                catch (Exception ex) {
                                    if (Log.Error()) {
                                        Log.Error(ex);
                                    }
                                }
                                Data = Data ?? new TData();
                                Data.PropertyChanged += Data_PropertyChanged;
                            }
                        }
                    }
                });
            }
            return Data;
        }
    }
}
