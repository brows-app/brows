using System;
using System.IO;

namespace Domore.IO {
    public interface IStreamText {
        bool StreamValid { get; }
        long StreamLength { get; }
        IDisposable StreamReady();
        Stream StreamText();
    }
}
