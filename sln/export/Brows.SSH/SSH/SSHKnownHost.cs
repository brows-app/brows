using Brows.SSH.Native;
using System;
using System.IO;

namespace Brows.SSH {
    public sealed class SSHKnownHost {
        public string Key { get; }
        public string Name { get; }
        public SSHKnownHostStatus Status { get; }

        public SSHKnownHost(string name, string key, SSHKnownHostStatus status) {
            Name = name;
            Key = key;
            Status = status;
        }

        public static SSHKnownHost Get(Conn conn, string hostName, int port) {
            var fileKnownHosts = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify), ".ssh", "known_hosts");
            var fileExists = File.Exists(fileKnownHosts);
            if (fileExists == false) {
                return new SSHKnownHost(name: null, key: null, status: SSHKnownHostStatus.NotFound);
            }
            using (var knownHost = new KnownHost())
            using (var knownHosts = new KnownHosts(conn)) {
                knownHosts.Check(fileKnownHosts, hostName, port, knownHost);
                return new SSHKnownHost(
                    name: knownHost.NamePlain,
                    key: knownHost.KeyBase64,
                    status:
                        knownHost.Status == KnownHostStatus.Match ? SSHKnownHostStatus.Match :
                        knownHost.Status == KnownHostStatus.Mismatch ? SSHKnownHostStatus.Mismatch :
                        knownHost.Status == KnownHostStatus.NotFound ? SSHKnownHostStatus.NotFound :
                        throw new InvalidOperationException());
            }
        }
    }
}
