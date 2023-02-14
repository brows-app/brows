using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PATH = System.IO.Path;

namespace Domore.Logs {
    using Conf;
    using IO;

    internal class LogConfFile {
        private CancellationTokenSource EventCancellation;
        private FileSystemEvent Event { get; }

        private async void Event_Handler(object sender, FileSystemEventArgs e) {
            if (e != null) {
                if (e.Name == Name) {
                    try { EventCancellation?.Cancel(); } catch { }
                    try { EventCancellation?.Dispose(); } catch { }
                    using (var eventCancellation = EventCancellation = new CancellationTokenSource()) {
                        try {
                            await Task.Delay(1000, eventCancellation.Token);
                            if (object.ReferenceEquals(eventCancellation, EventCancellation)) {
                                await Configure(eventCancellation.Token);
                            }
                        }
                        catch {
                        }
                    }
                }
            }
        }

        private async Task<string> Read(CancellationToken cancellationToken) {
            var tries = 1;
            var triesMax = 2;
            for (; ; ) {
                try {
                    return await File.ReadAllTextAsync(Path, cancellationToken);
                }
                catch (FileNotFoundException) {
                    return "";
                }
                catch (IOException) {
                    if (tries++ > triesMax) {
                        throw;
                    }
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private async Task<object> Configure(CancellationToken cancellationToken) {
            var text = await Read(cancellationToken);
            var conf = Conf.Contain(text);
            return conf.Configure(Logging.Config, key: "");
        }

        public string Path { get; }
        public string Name { get; }
        public string Directory { get; }

        public LogConfFile(string path) {
            Path = path;
            Name = PATH.GetFileName(Path);
            Directory = PATH.GetDirectoryName(Path);
            Event = new FileSystemEvent(Directory);
            Configure(default).ContinueWith(_ => {
                Event.Handler += Event_Handler;
            });
        }
    }
}
