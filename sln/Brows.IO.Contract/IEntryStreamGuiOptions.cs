using Domore.Text;
using System;
using System.Collections.Generic;

namespace Brows {
    public interface IEntryStreamGuiOptions {
        long? TextSourceLengthMax { get; }
        long? ImageSourceLengthMax { get; }
        DecodedTextOptions DecodedTextOptions { get; }
        IReadOnlyDictionary<string, Guid> PreviewCLSID { get; }
    }
}
