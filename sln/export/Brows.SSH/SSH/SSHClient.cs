using Brows.SSH.Clients;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows.SSH {
    internal abstract class SSHClient {
        private static readonly ILog Log = Logging.For(typeof(SSHClient));

        private string UriString =>
            _UriString ?? (
            _UriString = Uri.ToString().TrimEnd('/'));
        private string _UriString;

        protected async IAsyncEnumerable<SSHClientOutput> SSH(string command, [EnumeratorCancellation] CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(SSH), command));
            }
            var ch = Channel.CreateUnbounded<SSHClientOutput>();
            var writer = ch.Writer;
            var reader = ch.Reader;
            await Task.Run(cancellationToken: token, function: async () => {
                var error = default(Exception);
                try {
                    using (var process = new Process()) {
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.FileName = "ssh";
                        process.StartInfo.Arguments = $"-t {UriString} \"{command}\"";
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.RedirectStandardOutput = true;

                        process.ErrorDataReceived += (s, e) => { writer.TryWrite(new SSHClientOutput(e?.Data, SSHClientOutputKind.Error)); };
                        process.OutputDataReceived += (s, e) => { writer.TryWrite(new SSHClientOutput(e?.Data, SSHClientOutputKind.Output)); };
                        process.Start();
                        process.BeginErrorReadLine();
                        process.BeginOutputReadLine();
                        await process.WaitForExitAsync(token);
                    }
                }
                catch (Exception ex) {
                    error = ex;
                }
                writer.Complete(error);
            });
            await foreach (var item in reader.ReadAllAsync(token)) {
                switch (item.Kind) {
                    case SSHClientOutputKind.Error:
                        if (Log.Warn()) {
                            Log.Warn(item.Content);
                        }
                        break;
                    case SSHClientOutputKind.Output:
                        if (Log.Info()) {
                            Log.Info(item.Content);
                        }
                        break;
                }
                yield return item;
            }
        }

        protected async Task SCP(IEnumerable<SSHEntryInfo> remote, DirectoryInfo local, CancellationToken token) {
            if (null == remote) throw new ArgumentNullException(nameof(remote));
            var remoteDirs = remote.Where(i => i.Kind == SSHEntryKind.Directory).ToList();
            var remoteDirsTask = Task.WhenAll(remoteDirs.Select(remoteDir => Task.Run(cancellationToken: token, function: async () => {

            })));

            var remoteFiles = remote.Except(remoteDirs).ToList();


            var outputChan = Channel.CreateUnbounded<char>();
            await Task.Run(cancellationToken: token, function: async () => {
                using (var process = new Process()) {
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName = "scp";
                    process.StartInfo.Arguments = $"";
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();

                    var exited = process.WaitForExitAsync(token);


                    var readBuffer = new Memory<char>(new char[1024]);
                    var read = await process.StandardOutput.ReadAsync(readBuffer, token);
                    if (read < 0) {

                    }


                    await process.WaitForExitAsync(token);
                }
            });
        }

        public Uri Uri { get; private set; }
        public SecureString Password { get; private set; }

        public abstract IAsyncEnumerable<SSHEntryInfo> List(string path, CancellationToken token);

        public static SSHClient Create(Uri uri, SecureString password) {
            if (null == uri) throw new ArgumentNullException(nameof(uri));
            var
            client = new PosixSSHClient();
            client.Uri = uri;
            client.Password = password;
            return client;
        }
    }
}
