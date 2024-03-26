using System;

namespace Brows {
    public interface IMessenger : IDisposable {
        event MessageEventHandler Message;
    }
}
