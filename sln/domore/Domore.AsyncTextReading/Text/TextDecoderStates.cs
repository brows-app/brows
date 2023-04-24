using System;

namespace Domore.Text {
    [Flags]
    internal enum TextDecoderStates {
        Running = 0,
        Complete = 1,
        Success = 2,
        Canceled = 4,
        Error = 8
    }
}
