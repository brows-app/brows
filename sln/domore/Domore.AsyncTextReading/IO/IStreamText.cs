﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Domore.IO {
    public interface IStreamText {
        bool StreamValid { get; }
        long StreamLength { get; }
        Stream StreamText();
        Task<IDisposable> StreamReady(CancellationToken cancellationToken);
    }
}
