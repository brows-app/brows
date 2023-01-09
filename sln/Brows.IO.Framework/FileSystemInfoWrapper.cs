using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Config;
    using Threading.Tasks;

    internal class FileSystemInfoWrapper {
        private static readonly ILog Log = Logging.For(typeof(FileSystemInfoWrapper));

        private PropSysConfig PropertySystem =>
            _PropertySystem ?? (
            _PropertySystem = PropSysConfig.Instance);
        private PropSysConfig _PropertySystem;

        private readonly FileInfo File;
        private readonly DirectoryInfo Directory;
        private readonly Dictionary<string, object> Property = new Dictionary<string, object>();

        private Task RefreshedInfo;
        private Task RefreshedProperty;

        public FileAttributes? Attributes { get; private set; }
        public DateTime? CreationTime { get; private set; }
        public DateTime? CreationTimeUtc { get; private set; }
        public string Extension { get; private set; }
        public DateTime? LastAccessTime { get; private set; }
        public DateTime? LastAccessTimeUtc { get; private set; }
        public DateTime? LastWriteTime { get; private set; }
        public DateTime? LastWriteTimeUtc { get; private set; }
        public long? Length { get; private set; }
        public string LinkTarget { get; private set; }
        public string Name { get; private set; }
        public FileSystemEntryKind? Kind { get; private set; }

        public bool RefreshingInfo {
            get => _RefreshingInfo;
            set {
                if (_RefreshingInfo != value) {
                    _RefreshingInfo = value;

                    if (_RefreshingInfo) {
                        RefreshedInfo = null;
                    }
                }
            }
        }
        private bool _RefreshingInfo;

        public bool RefreshingProperty {
            get => _RefreshingProperty;
            set {
                if (_RefreshingProperty != value) {
                    _RefreshingProperty = value;

                    if (_RefreshingProperty) {
                        RefreshedProperty = null;
                    }
                }
            }
        }
        private bool _RefreshingProperty;

        public FileSystemInfo Info { get; }
        public IFilePropertyProvider Prop { get; }
        public CancellationToken CancellationToken { get; }
        public IReadOnlyList<string> View { get; }

        public FileSystemInfoWrapper(FileSystemInfo info, IFilePropertyProvider prop, IReadOnlyList<string> view, CancellationToken cancellationToken) {
            Info = info;
            Name = Info?.Name;
            File = Info as FileInfo;
            Directory = Info as DirectoryInfo;
            Kind =
                File != null ? FileSystemEntryKind.File :
                Directory != null ? FileSystemEntryKind.Directory :
                FileSystemEntryKind.Unknown;
            Prop = prop;
            View = view ?? Array.Empty<string>();
            CancellationToken = cancellationToken;
        }

        public async Task<object> Get(string propertyKey) {
            if (RefreshedProperty == null) {
                RefreshedProperty = new Func<Task>(async () => {
                    Property.Clear();
                    try {
                        var provider = Prop;
                        if (provider != null) {
                            var file = Info?.FullName;
                            if (file != null) {
                                var propKeys = PropertySystem.Keys;
                                var viewKeys = View.Where(k => propKeys.Contains(k)).ToList();
                                var properties = provider.GetProperties(file, viewKeys, CancellationToken);
                                await foreach (var property in properties) {
                                    Property[property.Key] = property.Value;
                                }
                            }
                        }
                    }
                    finally {
                        RefreshingProperty = false;
                    }
                })();
            }
            await RefreshedProperty;
            return Property.TryGetValue(propertyKey, out var value)
                ? value
                : null;
        }

        public async Task<T> Get<T>(Func<FileSystemInfoWrapper, T> func) {
            if (null == func) throw new ArgumentNullException(nameof(func));
            if (RefreshedInfo == null) {
                RefreshedInfo = Async.Run(CancellationToken, () => {
                    if (Log.Debug()) {
                        Log.Debug(
                            nameof(RefreshingInfo),
                            $"{nameof(Info)} > {Info}");
                    }
                    try {
                        var info = Info;

                        if (RefreshingInfo) {
                            info?.Refresh();
                        }
                        var infoExists = info?.Exists;
                        if (infoExists != true) {
                            info = null;
                        }
                        var file = info as FileInfo;
                        var directory = info as DirectoryInfo;
                        Kind =
                            file != null ? FileSystemEntryKind.File :
                            directory != null ? FileSystemEntryKind.Directory :
                            FileSystemEntryKind.Unknown;
                        Attributes = info?.Attributes;
                        CreationTime = info?.CreationTime;
                        CreationTimeUtc = info?.CreationTimeUtc;
                        Extension = file?.Extension;
                        LastAccessTime = info?.LastAccessTime;
                        LastAccessTimeUtc = info?.LastAccessTimeUtc;
                        LastWriteTime = info?.LastWriteTime;
                        LastWriteTimeUtc = info?.LastWriteTimeUtc;
                        Length = file?.Length;
                        LinkTarget = info?.LinkTarget;
                        Name = info?.Name;
                    }
                    finally {
                        RefreshingInfo = false;
                    }
                });
            }
            await RefreshedInfo;
            return func(this);
        }
    }
}
