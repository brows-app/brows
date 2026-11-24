using Brows.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class DrawImageWork {
    private Task<DrawImageTargets> Targets(CancellationToken token) {
        var requests = Outputs
            .Where(output => output.Key is FileInfo)
            .Select(output => {
                var file = (FileInfo)output.Key;
                return new KeyValuePair<FileInfo, string>(file, Path.Combine(file.DirectoryName, output.Value));
            });
        return DrawImageTargets.Plan(requests, Parameter.Overwrite == true, PathsAreEquivalent, token);
    }

    public ISTAThreadPool ThreadPool { get; }
    public DrawImage.Parameter Parameter { get; }
    public IReadOnlyList<KeyValuePair<FileSystemInfo, string>> Outputs { get; }
    public Func<string, string, CancellationToken, Task<bool>> PathsAreEquivalent { get; }

    public DrawImageWork(DrawImage.Parameter parameter, IReadOnlyList<KeyValuePair<FileSystemInfo, string>> outputs,
                         Func<string, string, CancellationToken, Task<bool>> pathsAreEquivalent,
                         ISTAThreadPool threadPool) {
        Outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        PathsAreEquivalent = pathsAreEquivalent;
    }

    public async Task<bool> Run(IOperationProgress progress, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(progress);
        Parameter.Validate();
        var targets = await Targets(token);
        var result = await progress.Children(Outputs, output => new(output.Key.Name, async (progress, childToken) => {
            async Task work(FileSystemInfo info, IOperationProgress progress, CancellationToken token) {
                ArgumentNullException.ThrowIfNull(progress);
                token.ThrowIfCancellationRequested();
                progress.Change(addTarget: 1);
                if (info is FileInfo file) {
                    await ThreadPool.Work(file.Name, cancellationToken: token, work: () => {
                        using (var draw = new DrawImageFile(file, Parameter, targets)) {
                            var codecs = $"{draw.DecoderCodec.FriendlyName} > {draw.EncoderCodec.FriendlyName}";
                            progress.Change(data: codecs);
                            draw.Save();
                        }
                        progress.Change(1);
                    });
                }
                else {
                    throw new DrawImageInfoIsNotFileException(info);
                }
            }
            await work(output.Key, progress, childToken);
        }
        ), OperationFailureBehavior.Record, token);
        return result.Results.Count > 0;
    }

    private sealed class DrawImageInfoIsNotFileException : Exception {
        public FileSystemInfo Info { get; }

        public DrawImageInfoIsNotFileException(FileSystemInfo info) {
            Info = info;
        }
    }
}
