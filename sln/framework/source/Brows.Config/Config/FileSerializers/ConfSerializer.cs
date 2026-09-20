using Domore.Conf;
using Domore.Conf.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config.FileSerializers;

/// <summary>
/// This implementation of <see cref="ConfigFileSerializer"/> serializes to and from
/// .conf files, which is a simple key/value format that is easily read and edited
/// by humans.
/// </summary>
internal sealed class ConfSerializer : ConfigFileSerializer {
    public sealed override string Extension => ".conf";

    public sealed override string Serialize(object value) {
        return value?.ConfText(key: "", multiline: true);
    }

    public sealed override T Deserialize<T>(string value) {
        return new T().ConfFrom(value, key: "");
    }

    public sealed override void Populate(object target, string text) {
        if (null == target) throw new ArgumentNullException(nameof(target));
        target.ConfFrom(text, key: "");
    }

    public sealed override Task<IDisposable> Watch(string path, object target, Action configured, Action<Exception> errored, CancellationToken token) {
        var
        confFile = new ConfFile(path, key: "", target);
        confFile.Configured += (s, e) => configured?.Invoke();
        confFile.ConfigureError += (s, e) => errored?.Invoke(e?.GetException());
        confFile.WatchError += (s, e) => errored?.Invoke(e?.GetException());
        return Task.Run(cancellationToken: token, function: () => {
            confFile.Configure(watch: true);
            return (IDisposable)confFile;
        });
    }
}
