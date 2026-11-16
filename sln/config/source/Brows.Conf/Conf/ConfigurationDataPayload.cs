using Domore.Conf.Extensions;
using Domore.Logs;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FILE = System.IO.File;
using PATH = System.IO.Path;

namespace Brows.Conf;

internal abstract class ConfigurationDataPayload {
    private static readonly ILog Log = Logging.For(typeof(ConfigurationDataPayload));
    private static readonly Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private async Task<(string Head, string Path)> File(CancellationToken cancellationToken) {
        var root = await ConfigurationPath.DataReady(cancellationToken);
        var path = PATH.Combine(root, Name + ".brws");
        return (Head: Name, Path: path);
    }

    protected ConfigurationDataPayload(string type, string name) {
        Type = type;
        Name = name;
    }

    public string Type { get; }
    public string Name { get; }

    public static ConfigurationDataPayload<TData> For<TData>(TData data, string name) {
        return new Of<TData>(data, name);
    }

    private sealed class Of<TData> : ConfigurationDataPayload<TData> {
        public Of(TData data, string name) : base(data, name) {
        }

        public sealed override async Task<TData> Save(CancellationToken cancellationToken) {
            var data = Data;
            var text = data?.ConfText(key: "", multiline: true);
            if (text != null) {
                string comment(string s) => $"# {s}";
                var file = await File(cancellationToken);
                var fileText = string.Join(Environment.NewLine,
                    comment(file.Head),
                    comment(Type), "", text);
                if (Log.Info()) {
                    Log.Info(
                        nameof(Save) + " > " + Type,
                        nameof(file) + " > " + file.Path);
                }
                await FILE
                    .WriteAllTextAsync(file.Path, fileText, FileEncoding, cancellationToken);
            }
            return data;
        }

        public sealed override async Task<TData> Load(CancellationToken cancellationToken) {
            var data = Data;
            var file = await File(cancellationToken);
            if (Log.Info()) {
                Log.Info(
                    nameof(Load) + " > " + Type,
                    nameof(file) + " > " + file.Path);
            }
            var text = default(string);
            try {
                text = await FILE
                    .ReadAllTextAsync(file.Path, cancellationToken);
            }
            catch (FileNotFoundException) {
                return data;
            }
            return data == null
                ? data
                : data.ConfFrom(text, key: "");
        }
    }
}

internal abstract class ConfigurationDataPayload<TData> : ConfigurationDataPayload {
    protected TData Data { get; }

    protected ConfigurationDataPayload(TData data, string name) : base(type: typeof(TData).Name, name: name) {
        Data = data;
    }

    public abstract Task<TData> Save(CancellationToken cancellationToken);
    public abstract Task<TData> Load(CancellationToken cancellationToken);
}
