using System.IO;

namespace Brows.Commands;

internal sealed class FindContentLimitException : IOException {
    internal FindContentLimitException() : base("The file exceeds the content search byte limit.") {
    }
}
