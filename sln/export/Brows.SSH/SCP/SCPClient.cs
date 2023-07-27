using Brows.SSH;
using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.SCP {
    internal sealed class SCPClient {
        private string Host =>
            _Host ?? (
            _Host = string.IsNullOrWhiteSpace(Uri.UserInfo) ? Uri.Host : $"{Uri.UserInfo}@{Uri.Host}");
        private string _Host;

        private async Task Copy(SSHEntryInfo remoteDirectory, DirectoryInfo localDestination, CancellationToken token) {
            if (null == remoteDirectory) throw new ArgumentNullException(nameof(remoteDirectory));
            if (null == localDestination) throw new ArgumentNullException(nameof(localDestination));
            using (var process = new Process()) {
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = "scp";
                process.StartInfo.Arguments = $"-r {Host}:{remoteDirectory.Path} \"{localDestination.FullName}\"";
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                for (; ; ) {
                    var errorStr = new StringBuilder();
                    var outputStr = new StringBuilder();
                    var errorBuff = new Memory<char>(new char[1024]);
                    var outputBuff = new Memory<char>(new char[1024]);
                    var errorRead = await process.StandardError.ReadAsync(errorBuff, token);
                    if (errorRead > 0) {
                        errorStr.Append(errorBuff.Slice(0, errorRead));
                    }
                    var outputRead = await process.StandardOutput.ReadAsync(outputBuff, token);
                    if (outputRead > 0) {
                        outputStr.Append(outputBuff.Slice(0, outputRead));
                    }
                    if (outputRead == 0 && errorRead == 0) {
                        if (process.HasExited) {
                            break;
                        }
                    }
                }
            }
        }

        public Uri Uri { get; private set; }
        public SecureString Password { get; private set; }
    }
}
