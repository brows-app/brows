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
    internal sealed class ConfigFile {
        private static readonly ILog Log = Logging.For(typeof(ConfigFile));
        private static readonly string Extension = "br~ws";

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

        private static async Task<string> CreateRoot(CancellationToken cancellationToken) {
            var root = ConfigPath.Root;
            if (Directory.Exists(root) == false) {
                Directory.CreateDirectory(root);
                for (; ; ) {
                    if (Directory.Exists(root)) {
                        break;
                    }
                    await Task.Delay(10, cancellationToken);
                }
            }
            return root;
        }

        private static async Task<(string Head, string Path)> Path(ConfigFilePayload payload, CancellationToken cancellationToken) {
            if (null == payload) throw new ArgumentNullException(nameof(payload));
            var root = await Task.Run(() => CreateRoot(cancellationToken), cancellationToken);
            var head = Head(payload);
            var path = PATH.Combine(root, head + ".br~ws");
            return (Head: head, Path: path);
        }

        private static string Head(ConfigFilePayload payload) {
            if (null == payload) throw new ArgumentNullException(nameof(payload));
            return ".01@" + Encode32(payload.Type) + "#" + Encode32(payload.ID);
        }

        public async Task<object> Save(ConfigFilePayload payload, CancellationToken cancellationToken) {
            if (null == payload) throw new ArgumentNullException(nameof(payload));
            var data = payload.Data;
            var text = data?.ConfText(key: "", multiline: true);
            if (text != null) {
                var file = await Path(payload, cancellationToken);
                var fileText = file.Head + Environment.NewLine + Environment.NewLine + text;
                if (Log.Info()) {
                    Log.Info(
                        nameof(Save) + " > " + payload.Type,
                        nameof(file) + " > " + file.Path);
                }
                await File.WriteAllTextAsync(file.Path, fileText, cancellationToken);
            }
            return data;
        }

        public async Task<object> Load(ConfigFilePayload payload, CancellationToken cancellationToken) {
            if (null == payload) throw new ArgumentNullException(nameof(payload));
            var data = payload.Data;
            var file = await Path(payload, cancellationToken);
            if (Log.Info()) {
                Log.Info(
                    nameof(Load) + " > " + payload.Type,
                    nameof(file) + " > " + file.Path);
            }
            var text = default(string);
            try {
                text = await File.ReadAllTextAsync(file.Path, cancellationToken);
            }
            catch (FileNotFoundException) {
                return data;
            }
            return data?.ConfFrom(text, key: "");
        }
    }
}
