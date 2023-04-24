using Domore.Notification;
using System;
using System.Collections.ObjectModel;

namespace Brows {
    using Gui;

    internal sealed class CommandSuggestionCollection : Notifier, IControlled<ICommandSuggestionCollectionController> {
        private readonly ObservableCollection<ICommandSuggestion> Observable = new();

        private void Controller_CurrentSuggestionChanged(object sender, EventArgs e) {
            CurrentSuggestion = Controller?.CurrentSuggestion;
            CurrentSuggestionChanged?.Invoke(this, e);
        }

        public event EventHandler CurrentSuggestionChanged;

        public object Source =>
            Observable;

        public ICommandSuggestion CurrentSuggestion {
            get => _CurrentSuggestion;
            private set => Change(ref _CurrentSuggestion, value, nameof(CurrentSuggestion));
        }
        private ICommandSuggestion _CurrentSuggestion;

        public void Add(ICommandSuggestion suggestion) {
            Observable.Add(suggestion);
        }

        public void Clear() {
            Observable.Clear();
        }

        public void MoveCurrentSuggestion(PressKey pressKey) {
            Controller?.MoveCurrentSuggestion(pressKey);
        }

        ICommandSuggestionCollectionController IControlled<ICommandSuggestionCollectionController>.Controller {
            set {
                var newValue = value;
                var oldValue = Controller;
                if (oldValue != newValue) {
                    if (oldValue != null) {
                        oldValue.CurrentSuggestionChanged -= Controller_CurrentSuggestionChanged;
                    }
                    if (newValue != null) {
                        newValue.CurrentSuggestionChanged += Controller_CurrentSuggestionChanged;
                    }
                    Controller = newValue;
                }
            }
        }
        private ICommandSuggestionCollectionController Controller;
    }
}
