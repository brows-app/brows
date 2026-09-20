using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TASK = System.Threading.Tasks.Task;

namespace Brows.CLI;

internal sealed class CliProgramWrapper {
    private readonly DataBuilder LogBuilder = new();
    private readonly DataBuilder OutputBuilder = new();

    private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e) {
        LogBuilder.Add(
            data: e?.Data,
            maxLength: Info?.MaxLogLength ?? CliProgramInfo.DefaultMaxLogLength);
        Info?.OnLog?.Invoke(e?.Data);
    }

    private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e) {
        OutputBuilder.Add(
            data: e?.Data,
            maxLength: Info?.MaxOutputLength ?? CliProgramInfo.DefaultMaxOutputLength);
        Info?.OnOutput?.Invoke(e?.Data);
    }

    private CliProgramData Data(bool canceled) {
        return new(
            log: LogBuilder.ToString().Trim(),
            output: OutputBuilder.ToString().Trim(),
            canceled: canceled);
    }

    private CliProgramData Process() {
        var waitTime = Info?.WaitTime ?? CliProgramInfo.DefaultWaitTime;
        var inWindow = Info?.InWindow ?? false;
        using var process = new Process {
            EnableRaisingEvents = true,
            StartInfo = new() {
                Arguments = CommandLine,
                CreateNoWindow = !inWindow,
                ErrorDialog = false,
                FileName = FileName,
                RedirectStandardError = !inWindow,
                RedirectStandardOutput = !inWindow,
                UseShellExecute = inWindow,
                WindowStyle = inWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden
            }
        };
        try {
            process.ErrorDataReceived += Process_ErrorDataReceived;
            process.OutputDataReceived += Process_OutputDataReceived;
            process.Start();
            if (!inWindow) {
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
            }
            for (; ; ) {
                var cancel = false;
                var exited = process.WaitForExit(waitTime);
                if (exited == false) {
                    var canceled = cancel = Token.IsCancellationRequested;
                    if (canceled) {
                        try {
                            process.Kill();
                        }
                        catch {
                            if (process.HasExited == false) {
                                throw;
                            }
                        }
                        exited = true;
                    }
                }
                if (exited) {
                    try {
                        /*
                         * Call WaitForExit without a timeout to ensure async callbacks
                         * for reading error and output lines have been allowed to complete.
                         */
                        process.WaitForExit();
                    }
                    catch {
                        /*
                         * TODO: Check if an exception is caught here, and that the above makes sense.
                         */
                    }
                    return Data(cancel);
                }
            }
        }
        finally {
            process.ErrorDataReceived -= Process_ErrorDataReceived;
            process.OutputDataReceived -= Process_OutputDataReceived;
        }
    }

    private async Task<CliProgramData> Start(CancellationToken token) {
        var data = await TASK.Run(Process, token).ConfigureAwait(false);
        if (data.Canceled) {
            var @throw = Info?.ThrowIfCancellationRequested ?? false;
            if (@throw) {
                token.ThrowIfCancellationRequested();
            }
        }
        return data;
    }

    public string FileName { get; }
    public string CommandLine { get; }
    public CancellationToken Token { get; }
    public Task<CliProgramData> Task { get; }
    public CliProgramInfo Info { get; }

    public CliProgramWrapper(string fileName, string commandLine, CliProgramInfo info, CancellationToken token) {
        Info = info;
        Token = token;
        FileName = fileName;
        CommandLine = commandLine;
        Task = Start(Token);
    }

    public sealed override string ToString() {
        return $"\"{FileName}\" {CommandLine}";
    }

    private sealed class DataBuilder {
        private readonly StringBuilder SB = new();

        public void Add(string data, int maxLength) {
            lock (SB) {
                if (maxLength <= 0) {
                    if (SB.Length > 0) {
                        SB.Clear();
                    }
                    return;
                }
                var sb = SB.AppendLine(data);
                if (sb.Length > maxLength) {
                    sb.Remove(0, sb.Length - maxLength);
                }
            }
        }

        public sealed override string ToString() {
            lock (SB) {
                return SB.ToString();
            }
        }
    }
}
