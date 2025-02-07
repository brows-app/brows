using Domore.Conf.Cli;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    internal sealed class Open : FtpCommand<Open.Parameter> {
        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(FtpEntry)
        };

        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.HasPanel(out var active)) return false;
            if (false == context.GetParameter(out var parameter)) return false;
            if (false == context.HasSource(out FtpEntry _, out var entries)) {
                return false;
            }
            var where = parameter.Where ?? CommandContextProvide.ActivePanel;
            return context.Operate(async (progress, token) => {
                async Task<bool> open(FtpEntry entry, CancellationToken token) {
                    if (entry?.Kind == FileProtocolEntryKind.Directory) {
                        var provided = await context.Provide(id: entry.ID, where, token);
                        if (provided) {
                            return true;
                        }
                    }
                    return false;
                }
                var worked = false;
                foreach (var entry in entries) {
                    if (entry is FtpEntry file) {
                        worked |= await open(file, token);
                    }
                }
                return worked;
            });
        }

        public sealed class Parameter {
            [CliArgument]
            public CommandContextProvide? Where { get; set; }
        }
    }
}
