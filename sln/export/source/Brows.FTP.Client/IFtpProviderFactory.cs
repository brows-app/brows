namespace Brows;

internal interface IFtpProviderFactory : IExport {
    FtpClientCache Client { get; }
}
