using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Brows {
    public interface IRequestFactory {
        object Create(Action<object> execute, Func<object, bool> canExecute, INotifyPropertyChanged owner, IEnumerable<string> dependencies);
    }
}
