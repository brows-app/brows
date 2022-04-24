using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands {
    using Triggers;

    internal class NewFile : Command<NewFile.Info>, ICommandExport {
        protected override IEnumerable<ITrigger> DefaultTriggers {
            get {
                yield return new KeyboardTrigger(KeyboardKey.N, KeyboardModifiers.Control | KeyboardModifiers.Shift | KeyboardModifiers.Alt);
                yield return new InputTrigger("newfile", "newf", "nf");
            }
        }

        protected override async Task<bool> ProtectedWorkAsync(Context context, CancellationToken cancellationToken) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.HasPanel(out var active)) {
                if (context.HasKey(out _)) {
                    if (context.HasCommander(out var commander)) {
                        var trigger = InputTrigger();
                        var fileName = "'New file'";
                        await commander.ShowPalette($"{trigger} {fileName}", trigger.Length + 1, fileName.Length, cancellationToken);
                        return true;
                    }
                }
                if (context.HasParameter(out var parameter)) {
                    var name = parameter.Name?.Trim() ?? "";
                    if (name != "") {
                        var open = parameter.Open;
                        await active.Deploy(
                            cancellationToken: cancellationToken,
                            createFiles: new[] { name },
                            then: async () => {
                                if (open) {
                                    await active.OpenCreated(name, cancellationToken);
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
            public string Name {
                get => _Name ?? (_Name = "");
                set => _Name = value;
            }
            private string _Name;

            [Switch(Name = "open", ShortName = 'o')]
            public bool Open { get; set; }
        }
    }
}
