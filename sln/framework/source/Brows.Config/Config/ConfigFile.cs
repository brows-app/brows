using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config;

internal sealed class ConfigFile {
    private const string CONFIG = "Config";

    private static readonly ILog Log = Logging.For(typeof(ConfigFile));
    private static readonly Dictionary<Type, ConfigFile> Cache = [];

    private readonly SemaphoreSlim Locker = new(1, 1);

    private TimeSpan LockTimeout => TimeSpan.FromSeconds(5);

    private ConfigFileKind Kind => _Kind ??= GetKind();
    private ConfigFileKind? _Kind;

    private IEnumerable<string> Path => field ??= GetPath();

    private ConfigFileSerializer Serializer => field ??= GetSerializer();

    private ConfigFile(Type targetType) {
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
    }

    private ConfigFileKind GetKind() {
        // The names of types used in this framework must follow a convention.
        // The name of the type should end in the string value of one of the members
        // of the enumeration for the config file kind.
        // That means, in practice, the type name should end in either 'Conf', 'Json',
        // or 'Data'. A special case is made for type names that end in 'Config'.
        // This method parses the config file kind from the type name using
        // that convention.
        var name = TargetType.Name;
        var opts = Enum.GetNames(typeof(ConfigFileKind));
        var kind = opts.FirstOrDefault(opt =>
            name.Length > opt.Length &&
            name.EndsWith(opt, StringComparison.OrdinalIgnoreCase));
        if (kind != null) {
            return (ConfigFileKind)Enum.Parse(typeof(ConfigFileKind), kind);
        }
        if (name.Length > CONFIG.Length && name.EndsWith(CONFIG, StringComparison.OrdinalIgnoreCase)) {
            return ConfigFileKind.Json;
        }
        return ConfigFileKind.Data;
    }

    private IEnumerable<string> GetPath() {
        // The names of types used in this framework should follow a convention.
        // The name of the type should begin with the name of the file that is
        // saved to disk, minus its extension.
        // This method parses the relative path of the respective file for a 
        // type from the type name.
        var kind = $"{Kind}";
        var name = TargetType.Name;
        if (name.Length > kind.Length) {
            if (name.EndsWith(kind, StringComparison.OrdinalIgnoreCase)) {
                name = name.Substring(0, name.Length - kind.Length);
            }
        }
        if (name.Length > CONFIG.Length) {
            if (name.EndsWith(CONFIG, StringComparison.OrdinalIgnoreCase)) {
                name = name.Substring(0, name.Length - CONFIG.Length);
            }
        }
        var ext = Serializer.Extension?.TrimStart('.');
        var file = string.Join(".", name, ext);
        return new[] { kind, file };
    }

    private ConfigFileSerializer GetSerializer() {
        return ConfigFileSerializer.For(Kind);
    }

    public Type TargetType { get; }

    public async Task<T> Lock<T>(Func<ConfigFileInfo, CancellationToken, Task<T>> task, CancellationToken token) {
        if (task is null) {
            throw new ArgumentNullException(nameof(task));
        }
        // The first thing to do is acquire the lock. If, for some reason,
        // the previous callback task is taking a long time and the lock
        // can't be acquired within a timeout, log a message so we at least
        // have a chance of figuring out what's going on (typically we
        // shouldn't ever be in that situation).
        for (; ; )
        {
            var lockTo = LockTimeout;
            var locked = await Locker.WaitAsync(lockTo, token).ConfigureAwait(false);
            if (locked) {
                break;
            }
            if (Log.Warn()) {
                Log.Warn($"Timed out waiting for lock",
                         $"{lockTo}",
                         $"{this}");
            }
        }
        // Here, the lock has been acquired, so do the task. Afterward,
        // release the lock.
        try {
            var t = task(new ConfigFileInfo(Path, Serializer), token);
            if (t == null) {
                throw new ArgumentException(paramName: nameof(task), message: "Returned value from task is null.");
            }
            return await t.ConfigureAwait(false);
        }
        finally {
            Locker.Release();
        }
    }

    public sealed override string ToString() {
        return $"{GetType().Name}:" + string.Join("/", Path);
    }

    public static ConfigFile For(Type targetType) {
        lock (Cache) {
            if (Cache.TryGetValue(targetType, out var item) == false) {
                Cache[targetType] = item = new ConfigFile(targetType);
            }
            return item;
        }
    }
}
