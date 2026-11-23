namespace Brows.MBox.Format;

internal readonly record struct MBoxHeaderReadResult(
    MBoxHeaders Headers,
    int BytesConsumed,
    bool IsComplete,
    bool LimitReached);
