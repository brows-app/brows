using Domore.Text;
using System;
using System.Collections.Generic;

namespace Brows {
    public interface IEntryStreamGuiOptions {
        long? TextSourceLengthMax { get; }
        long? ImageSourceLengthMax { get; }
        DecodedTextOptions TextDecoderOptions { get; }
        IReadOnlyDictionary<string, Guid> PreviewCLSID { get; }
        bool LoopVideo { get; }
        bool LoopAudio { get; }
        bool AutoplayVideo { get; }
        bool AutoplayAudio { get; }
        bool MediaMuted { get; }
        double MediaSpeedRatio { get; }
        double MediaVolume { get; }
    }
}
