using System.Threading;

namespace Brows.Updates;

internal static class UpdateHandoff {
    internal const string MutexName = @"Local\Brows.Update";

    internal static bool Running() {
        if (!Mutex.TryOpenExisting(MutexName, out var mutex)) {
            return false;
        }
        mutex.Dispose();
        return true;
    }
}
