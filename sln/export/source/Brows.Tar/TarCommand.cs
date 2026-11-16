using Brows.Commands;
using System;

namespace Brows;

internal abstract class TarCommand<TParameter> : Command<TParameter> where TParameter : new() {
    protected sealed override Type Provider {
        get {
            return typeof(TarProvider);
        }
    }
}
