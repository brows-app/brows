using Brows.Commands;
using System;

namespace Brows;

internal abstract class SevenZipCommand<TParameter> : Command<TParameter> where TParameter : new() {
    protected sealed override Type Provider {
        get {
            return typeof(SevenZipProvider);
        }
    }
}
