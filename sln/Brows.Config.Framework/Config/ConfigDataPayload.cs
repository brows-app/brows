using Domore.Conf.Extensions;
using Domore.Logs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FILE = System.IO.File;
using PATH = System.IO.Path;

namespace Brows.Config {
    internal class ConfigDataPayload {
        private static readonly ILog Log = Logging.For(typeof(ConfigDataPayload));

        private async Task<(string Head, string Path)> File(CancellationToken cancellationToken) {
            var root = await ConfigPath.DataReady(cancellationToken);
            var head = ConfigDataHead.For(Type, ID);
            var path = PATH.Combine(root, head + ".brws");
            return (Head: head, Path: path);
        }

        public string ID { get; }
        public string Type { get; }

        public ConfigDataPayload(string type, string id) {
            ID = id;
            Type = type;
        }

        public static ConfigDataPayload<TData> For<TData>(TData data, string id) {
            return new Of<TData>(data, id);
        }

        private sealed class Of<TData> : ConfigDataPayload<TData> {
            public Of(TData data, string id) : base(data, id) {
            }

            public sealed override async Task<TData> Save(CancellationToken cancellationToken) {
                var data = Data;
                var text = data?.ConfText(key: "", multiline: true);
                if (text != null) {
                    string comment(string s) => $"# {s}";
                    var file = await File(cancellationToken);
                    var fileText = string.Join(Environment.NewLine, comment(file.Head), comment(Type), comment(ID), "", text);
                    if (Log.Info()) {
                        Log.Info(
                            nameof(Save) + " > " + Type,
                            nameof(file) + " > " + file.Path);
                    }
                    await FILE.WriteAllTextAsync(file.Path, fileText, cancellationToken);
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
                    text = await System.IO.File.ReadAllTextAsync(file.Path, cancellationToken);
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

    internal abstract class ConfigDataPayload<TData> : ConfigDataPayload {
        protected TData Data { get; }

        protected ConfigDataPayload(TData data, string id) : base(type: typeof(TData).Name, id: id) {
            Data = data;
        }

        public abstract Task<TData> Save(CancellationToken cancellationToken);
        public abstract Task<TData> Load(CancellationToken cancellationToken);
    }
}
