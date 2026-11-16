using System;

namespace Brows.Commands;

internal sealed class SftpUrl : Command {
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
        if (uri.Scheme.Equals("sftp", StringComparison.OrdinalIgnoreCase) == false) {
            return false;
        }
        var locationIsInvalid = uri.Host.Length == 0 || uri.AbsolutePath.StartsWith('/') == false;
        if (locationIsInvalid) {
            return false;
        }
        var id = uri.ToString();
        return context.Operate(async (progress, token) => {
            return await context.Provide(id, CommandContextProvide.ActivePanel, token);
        });
    }
}
