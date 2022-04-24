using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    using ComponentModel;

    public class CommandSuggestion : NotifyPropertyChanged, ICommandSuggestion {
        private IEnumerable<string> InputAliases =>
            Command.InputTriggers.SelectMany(trigger => trigger.Alias).Distinct();

        public Command Command { get; }
        public ICommandContext Context { get; }

        public string Help {
            get => _Help ?? Input;
            set => Change(ref _Help, value, nameof(Help));
        }
        private string _Help;

        public int Relevance {
            get => _Relevance;
            set => Change(ref _Relevance, value, nameof(Relevance));
        }
        private int _Relevance;

        public string Input {
            get => _Input;
            set => Change(ref _Input, value, nameof(Input));
        }
        private string _Input;

        public string Group {
            get => _Group;
            set => Change(ref _Group, value, nameof(Group), nameof(Header));
        }
        private string _Group;

        public string Description {
            get => _Description ?? Translate.Value($"Command_Description_{Command.GetType().Name}");
            set => Change(ref _Description, value, nameof(Description));
        }
        private string _Description;

        public string Header =>
            Translate.Value($"Command_Header_Group_{Group}");

        public string InputAlias =>
            string.Join(",", InputAliases);

        public string KeyboardTrigger =>
            string.Join(",", Command.KeyboardTriggers);

        public bool InputAliasExists =>
            InputAliases.Any();

        public bool KeyboardTriggerExists =>
            Command.KeyboardTriggers.Any();

        public bool DescriptionExists =>
            !string.IsNullOrWhiteSpace(Description);

        public CommandSuggestion(Command command, ICommandContext context) {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }
    }
}
