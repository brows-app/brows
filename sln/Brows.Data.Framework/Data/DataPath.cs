using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Brows.Data {
    public class DataPath {
        private static string Encode(string s) {
            var bytes = Encoding.UTF8.GetBytes(s);
            return Convert.ToBase64String(bytes);
        }

        private static string LocalApplicationData() {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
        }

        internal string For(string name, string id, string extension) {
            var root = Root;
            var model = Path.Combine(root, name);
            Directory.CreateDirectory(model);
            while (Directory.Exists(model) == false) {
                Thread.Sleep(0);
            }
            var
            file = Path.Combine(model, Encode(id));
            file = Path.ChangeExtension(file, extension);
            return file;
        }

        public static string Root =>
            _Root ?? (
            _Root = Path.Combine(
                LocalApplicationData(),
                "Brows",
                "data"));
        private static string _Root;
    }
}
