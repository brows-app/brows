using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using ICOMMAND = System.Windows.Input.ICommand;

namespace Brows {
    using Threading.Tasks;

    public class CommandFactory : RequestFactory {
        public override object Create(Action<object> execute, Func<object, bool> canExecute, INotifyPropertyChanged owner, IEnumerable<string> dependencies) {
            return new CommandImplementation(
                owner: owner ?? new PropertyChangedDefault(),
                dependencies: new HashSet<string>(dependencies ?? Array.Empty<string>()),
                execute: execute ?? (_ => { }),
                canExecute: canExecute ?? (_ => true));
        }

        public override object CreateAsync(Func<object, CancellationToken, Task> execute, Func<object, bool> canExecute, INotifyPropertyChanged owner, IEnumerable<string> dependencies) {
            if (null == owner) throw new ArgumentNullException(nameof(owner));
            var taskRunner = new TaskHandler(owner.GetType());
            return Create(
                owner: owner,
                dependencies: dependencies,
                canExecute: canExecute,
                execute: obj => {
                    var
                    task = execute(obj, default);
                    taskRunner.Begin(task);
                });
        }

        private class PropertyChangedDefault : INotifyPropertyChanged {
            event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged {
                add { }
                remove { }
            }
        }

        private class CommandImplementation : ICOMMAND {
            private Action<object> Execute { get; }
            private Func<object, bool> CanExecute { get; }
            private HashSet<string> Dependencies { get; }

            private void Owner_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e != null) {
                    if (Dependencies.Contains(e.PropertyName)) {
                        CanExecuteChanged?.Invoke(this, e);
                    }
                }
            }

            public event EventHandler CanExecuteChanged;

            public CommandImplementation(Action<object> execute, Func<object, bool> canExecute, INotifyPropertyChanged owner, HashSet<string> dependencies) {
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
