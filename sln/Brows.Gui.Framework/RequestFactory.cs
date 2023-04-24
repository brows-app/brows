using System;
using System.Collections.Generic;
using System.ComponentModel;
using ICOMMAND = System.Windows.Input.ICommand;

namespace Brows {
    public sealed class RequestFactory : IRequestFactory {
        public object Create(Action<object> execute, Func<object, bool> canExecute, INotifyPropertyChanged owner, IEnumerable<string> dependencies) {
            return new CommandImplementation(
                owner: owner ?? new PropertyChangedDefault(),
                dependencies: new HashSet<string>(dependencies ?? Array.Empty<string>()),
                execute: execute ?? (_ => { }),
                canExecute: canExecute ?? (_ => true));
        }

        private sealed class PropertyChangedDefault : INotifyPropertyChanged {
            event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged {
                add { }
                remove { }
            }
        }

        private sealed class CommandImplementation : ICOMMAND {
            private Action<object> Execute { get; }
            private Func<object, bool> CanExecute { get; }
            private IReadOnlySet<string> Dependencies { get; }

            private void Owner_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e != null) {
                    if (Dependencies.Contains(e.PropertyName)) {
                        CanExecuteChanged?.Invoke(this, e);
                    }
                }
            }

            public event EventHandler CanExecuteChanged;

            public CommandImplementation(Action<object> execute, Func<object, bool> canExecute, INotifyPropertyChanged owner, IReadOnlySet<string> dependencies) {
                Execute = execute ?? throw new ArgumentNullException(nameof(execute));
                CanExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute));
                Dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
                PropertyChangedEventManager.AddHandler(owner, Owner_PropertyChanged, string.Empty);
            }

            bool ICOMMAND.CanExecute(object parameter) {
                return CanExecute(parameter);
            }

            void ICOMMAND.Execute(object parameter) {
                Execute(parameter);
            }
        }
    }
}
