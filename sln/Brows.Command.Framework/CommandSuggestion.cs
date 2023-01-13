using Domore.Notification;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Brows {
    internal class CommandSuggestion : Notifier, ICommandSuggestion {
        private static readonly PropertyChangedEventArgs HelpEventArgs = new(nameof(Help));
        private static readonly PropertyChangedEventArgs RelevanceEventArgs = new(nameof(Relevance));
        private static readonly PropertyChangedEventArgs InputEventArgs = new(nameof(Input));
        private static readonly PropertyChangedEventArgs GroupEventArgs = new(nameof(Group));
        private static readonly PropertyChangedEventArgs DescriptionEventArgs = new(nameof(Description));
        private static readonly PropertyChangedEventArgs[] DescriptionExistsEventArgs = new PropertyChangedEventArgs[] {
            new(nameof(DescriptionExists))
        };

        private IEnumerable<string> InputAliases =>
            Command.Trigger?.Input?.Aliases?.AsEnumerable() ??
            Array.Empty<string>();

        public static readonly string DescriptionHidden = nameof(DescriptionHidden);

        public Command Command { get; }
        public ICommandContext Context { get; }

        public string Help {
            get => _Help ?? Input;
            set => Change(ref _Help, value, HelpEventArgs);
        }
        private string _Help;

        public int Relevance {
            get => _Relevance;
            set => Change(ref _Relevance, value, RelevanceEventArgs);
        }
        private int _Relevance;

        public string Input {
            get => _Input;
            set => Change(ref _Input, value, InputEventArgs);
        }
        private string _Input;

        public string Group {
            get => _Group;
            set => Change(ref _Group, value, GroupEventArgs);
        }
        private string _Group;

        public string Description {
            get => _Description;
            set => Change(ref _Description, value, DescriptionEventArgs, DescriptionExistsEventArgs);
        }
        private string _Description;

        public string InputAlias =>
            string.Join(",", InputAliases);

        public string KeyboardTrigger =>
            string.Join(",",
                Command.Trigger?.Press?.AsEnumerable() ??
                Array.Empty<ITriggerPress>());

        public bool InputAliasExists =>
            InputAliases.Any();

        public bool KeyboardTriggerExists =>
            Command.Trigger?.Press?.Any() ?? false;

        public bool DescriptionExists =>
            !string.IsNullOrWhiteSpace(Description);

        public CommandSuggestion(Command command, ICommandContext context) {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }
    }
}
