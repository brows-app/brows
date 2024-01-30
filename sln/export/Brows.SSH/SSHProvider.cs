﻿using Brows.Exports;
using Brows.SCP;
using Brows.SSH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using PATH = System.IO.Path;

namespace Brows {
    internal sealed class SSHProvider : SSHProviderBase<SSHEntry, SSHConfig> {
        private async IAsyncEnumerable<SSHEntry> List([EnumeratorCancellation] CancellationToken token) {
            var client = await Client.Ready(token);
            var list = client.List(Uri, token);
            await foreach (var item in list) {
                yield return new SSHEntry(this, item);
            }
        }

        protected sealed override async Task Begin(CancellationToken token) {
            await foreach (var item in List(token)) {
                await Provide(item);
            }
        }

        protected sealed override async Task Refresh(CancellationToken token) {
            await Revoke(Provided);
            await foreach (var item in List(token)) {
                await Provide(item);
            }
        }

        public SSHProvider(SSHProviderFactory factory, Uri uri, object icon) : base(factory, uri, icon) {
        }

        public SSHClientBase ClientReady() {
            return Client.Result;
        }

        private sealed class ProvideIO : IProvideIO, IProviderExport<SSHProvider> {
            public async Task<bool> Work(ICollection<IProvidedIO> io, ICommandSource source, IProvider target, IOperationProgress progress, CancellationToken token) {
                if (io is null) throw new ArgumentNullException(nameof(io));
                if (source is null) return false;
                if (target is not SSHProvider provider) {
                    return false;
                }
                var added = false;
                var entries = source.Items?.OfType<SSHEntry>()?.ToList();
                if (entries?.Count > 0) {
                    var client = await provider.Client.Ready(token);
                    var streams = new List<SCPStreamSource>();
                    foreach (var entry in entries) {
                        if (entry?.Info?.Kind == SSHEntryKind.File) {
                            streams.Add(new SCPStreamSource(entry, client));
                        }
                        if (entry?.Info?.Kind == SSHEntryKind.Directory) {
                            await foreach (var info in client.ListRecursively(entry.Uri, token)) {
                                streams.Add(new SCPStreamSource(new SSHEntry(provider, info), client));
                            }
                        }
                    }
                    io.Add(new ProvidedIO { StreamSets = new[] { new SCPStreamSet(streams) } });
                    added = true;
                }
                //var treeNodes = source.Items?.OfType<FileSystemTreeNode>()?.ToList();
                //if (treeNodes?.Count > 0) {
                //    var streams = treeNodes.Select(n => n.StreamSource);
                //    var streamSet = new FileSystemTreeNode.StreamSet(streams);
                //    io.Add(new ProvidedIO { StreamSets = new[] { streamSet } });
                //    added = true;
                //}
                return await Task.FromResult(added);
            }
        }

        private sealed class CopyProvidedIO : ICopyProvidedIO, IProviderExport<SSHProvider> {
            public async Task<bool> Work(IEnumerable<IProvidedIO> io, IProvider target, IOperationProgress progress, CancellationToken token) {
                if (io is null) throw new ArgumentNullException(nameof(io));
                if (target is not SSHProvider provider) {
                    return false;
                }
                var files = await io.FlattenFiles(progress, token);
                if (files.Count > 0) {
                    return await new CopyFromFiles().Work(files, provider, progress, token);
                }
                var streams = io.StreamSets();
                if (streams.Count > 0) {
                    return await new CopyFromStreams().Work(streams, provider, progress, token);
                }
                return false;
            }

            private sealed class CopyFromFiles {
                public async Task<bool> Work(IReadOnlyCollection<ProvidedFile> files, SSHProvider provider, IOperationProgress progress, CancellationToken token) {
                    if (null == files) throw new ArgumentNullException(nameof(files));
                    if (null == provider) throw new ArgumentNullException(nameof(provider));
                    if (files.Count == 0) {
                        return false;
                    }
                    var items = files.Select(file => new FileItem(provider, file)).Where(item => !string.IsNullOrWhiteSpace(item.DestinationPath)).ToList();
                    var dests = items.Select(item => item.DestinationDirectory).Where(dest => !string.IsNullOrWhiteSpace(dest)).Distinct();
                    var client = await provider.Client.Ready(token);
                    foreach (var dest in dests) {
                        await client.CreateDirectory(dest, token);
                    }
                    foreach (var item in items) {
                        await Task.Run(cancellationToken: token, function: async () => {
                            var config = provider.Config;
                            var source = item.OriginalPath;
                            var sourceFile = new FileInfo(source);
                            if (sourceFile.Exists) {
                                var destination = item.DestinationPath;
                                using var scpSend = await client.SCPSend(destination, config.Scp.Mode, sourceFile.Length, token);
                                using var scpSendStream = scpSend.Stream();
                                await using var stream = sourceFile.OpenRead();
                                await stream.CopyToAsync(scpSendStream, token);
                            }
                        });
                    }
                    return true;
                }

                private sealed class FileItem {
                    public string RelativePath => _RelativePath ??=
                        Provided.RelativePath?.Trim()?.Trim('/') ?? "";
                    private string _RelativePath;

                    public string DestinationPath => _DestinationPath ??=
                        string.Join('/', Provider.Uri.LocalPath.TrimEnd('/'), RelativePath);
                    private string _DestinationPath;

                    public IReadOnlyList<string> DestinationPathParts => _DestinationPathParts ??=
                        DestinationPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    private IReadOnlyList<string> _DestinationPathParts;

                    public string DestinationDirectory => _DestinationDirectory ??=
                        DestinationPathParts.Count > 1
                            ? '/' + string.Join('/', DestinationPathParts.Take(DestinationPathParts.Count - 1))
                            : "";
                    private string _DestinationDirectory;

                    public string OriginalPath =>
                        Provided.OriginalPath;

                    public ProvidedFile Provided { get; }
                    public SSHProvider Provider { get; }

                    public FileItem(SSHProvider provider, ProvidedFile provided) {
                        Provided = provided ?? throw new ArgumentNullException(nameof(provided));
                        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
                    }
                }
            }

            private sealed class CopyFromStreams {
                public async Task<bool> Work(IReadOnlyList<IEntryStreamSet> entryStreamSets, SSHProvider provider, IOperationProgress progress, CancellationToken token) {
                    if (null == provider) throw new ArgumentNullException(nameof(provider));
                    if (null == entryStreamSets) throw new ArgumentNullException(nameof(entryStreamSets));
                    if (entryStreamSets.Count == 0) {
                        return false;
                    }
                    await Task.WhenAll(entryStreamSets.Select(set => Task.Run(cancellationToken: token, function: async () => {
                        await set.Consume(progress: progress, token: token, consuming: async (source, stream, progress, token) => {
                            var relativePath = source.RelativePath?.Trim()?.Trim(PATH.DirectorySeparatorChar, PATH.AltDirectorySeparatorChar) ?? "";
                            if (relativePath != "") {
                                var path = string.Join('/', provider.Uri.LocalPath.TrimEnd('/'), relativePath);
                                var config = provider.Config;
                                var client = await provider.Client.Ready(token);
                                if (stream != null) {
                                    using var scpSend = await client.SCPSend(path, config.Scp.Mode, source.StreamLength, token);
                                    using var scpSendStream = scpSend.Stream();
                                    await stream.CopyToAsync(scpSendStream, token);
                                }
                            }
                        });
                    })));
                    return true;
                }
            }
        }
    }
}