using System;

namespace Brows.Messaging;

public interface IMessenger : IDisposable {
    event MessageEventHandler Message;
}
