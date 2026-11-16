using System;

namespace Brows;

internal sealed class FtpProvider : FtpProviderBase {
    private sealed class ProvideExport : FtpProvideIO<FtpProvider> {
    }

    private sealed class CopyExport : FtpCopyIO<FtpProvider> {
    }

    private sealed class MoveExport : FtpMoveProvidedIO<FtpProvider> {
        private static readonly IWorkProvidedIO CopyService = new CopyExport();

        protected sealed override IWorkProvidedIO Copy => CopyService;
    }

    public FtpProvider(FtpProviderFactory factory, Uri uri, object icon) : base(factory, uri, icon) {
    }
}
