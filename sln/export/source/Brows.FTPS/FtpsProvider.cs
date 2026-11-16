using System;

namespace Brows;

internal sealed class FtpsProvider : FtpProviderBase {
    private sealed class ProvideExport : FtpProvideIO<FtpsProvider> {
    }

    private sealed class CopyExport : FtpCopyIO<FtpsProvider> {
    }

    private sealed class MoveExport : FtpMoveProvidedIO<FtpsProvider> {
        private static readonly IWorkProvidedIO CopyService = new CopyExport();

        protected sealed override IWorkProvidedIO Copy => CopyService;
    }

    public FtpsProvider(FtpsProviderFactory factory, Uri uri, object icon) : base(factory, uri, icon) {
    }
}
