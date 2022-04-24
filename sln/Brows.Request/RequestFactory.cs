using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class RequestFactory {
        public abstract object Create(Action<object> execute, Func<object, bool> canExecute, INotifyPropertyChanged owner, IEnumerable<string> dependencies);
        public abstract object CreateAsync(Func<object, CancellationToken, Task> execute, Func<object, bool> canExecute, INotifyPropertyChanged owner, IEnumerable<string> dependencies);
    }
}
