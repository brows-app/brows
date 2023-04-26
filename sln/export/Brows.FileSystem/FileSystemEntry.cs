using Domore.Logs;
using Domore.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class FileSystemEntry : Entry<FileSystemProvider> {
        private static readonly ILog Log = Logging.For(typeof(FileSystemEntry));

        private FileSystemEntryRefreshDelay RefreshDelay;

        private async Task<FileSystemInfo> FileSystemWork(CancellationToken token) {
            return await Task.Run(cancellationToken: token, function: () => {
                Info.Refresh();
                return Info;
            });
        }

        private async Task<IReadOnlyDictionary<string, string>> MetadataWork(CancellationToken token) {
            var service = Provider.Factory.MetadataFileReader;
            if (service != null) {
                if (Info is FileInfo file) {
                    var keys = Provider.ObservedKeys;
                    var dict = keys
                        .Where(key => Provider.Factory.Metadata.System.ContainsKey(key))
                        .ToDictionary(
                            key => Provider.Factory.Metadata.System[key].Definition,
                            key => "");
                    await service.Work(file.FullName, dict, null, token);
                    return dict.ToDictionary(item => item.Key.Key, item => item.Value);
                }
            }
            return new Dictionary<string, string>(capacity: 0);
        }

        public object Stream =>
            new FileSystemStreamGui(this);

        public TaskCache<FileSystemInfo> FileSystemCache =>
            _FileSystemCache ?? (
            _FileSystemCache = new(async token => await FileSystemWork(token)));
        private TaskCache<FileSystemInfo> _FileSystemCache;

        public TaskCache<IReadOnlyDictionary<string, string>> MetadataCache =>
            _MetadataCache ?? (
            _MetadataCache = new(async token => await MetadataWork(token)));
        private TaskCache<IReadOnlyDictionary<string, string>> _MetadataCache;

        public string Extension { get; }
        public sealed override string ID { get; }
        public sealed override string Name { get; }

        public FileSystemInfo Info { get; }
        public FileSystemEntryKind Kind { get; }

        public new FileSystemProvider Provider =>
            base.Provider;

        public FileSystemEntry(FileSystemProvider provider, FileSystemInfo info) : base(provider) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            Kind = Info is FileInfo
                ? FileSystemEntryKind.File
                : Info is DirectoryInfo
                    ? FileSystemEntryKind.Directory
                    : FileSystemEntryKind.Unknown;
            ID = Info.FullName;
            Name = Info.Name;
            Extension = Info.Extension;
        }

        public void RefreshAfter(int delay) {
            if (Log.Debug()) {
                Log.Debug(Log.Join(nameof(RefreshAfter), delay, ID));
            }
            var refreshDelay = RefreshDelay;
            if (refreshDelay == null) {
                refreshDelay = RefreshDelay = new FileSystemEntryRefreshDelay(this, delay, Token);
                refreshDelay.Completed.ContinueWith(_ => RefreshDelay = null);
            }
        }

        public void Refresh(bool delayed) {
            if (Log.Debug()) {
                Log.Debug(Log.Join(nameof(Refresh), nameof(delayed), delayed, ID));
            }
            if (delayed) {
                try {
                    var lastWriteTime = Info.LastWriteTimeUtc;
                    var currentWriteTime = File.GetLastWriteTimeUtc(Info.FullName);
                    if (currentWriteTime == lastWriteTime) {
                        if (Log.Debug()) {
                            Log.Debug(Log.Join(nameof(currentWriteTime), ID));
                        }
                    }
                    else {
                        NotifyPropertyChanged(nameof(Stream));
                    }
                }
                catch (Exception ex) {
                    if (Log.Warn()) {
                        Log.Warn(ex);
                    }
                }
            }
            else {
                NotifyPropertyChanged(nameof(Stream));
            }
            Refresh();
        }
    }
}
