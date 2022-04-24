using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class NewDirectory : Command<NewDirectory.Info>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.N, KeyboardModifiers.Control | KeyboardModifiers.Shift);
                yield return new InputTrigger("newdir", "newd", "nd");
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) return false;
            if (context.HasPanel(out var active)) {
                if (context.HasKey(out _)) {
                    if (context.HasCommander(out var commander)) {
                        var trigger = InputTrigger();
                        var directoryName = "'New folder'";
                        await commander.ShowPalette($"{trigger} {directoryName}", trigger.Length + 1, directoryName.Length, cancellationToken);
                        return true;
                    }
                }
                if (context.HasParameter(out var parameter)) {
                    var dir = parameter.DirectoryName?.Trim() ?? "";
                    var open = parameter.Open;
                    if (dir != "") {
                        await active.Deploy(cancellationToken, createDirectories: new[] { dir }, then: async () => {
                            if (open) {
                                await active.OpenCreated(dir, cancellationToken);
                            }
                        });
                        return true;
                    }
                }
            }
            return false;
        }

        public class Info {
            [Argument(Name = "name", Required = true)]
            public string DirectoryName {
                get => _DirectoryName ?? (_DirectoryName = "");
                set => _DirectoryName = value;
            }
            private string _DirectoryName;

            [Switch(Name = "open", ShortName = 'o')]
            public bool Open { get; set; }
        }
    }
}
