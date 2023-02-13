using Domore.Conf.Extensions;
using Domore.Converting;
using Domore.Logs;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PATH = System.IO.Path;

namespace Brows.Config {
    internal class ConfigDataPayload {
        private static readonly ILog Log = Logging.For(typeof(ConfigDataPayload));

        private static string Encode32(string s) {
            var utf8bytes = Encoding.UTF8.GetBytes(s);
            var base32hex = Base32HexString.From(utf8bytes);
            return base32hex;
        }

        private static string Encode64(string s) {
            var utf8bytes = Encoding.UTF8.GetBytes(s);
            var base64str = Convert.ToBase64String(utf8bytes);
            return base64str;
        }

        private async Task<(string Head, string Path)> Path(CancellationToken cancellationToken) {
            var root = await ConfigPath.DataReady(cancellationToken);
            var head = Head();
            var path = PATH.Combine(root, head + ".brws");
            return (Head: head, Path: path);
        }

        private string Head() {
            return ".01@" + Encode32(Type) + "#" + Encode32(ID);
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
                    var file = await Path(cancellationToken);
                    var fileText = file.Head + Environment.NewLine + Environment.NewLine + text;
                    if (Log.Info()) {
                        Log.Info(
                            nameof(Save) + " > " + Type,
                            nameof(file) + " > " + file.Path);
                    }
                    await File.WriteAllTextAsync(file.Path, fileText, cancellationToken);
                }
                return data;
            }

            public sealed override async Task<TData> Load(CancellationToken cancellationToken) {
                var data = Data;
                var file = await Path(cancellationToken);
                if (Log.Info()) {
                    Log.Info(
                        nameof(Load) + " > " + Type,
                        nameof(file) + " > " + file.Path);
                }
                var text = default(string);
                try {
                    text = await File.ReadAllTextAsync(file.Path, cancellationToken);
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
