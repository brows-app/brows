using Domore.Logs;
using Domore.Runtime.Win32;
using Domore.Threading;
using Domore.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class Metadata : FileSystemCommand<MetadataParameter> {
        private static readonly ILog Log = Logging.For(typeof(Metadata));

        private readonly STAThreadPool ThreadPool = new(nameof(Metadata)) { WorkerCountMin = 0 };
        private readonly Dictionary<string, TaskCache<PropertyDescription>> PropertyCache = new();

        private async Task<PropertyDescription> Property(string name, CancellationToken token) {
            if (PropertyCache.TryGetValue(name, out var value) == false) {
                PropertyCache[name] = value = new(async token => await ThreadPool.Work(
                    name: name,
                    cancellationToken: token,
                    work: () => PropertySystem.GetPropertyDescription(name)));
            }
            return await value.Ready(token);
        }

        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(IEntry)
        };

        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasParameter(out var parameter)) return false;
            if (false == context.HasSourceFileSystemInfo(out _, out var fileSystemInfos)) return false;
            return context.Operate(async (progress, token) => {
                var parameterSet = new ConcurrentDictionary<string, string>(parameter.Set);
                var propertyList = await Task.WhenAll(parameterSet.Keys
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(async p => KeyValuePair.Create(p, await Property(p, token))));
                return await progress.Children(fileSystemInfos, fileSystemInfo => (fileSystemInfo.Name, OperationProgressKind.None, async (progress, token) => {
                    if (fileSystemInfo is not FileInfo file) {
                        throw new MetadataInfoIsNotFileException(fileSystemInfo);
                    }
                    progress.Change(addTarget: 1);
                    var propSet = false;
                    var propDict = propertyList.ToDictionary(p => p.Key, p => p.Value);
                    var propFile = file.FullName;
                    var propError = new Dictionary<string, Exception>();
                    await Task.Run(cancellationToken: token, action: () => {
                        foreach (var pair in propDict) {
                            var propertyValue = parameterSet[pair.Key];
                            var propertyDescription = pair.Value;
                            try {
                                PropertySystem.SetPropertyValue(propFile, propertyDescription, propertyValue);
                                propSet = true;
                            }
                            catch (Exception ex) {
                                if (Log.Info()) {
                                    Log.Info(Log.Join(nameof(Exception), propertyDescription.CanonicalName, propFile), ex);
                                }
                                propError[propertyDescription.CanonicalName] = ex;
                            }
                        }
                    });
                    if (propSet) {
                        progress.Change(addProgress: 1);
                    }
                    else {
                        throw new MetadataFailedException(fileSystemInfo, propError);
                    }
                }
                ));
            });
        }

        private sealed class MetadataInfoIsNotFileException : Exception {
            public FileSystemInfo Info { get; }

            public MetadataInfoIsNotFileException(FileSystemInfo info) {
                Info = info;
            }
        }

        private sealed class MetadataFailedException : Exception {
            public FileSystemInfo Info { get; }
            public Dictionary<string, Exception> PropertyException { get; }

            public MetadataFailedException(FileSystemInfo info, Dictionary<string, Exception> propertyException) {
                Info = info;
                PropertyException = propertyException;
            }
        }
    }
}
