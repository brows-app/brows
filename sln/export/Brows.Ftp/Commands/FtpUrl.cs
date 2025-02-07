using System;

namespace Brows.Commands {
    internal sealed class FtpUrl : Command {
        protected sealed override bool ArbitraryWork(ICommandContext context) {
            if (context is null) return false;
            if (context.HasInput(out var input) == false) {
                return false;
            }
            if (input.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) == false) return false;
            if (input.Length <= "ftp://".Length) {
                return false;
            }
            if (Uri.TryCreate(input, UriKind.Absolute, out var uri) == false) {
                return false;
            }




            return context.Operate(async (progress, token) => {
                return await context.Provide(uri.ToString(), CommandContextProvide.ActivePanel, token);
            });
        }
    }
}
