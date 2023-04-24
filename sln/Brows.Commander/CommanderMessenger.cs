using Domore.IPC;
using Domore.Logs;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class CommanderMessenger {
        private static readonly ILog Log = Logging.For(typeof(CommanderMessenger));

        private MessengerFactory Factory {
            get {
                if (_Factory == null) {
                    _Factory = new MessengerFactory { Directory = Path.Combine(Config.Configure.Root, nameof(Domore.IPC)) };
                    _Factory.ErrorHandler += Factory_ErrorHandler;
                    _Factory.MessageHandler += Factory_MessageHandler;
                }
                return _Factory;
            }
        }
        private MessengerFactory _Factory;

        private Messenger Messenger =>
            _Messenger ?? (
            _Messenger = Factory.Create());
        private Messenger _Messenger;

        private void Factory_MessageHandler(object sender, MessageEventArgs e) {
            if (e != null) {
                if (Log.Info()) {
                    Log.Info(e.Message);
                }
            }
        }

        private void Factory_ErrorHandler(object sender, MessengerErrorEventArgs e) {
            if (e != null) {
                if (Log.Warn()) {
                    Log.Warn(e.Exception);
                }
                e.Exit = false;
            }
        }

        public async Task Send(string message, CancellationToken token) {
            await Messenger.Send(message, token);
        }

        public IAsyncEnumerable<string> Receive(CancellationToken token) {
            return Messenger.Receive(token);
        }
    }
}
