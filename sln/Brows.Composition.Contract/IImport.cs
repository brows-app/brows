using System;
using System.Collections.Generic;

namespace Brows {
    public interface IImport {
        IImport For(Type target);
        TService Get<TService>();
        IReadOnlyList<TService> List<TService>();
    }
}
