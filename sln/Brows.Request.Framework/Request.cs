using System;
using System.ComponentModel;

namespace Brows {
    public static class Request {
        public static IRequestFactory Factory { get; set; }

        public static object Create(Action<object> execute, Func<object, bool> canExecute, INotifyPropertyChanged owner, params string[] dependencies) {
            return Factory?.Create(execute, canExecute, owner, dependencies);
        }

        public static object Create(Action<object> execute) {
            return Create(execute, canExecute: null, owner: null);
        }
    }
}
