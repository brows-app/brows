using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FILE = System.IO.File;
using PATH = System.IO.Path;

namespace Brows.Config;

internal sealed class ConfigFileInfo {
    private static readonly ILog Log = Logging.For(typeof(ConfigFileInfo));

    private string GetPath(string root) {
        return PATH.Combine([root, .. Path]);
    }

    private async Task<FileInfo> WritePath(string root, string content, CancellationToken token) {
        var filePath = GetPath(root);
        var directory = PATH.GetDirectoryName(filePath);
        await CreateDirectory(directory, token).ConfigureAwait(false);

        var fileInfo = await Task
            .Run(() => new FileInfo(filePath), token)
            .ConfigureAwait(false);
        if (Log.Info()) {
            Log.Info($"Writing: {fileInfo.FullName}");
        }
        var stream = await Task
            .Run(() => fileInfo.Open(FileMode.Create, FileAccess.Write), token)
            .ConfigureAwait(false);
        using (stream) {
            using (var writer = new StreamWriter(stream)) {
                await writer.WriteAsync(content).ConfigureAwait(false);
            }
        }
        return fileInfo;
    }

    private Task<string> ReadPath(string root, CancellationToken token) {
        return Task.Run(cancellationToken: token, function: () => {
            var filePath = GetPath(root);
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Exists) {
                if (Log.Info()) {
                    Log.Info($"Reading: {fileInfo.FullName}");
                }
                return FILE.ReadAllText(fileInfo.FullName);
            }
            if (Log.Info()) {
                Log.Info($"Returning null for nonexistent file: {fileInfo.FullName}");
            }
            return null;
        });
    }

    private static async Task<DirectoryInfo> CreateDirectory(string path, CancellationToken token) {
        var timeout = TimeSpan.FromSeconds(2.5);
        var stopwatch = new Stopwatch();
        return await Task.Run(cancellationToken: token, function: async () => {
            for (; ; )
            {
                if (stopwatch.IsRunning) {
                    await Task.Delay(250, token).ConfigureAwait(false);
                }
                else {
                    stopwatch.Start();
                }
                var info = new DirectoryInfo(path);
                if (info.Exists) {
                    return info;
                }
                if (Log.Info()) {
                    Log.Info($"Creating nonexistent directory: {info.FullName}");
                }
                try {
                    Directory.CreateDirectory(info.FullName);
                }
                catch (Exception ex) {
                    if (stopwatch.Elapsed > timeout) {
                        throw;
                    }
                    Log.Error(ex);
                }
            }
        }).ConfigureAwait(false);
    }

    public IEnumerable<string> Path { get; }
    public ConfigFileSerializer Serializer { get; }

    public ConfigFileInfo(IEnumerable<string> path, ConfigFileSerializer serializer) {
        Path = path;
        Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public Task<string> Text(string root, CancellationToken token) {
        return ReadPath(root, token);
    }

    public Task<FileInfo> File(string root, CancellationToken token) {
        return Task.Run(cancellationToken: token, function: () => {
            var info = new FileInfo(GetPath(root));
            info.Refresh();
            return info;
        });
    }

    public async Task<T> Write<T>(string root, T target, CancellationToken token) {
        var text = Serializer.Serialize(target);
        await WritePath(root, text, token).ConfigureAwait(false);
        return target;
    }

    public async Task<T> Read<T>(string root, T target, CancellationToken token) {
        var text = await ReadPath(root, token).ConfigureAwait(false);
        if (text == null) {
            if (Log.Info()) {
                Log.Info($"Writing default config for type: {target?.GetType()?.Name}");
            }
            var defaultText = text = Serializer.Serialize(target);
            if (defaultText != null) {
                await WritePath(root, defaultText, token).ConfigureAwait(false);
            }
        }
        if (text == null) {
            return target;
        }
        try {
            Serializer.Populate(target, text);
        }
        catch (Exception ex) {
            if (Log.Warn()) {
                Log.Warn(ex);
            }
        }
        return target;
    }

    public Task<T> Read<T>(string root, CancellationToken token) where T : new() {
        return Read(root, new T(), token);
    }

    public async Task<T> Watch<T>(string root, Action configured, Action<Exception> errored, CancellationToken token) where T : new() {
        var path = GetPath(root);
        var target = new T();
        await Serializer.Watch(path, target, configured, errored, token).ConfigureAwait(false);
        return target;
    }

    public sealed override string ToString() {
        return $"{GetType().Name}:" + string.Join("/", Path);
    }
}
