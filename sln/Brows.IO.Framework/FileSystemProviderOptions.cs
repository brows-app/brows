namespace Brows {
    using Collections.Generic;
    using ComponentModel;
    using IO;

    public class FileSystemProviderOptions : NotifyPropertyChanged {
        public DirectoryEnumerableOptions Enumerable {
            get => _Enumerable ?? (_Enumerable = new DirectoryEnumerableOptions {
                Delay = new EnumerableDelayOptions {
                    Limit = 10,
                    Milliseconds = 50,
                    Threshold = 100
                },
                Mode = EnumerableMode.Channel
            });
            set => Change(ref _Enumerable, value, nameof(Enumerable));
        }
        private DirectoryEnumerableOptions _Enumerable;
    }
}
