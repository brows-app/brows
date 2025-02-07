﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Security {
    public interface IUnprotectData : IExport {
        Task<bool> Work(byte[] data, byte[] entropy, Action<byte[]> set, CancellationToken token);
    }
}
