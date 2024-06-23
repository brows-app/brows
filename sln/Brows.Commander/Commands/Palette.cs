using Domore.Conf.Converters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Commands {
    internal sealed class Palette : Command<Palette.Parameter> {
        protected sealed override bool Work(Context context) {
            if (null == context) return false;
            if (false == context.GetParameter(out var parameter)) {
                return false;
            }
            var input = parameter.Input(context);
            var suggestCommands = parameter.SuggestCommands(context);
            return context.ShowPalette(new CommandPaletteConfig {
                Input = input,
                SelectedStart = 0,
                SelectedLength = input.Length,
                SuggestCommands = suggestCommands
            });
        }

        public sealed class Parameter {
            public Show Show { get; set; }

            [ConfListItems]
            public List<string> Suggest {
                get => _Suggest ?? (_Suggest = []);
                set => _Suggest = value;
            }
            private List<string> _Suggest;

            public string Input(ICommandContext context) {
                var show = Show;
                switch (show) {
                    case Show.Nothing:
                        break;
                    case Show.EntryID: {
                        if (context.HasPanel(out var active)) {
                            if (active.HasSelection(out var entries)) {
                                if (entries.Count > 0) {
                                    return entries.FirstOrDefault()?.ID;
                                }
                            }
                        }
                        break;
                    }
                    case Show.ProviderID: {
                        if (context.HasPanel(out var active)) {
                            if (active.HasProvider(out Provider provider)) {
                                return provider.ID;
                            }
                        }
                        break;
                    }
                }
                return "";
            }

            public IEnumerable<ICommand> SuggestCommands(ICommandContext context) {
                if (context == null) return null;
                if (context.HasCommander(out var commander) == false) return null;
                if (commander is not Commander cCommander) {
                    return null;
                }
                var commands = cCommander.Commands?.AsEnumerable();
                if (commands == null) {
                    return null;
                }
                var suggest = new HashSet<string>(Suggest, StringComparer.OrdinalIgnoreCase);
                if (suggest.Count == 0) {
                    return null;
                }
                return commands
                    .Select(c => new { Command = c, c.GetType().Name })
                    .Where(c =>
                        !suggest.Contains($"!{c.Name}") && (
                            suggest.Contains("*") ||
                            suggest.Contains(c.Name)
                        ))
                    .Select(c => c.Command)
                    .ToList();
            }
        }

        public enum Show {
            Nothing = 0,
            EntryID,
            ProviderID
        }
    }
}
