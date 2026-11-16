using Domore.Logs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/*
 * Limits the downloads and listings that one provider runs against its FTP server at the same time
 * and adapts the limit to that server. A server that limits concurrent sessions refuses a login (530 or 421) or drops the
 * connection before the login completes. Once any login with the same credentials has succeeded,
 * such a failure is treated as a refusal: the limit is lowered to the downloads still running and
 * the download is retried after one of them finishes. Nothing has been transferred before a login
 * completes, so the retry cannot duplicate or lose data.
 */
internal sealed class FtpTransferLimiter {
    private const int PeekLength = 81920;

    private static readonly ILog Log = Logging.For(typeof(FtpTransferLimiter));

    private readonly Lock Locker = new();

    private TaskCompletionSource Changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int Active;
    private int Refusing;
    private int CurrentLimit = int.MaxValue;
    private bool LoggedIn;

    private int Attempts { get; }
    private string Name { get; }
    private TimeSpan RetryDelay { get; }

    private static bool IsLoggedInReply(string line) {
        return line is not null && line.TrimStart().StartsWith("230", StringComparison.Ordinal);
    }

    private void NotifyChanged() {
        var
        changed = Changed;
        Changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        changed.TrySetResult();
    }

    private async Task Enter(string target, CancellationToken token) {
        var waiting = false;
        for (; ; ) {
            Task changed;
            lock (Locker) {
                if (Active < CurrentLimit) {
                    Active++;
                    return;
                }
                changed = Changed.Task;
                if (waiting is false) {
                    if (Log.Debug()) {
                        Log.Debug(Log.Join("FTP connection waiting for a slot", Name, target, Active, CurrentLimit));
                    }
                    waiting = true;
                }
            }
            await changed.WaitAsync(token);
        }
    }

    private void Exit() {
        lock (Locker) {
            Active--;
            NotifyChanged();
        }
    }

    private void Login() {
        lock (Locker) {
            if (LoggedIn) {
                return;
            }
            LoggedIn = true;
            NotifyChanged();
        }
        if (Log.Info()) {
            Log.Info(Log.Join("FTP login succeeded; later refused connections will be retried", Name));
        }
    }

    /*
     * Decides whether a download that failed before logging in was refused by a session limit. While no
     * login has succeeded yet, it waits for the other downloads that are still logging in; if none of
     * them succeeds, the failure is reported as it is, so wrong credentials are never retried.
     */
    private async Task<bool> Refused(CancellationToken token) {
        lock (Locker) {
            Refusing++;
            NotifyChanged();
        }
        try {
            for (; ; ) {
                Task changed;
                lock (Locker) {
                    var running = Active - Refusing;
                    if (LoggedIn) {
                        CurrentLimit = Math.Max(1, Math.Min(CurrentLimit, running));
                        return true;
                    }
                    if (running == 0) {
                        return false;
                    }
                    changed = Changed.Task;
                }
                await changed.WaitAsync(token);
            }
        }
        finally {
            lock (Locker) {
                Refusing--;
                NotifyChanged();
            }
        }
    }

    private void LogFailure(string target,
                            int attempt,
                            bool loggedIn,
                            bool retry,
                            int previousLimit,
                            Exception error,
                            CancellationToken token) {
        if (token.IsCancellationRequested) {
            if (Log.Debug()) {
                Log.Debug(Log.Join("FTP connection canceled", Name, target));
            }
            return;
        }
        if (retry) {
            if (Log.Info()) {
                Log.Info(Log.Join(
                    "FTP connection refused before login; retrying",
                    Name,
                    target,
                    "attempt", attempt,
                    "limit", previousLimit, Limit,
                    "delay", RetryDelay * attempt,
                    error.Message));
            }
            return;
        }
        var reason = loggedIn
            ? "failed after login; not retried"
            : attempt >= Attempts
                ? "failed before login; no attempts remain"
                : "failed before login and no login has succeeded; not retried";
        if (Log.Warn()) {
            Log.Warn(Log.Join("FTP connection " + reason, Name, target, "attempt", attempt, error.Message));
        }
    }

    private async Task<Stream> Connect(Func<Action<string>, CancellationToken, Task<Stream>> read,
                                       Action loggedIn,
                                       CancellationToken token) {
        var stream = await read(
            line => {
                if (IsLoggedInReply(line)) {
                    loggedIn();
                    Login();
                }
            },
            token);
        try {
            /*
             * The first read waits for the login and the start of the transfer, so a refused
             * connection fails here instead of in the middle of the caller's copy.
             */
            var buffer = new byte[PeekLength];
            var count = await stream.ReadAsync(buffer.AsMemory(), token);
            return new FtpDownloadStream(stream, buffer, count, Exit);
        }
        catch {
            await stream.DisposeAsync();
            throw;
        }
    }

    internal int Limit {
        get {
            lock (Locker) {
                return CurrentLimit;
            }
        }
    }

    internal FtpTransferLimiter(int attempts = 5, TimeSpan? retryDelay = null, string name = null) {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        Attempts = attempts;
        RetryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        Name = name ?? "";
    }

    internal Task<Stream> Open(Func<Action<string>, CancellationToken, Task<Stream>> read,
                               CancellationToken token) {
        return Open(null, read, token);
    }

    internal async Task<Stream> Open(string target,
                                     Func<Action<string>, CancellationToken, Task<Stream>> read,
                                     CancellationToken token) {
        ArgumentNullException.ThrowIfNull(read);
        for (var attempt = 1; ; attempt++) {
            await Enter(target, token);
            var loggedIn = false;
            try {
                var stream = await Connect(read, () => loggedIn = true, token);
                if (Log.Debug()) {
                    Log.Debug(Log.Join("FTP connection opened", Name, target, "attempt", attempt));
                }
                return stream;
            }
            catch (Exception error) {
                var retry = false;
                var previousLimit = Limit;
                try {
                    var mayRetry = token.IsCancellationRequested is false &&
                                   loggedIn is false &&
                                   attempt < Attempts;
                    if (mayRetry) {
                        retry = await Refused(token);
                    }
                }
                finally {
                    Exit();
                }
                LogFailure(target, attempt, loggedIn, retry, previousLimit, error, token);
                if (retry is false) {
                    throw;
                }
            }
            await Task.Delay(RetryDelay * attempt, token);
        }
    }
}
