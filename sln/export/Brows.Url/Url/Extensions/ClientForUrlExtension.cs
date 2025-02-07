using System;

namespace Brows.Url.Extensions {
    internal static class ClientForUrlExtension {
        public static Exception Try(this ClientForUrl clientForUrl) {
            ArgumentNullException.ThrowIfNull(clientForUrl);
            try {
                try {
                    clientForUrl.TxRx();
                }
                catch (Exception ex) {
                    throw new ClientForUrlException(clientForUrl.ErrorString(), ex);
                }
            }
            catch (Exception ex) {
                return ex;
            }
            return null;
        }
    }
}
