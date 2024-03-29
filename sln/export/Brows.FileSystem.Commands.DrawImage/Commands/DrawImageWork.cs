﻿using Domore.IO;
using Domore.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class DrawImageWork {
        private IReadOnlyDictionary<FileSystemInfo, string> Name =>
            _Name ?? (
            _Name = Rename.Selection(Parameter.Output));
        private IReadOnlyDictionary<FileSystemInfo, string> _Name;

        private IReadOnlyList<FileSystemInfo> Info =>
            _Info ?? (
            _Info = new List<FileSystemInfo>(Name.Keys));
        private IReadOnlyList<FileSystemInfo> _Info;

        public STAThreadPool ThreadPool { get; }
        public FileSystemRename Rename { get; }
        public DrawImageParameter Parameter { get; }

        public DrawImageWork(DrawImageParameter parameter, FileSystemRename rename, STAThreadPool threadPool) {
            Rename = rename ?? throw new ArgumentNullException(nameof(rename));
            Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            ThreadPool = threadPool ?? throw new ArgumentNullException(nameof(threadPool));
        }

        public Task<bool> Run(IOperationProgress progress, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(progress);
            return progress.Children(Info, info => (info.Name, OperationProgressKind.None, async (progress, t) => {
                async Task work(FileSystemInfo info, IOperationProgress progress, CancellationToken token) {
                    ArgumentNullException.ThrowIfNull(progress);
                    token.ThrowIfCancellationRequested();
                    progress.Change(addTarget: 2);
                    if (info is FileInfo file) {
                        var name = Path.Combine(file.DirectoryName, Name[file]);
                        var outp = Parameter.Overwrite == true
                            ? name
                            : await FileSystemTask.Nonexistent(name, FileSystemCollisionPrevention.Default, token);
                        await ThreadPool.Work(file.Name, cancellationToken: token, work: () => {
                            var
                            draw = new DrawImageFile(file, Parameter, outp);
                            progress.Change(data: $"{draw.DecoderCodec.FriendlyName} > {draw.EncoderCodec.FriendlyName}");
                            draw.Save();
                            progress.Change(1);
                            draw.Transfer();
                            progress.Change(1);
                        });
                    }
                    else {
                        throw new DrawImageInfoIsNotFileException(info);
                    }
                }
                using (var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, t)) {
                    await work(info, progress, tokenSource.Token);
                }
            }
            ));
        }

        private sealed class DrawImageInfoIsNotFileException : Exception {
            public FileSystemInfo Info { get; }

            public DrawImageInfoIsNotFileException(FileSystemInfo info) {
                Info = info;
            }
        }
    }
}
