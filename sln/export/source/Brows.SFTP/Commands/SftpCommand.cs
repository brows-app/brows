using System;
using System.Collections.Generic;

namespace Brows.Commands;

internal abstract class SftpCommand<TParameter> : Command<TParameter> where TParameter : new() {
    protected sealed override Type Provider => typeof(SftpProvider);
}
