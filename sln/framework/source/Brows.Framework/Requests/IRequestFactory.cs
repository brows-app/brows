using Brows.Composition;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Brows.Requests;

public interface IRequestFactory : IExport {
    object Create(Action<object> execute,
                  Func<object, bool> canExecute,
                  INotifyPropertyChanged owner,
                  IEnumerable<string> dependencies);
}
