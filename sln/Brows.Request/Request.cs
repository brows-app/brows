using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public static class Request {
        public static RequestFactory Factory { get; set; }

        public static object Create(Action<object> execute, Func<object, bool> canExecute, INotifyPropertyChanged owner, params string[] dependencies) {
            return Factory.Create(execute, canExecute, owner, dependencies);
        }

        public static object Create(Action<object> execute, Func<object, bool> canExecute) {
            return Create(execute, canExecute, null);
        }

        public static object CreateAsync(Func<object, CancellationToken, Task> execute, Func<object, bool> canExecute, INotifyPropertyChanged owner, params string[] dependencies) {
            return Factory.CreateAsync(execute, canExecute, owner, dependencies);
        }

        public static object CreateAsync(Func<object, CancellationToken, Task> execute, Func<object, bool> canExecute) {
            return CreateAsync(execute, canExecute, null);
        }
    }
}
