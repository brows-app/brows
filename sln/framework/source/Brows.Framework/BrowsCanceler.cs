using System.Runtime.InteropServices;

namespace Brows;

/// <summary>
/// Passes cancellation requests to Brows native operations.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct BrowsCanceler {
    [UnmanagedFunctionPointer(BrowsNative.Call, CharSet = BrowsNative.Chars)]
    private delegate int CanceledDelegate(ref BrowsCanceler p);

    private readonly CanceledDelegate Canceled;

    /// <summary>
    /// A value that never requests cancellation.
    /// </summary>
    public static readonly BrowsCanceler None = new();

    private BrowsCanceler(CanceledDelegate canceled) {
        Canceled = canceled;
    }

    /// <summary>
    /// Creates a native cancellation value that follows a cancellation token.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A value that reports cancellation when the token is canceled.
    /// </returns>
    public static BrowsCanceler From(CancellationToken token) {
        int canceled(ref BrowsCanceler p) {
            return token.IsCancellationRequested
                ? 1
                : 0;
        }
        return new BrowsCanceler(canceled);
    }
}
