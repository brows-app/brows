using Brows.Exports;
using Brows.SCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PATH = System.IO.Path;

namespace Brows {
    internal sealed class SSHProvider : SSHProviderBase<SSHEntry, SSHConfig> {
        protected sealed override async Task Begin(CancellationToken token) {
            var client = await Client.Ready(token);
            var list = client.List(Uri, token);
            await foreach (var info in list) {
                await Provide(new SSHEntry(this, info));
            }
        }

        public SSHProvider(SSHProviderFactory factory, Uri uri, object icon) : base(factory, uri, icon) {
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
                            await foreach (var info in client.ListFilesRecursively(entry.Uri, token)) {
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
                var streams = io.StreamSets();
                if (streams.Count > 0) {
                    await Task.WhenAll(streams.Select(set => Task.Run(cancellationToken: token, function: async () => {
                        await set.Consume(progress: progress, token: token, consuming: async (source, stream, progress, token) => {
                            var relativePath = source.RelativePath?.Trim()?.Trim(PATH.DirectorySeparatorChar, PATH.AltDirectorySeparatorChar) ?? "";
                            if (relativePath != "") {
                                var path = string.Join('/', provider.Uri.LocalPath.TrimEnd('/'), relativePath);
                                var config = provider.Config;
                                var client = await provider.Client.Ready(token);
                                using var scpSend = await client.SCPSend(path, config.Scp.Mode, source.StreamLength, token);
                                using var scpSendStream = scpSend.Stream();
                                await stream.CopyToAsync(scpSendStream);
                            }
                        });
                    })));
                    return true;
                }
                return false;
            }
        }
    }
}
