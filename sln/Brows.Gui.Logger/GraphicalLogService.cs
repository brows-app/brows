using System.Threading.Channels;

namespace Brows {
    using Logger.Services;

    internal class GraphicalLogService : LogServiceBase {
        private readonly Channel<string> Ch = Channel.CreateUnbounded<string>(new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = true
        });

        public ChannelReader<string> Reader => Ch.Reader;

        public override async void Log(string s) {
            await Ch.Writer.WriteAsync(s);
        }
    }
}
