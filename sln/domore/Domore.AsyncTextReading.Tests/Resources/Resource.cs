using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FILE = System.IO.File;
using PATH = System.IO.Path;

namespace Domore.Resources {
    internal class Resource : IDisposable {
        private static async Task<string> Read(string name) {
            var assembly = Assembly.GetExecutingAssembly();
            var resource = $"{typeof(Resource).Namespace}.{name}";
            await using (var stream = assembly.GetManifestResourceStream(resource)) {
                using (var reader = new StreamReader(stream)) {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        public string Name { get; }
        public string Path { get; }

        public Resource(string name) {
            Name = name;
            Path = PATH.GetTempFileName();
        }

        public async Task<string> Text() {
            return
                _Text ?? (
                _Text = await Read(Name));
        }
        private string _Text;

        public FileInfo File() {
            return new FileInfo(Path);
        }

        public async Task Save(Encoding encoding) {
            var text = await Text();
            await FILE.WriteAllTextAsync(Path, text, encoding);
        }

        public static string[] Names() {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetManifestResourceNames()
                .Select(name => name.Split(".").Last())
                .ToArray();
        }

        void IDisposable.Dispose() {
            FILE.Delete(Path);
        }
    }
}
