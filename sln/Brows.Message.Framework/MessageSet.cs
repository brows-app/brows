using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    public sealed class MessageSet : IDisposable {
        private static readonly ILog Log = Logging.For(typeof(MessageSet));

        private readonly IReadOnlySet<IMessenger> Agent;

        private void Item_Message(object source, MessageEventArgs e) {
            Message?.Invoke(this, e);
        }

        private void Dispose(bool disposing) {
            if (disposing) {
                foreach (var item in Agent) {
                    try {
                        item.Message -= Item_Message;
                        item.Dispose();
                    }
                    catch (Exception ex) {
                        if (Log.Error()) {
                            Log.Error(ex);
                        }
                    }
                }
                Message = null;
            }
        }

        public event MessageEventHandler Message;

        public MessageSet(IEnumerable<IMessenger> messengers) {
            if (null == messengers) throw new ArgumentNullException(nameof(messengers));
            Agent = messengers.Where(item => item is not null).ToHashSet();
            Agent.ToList().ForEach(item => {
                item.Message += Item_Message;
            });
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MessageSet() {
            Dispose(false);
        }
    }
}
