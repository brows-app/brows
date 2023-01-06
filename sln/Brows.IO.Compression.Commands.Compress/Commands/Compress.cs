using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using IO.Compression;
    using Triggers;

    internal class Compress : Command<Compress.Parameter>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new InputTrigger("zip");
                yield return new InputTrigger("compress");
            }
        }

        protected override async Task<bool> Work(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var active) == false) {
                return false;
            }
            var entries = active.Selection();
            if (entries.Any() == false) {
                return false;
            }
            if (context.HasParameter(out var param) == false) {
                param = new Parameter();
            }
            var format = CompressionFormat.Create(param.Format);
            var output = string.IsNullOrWhiteSpace(param.Output)
                ? Path.ChangeExtension(entries.First().Name, format.Extension)
                : param.Output;
            var fullyQualified = Path.IsPathFullyQualified(output);
            if (fullyQualified == false) {
                output = Path.Combine(active.Directory, output);
            }
            var extensionless = string.IsNullOrWhiteSpace(Path.GetExtension(output));
            if (extensionless) {
                output = Path.ChangeExtension(output, format.Extension);
            }
            var operating = Operate(context, async (progress, cancellationToken) => {
                await format.Create(new CompressionCreate {
                    Entries = entries,
                    Output = output,
                    Progress = progress
                }, cancellationToken);
            });
            return await Task.FromResult(operating);
        }

        public class Parameter {
            [Argument(Name = "output")]
            public string Output { get; set; }

            [Switch(Name = "format", ShortName = 'f')]
            public string Format { get; set; }
        }
    }
}
