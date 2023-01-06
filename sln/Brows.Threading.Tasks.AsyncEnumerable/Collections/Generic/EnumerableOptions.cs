using Domore.Notification;

namespace Brows.Collections.Generic {
    public class EnumerableOptions : Notifier {
        public EnumerableDelayOptions Delay {
            get => _Delay;
            set => Change(ref _Delay, value, nameof(Delay));
        }
        private EnumerableDelayOptions _Delay;

        public EnumerableMode Mode {
            get => _Mode;
            set => Change(ref _Mode, value, nameof(Mode));
        }
        private EnumerableMode _Mode = EnumerableMode.Default;

        public bool ChannelAllowSynchronousContinuations {
            get => _ChannelAllowSynchronousContinuations;
            set => Change(ref _ChannelAllowSynchronousContinuations, value, nameof(ChannelAllowSynchronousContinuations));
        }
        private bool _ChannelAllowSynchronousContinuations;
    }
}
