using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Url.Ftp {
    public sealed class FtpClient : UrlClient {
        protected sealed override void Configure(ClientForUrl client) {
            ArgumentNullException.ThrowIfNull(client);
            //client.FtpFileMethod(FtpFileMethod.MultiCwd);
        }

        public Task<UrlListing<FtpListingInfo>> List(CancellationToken token) {
            return List(new FtpListingParser(), token);
        }

        public FtpClient Extend(string path) {
            var other = (FtpClient)MemberwiseClone();
            var otherUrl = new UriBuilder(Uri);
            otherUrl.Path = string.Join('/', otherUrl.Path.TrimEnd('/'), path.TrimStart('/'));
            other.Url = otherUrl.Uri.ToString();
            return other;
        }
    }
}
