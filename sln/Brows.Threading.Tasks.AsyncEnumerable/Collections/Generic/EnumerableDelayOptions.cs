using Domore.Notification;

namespace Brows.Collections.Generic {
    public class EnumerableDelayOptions : Notifier {
        public int Limit {
            get => _Limit;
            set => Change(ref _Limit, value, nameof(Limit));
        }
        private int _Limit;

        public int Milliseconds {
            get => _Milliseconds;
            set => Change(ref _Milliseconds, value, nameof(Milliseconds));
        }
        private int _Milliseconds;

        public int Threshold {
            get => _Threshold;
            set => Change(ref _Threshold, value, nameof(Threshold));
        }
        private int _Threshold;
    }
}
