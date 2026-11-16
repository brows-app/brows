using System;

namespace Brows.Commands;

internal sealed class FtpUrl : Command {
    protected sealed override bool ArbitraryWork(ICommandContext context) {
        if (context is null) {
            return false;
        }
        if (context.HasInput(out var input) == false) {
            return false;
        }
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) == false) {
            return false;
        }
        var schemeIsFtp = uri.Scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase);
        var schemeIsFtps = uri.Scheme.Equals("ftps", StringComparison.OrdinalIgnoreCase);
        var schemeIsExplicitFtps = uri.Scheme.Equals("ftpes", StringComparison.OrdinalIgnoreCase);
        var schemeIsSupported = schemeIsFtp || schemeIsFtps || schemeIsExplicitFtps;
        if (schemeIsSupported == false) {
            return false;
        }
        if (uri.Host.Length == 0) {
            return false;
        }
        var id = uri.ToString();
        return context.Operate(async (progress, token) => {
            return await context.Provide(id, CommandContextProvide.ActivePanel, token);
        });
    }
}
